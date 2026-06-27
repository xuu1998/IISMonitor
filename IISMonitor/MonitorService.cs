using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using IISMonitor.Models;
using Microsoft.Web.Administration;

namespace IISMonitor
{
    public enum CheckType
    {
        Site,
        AppPool,
        Resource
    }

    /// <summary>
    /// 核心监控服务引擎
    /// </summary>
    public class MonitorService
    {
        private System.Threading.Timer timer;
        private MonitorConfig config;
        private volatile bool isRunning;
        private int _inProgressGuard = 0;

        // 线程安全计数器
        private ConcurrentDictionary<string, int> siteFailureCount = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> appPoolFailureCount = new ConcurrentDictionary<string, int>();

        // 恢复历史记录（加锁保护）
        private Dictionary<string, Queue<DateTime>> _recoveryHistory = new Dictionary<string, Queue<DateTime>>();
        private readonly object _recoveryLock = new object();
        private const int MaxRecoveriesPerWindow = 3;
        private static readonly TimeSpan RecoveryWindow = TimeSpan.FromMinutes(5);

        /// <summary>
        /// HTTP 探测最大尝试次数（含首次），用于抑制瞬时网络抖动误报
        /// </summary>
        private const int HttpCheckAttempts = 2;

        /// <summary>
        /// 正在恢复中的目标集合（key 形如 "AppPool:xxx" / "Site:xxx"）。
        /// 恢复动作异步执行，标记存在期间跳过对同一目标的重复恢复触发，
        /// 避免恢复期间检测继续跑而重复触发恢复。
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _recoveringKeys = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// 全局恢复信号量：串行化所有恢复操作，确保同一时间只有一个 iisreset 在执行，
        /// 避免 AppPool 恢复与 Site 恢复并发跑 iisreset 互相冲突（如 1062 错误）。
        /// </summary>
        private static readonly SemaphoreSlim _recoverySemaphore = new SemaphoreSlim(1, 1);

        // 可选服务
        private AlertService _alertService;
        private ResourceMonitor _resourceMonitor;
        private int _tickCount = 0;
        private CancellationTokenSource _cts;

        public event Action<string> OnStatusUpdate;
        public event Action<CheckType, string, bool> OnCheckResult;
        public event Action<ResourceSnapshot> OnResourceSnapshot;

        public bool IsRunning => isRunning;

        /// <summary>
        /// 启动监控服务
        /// </summary>
        public void Start(MonitorConfig cfg)
        {
            if (isRunning) return;

            // 检查管理员权限
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                string msg = "IISMonitor 需要管理员权限运行，请以管理员身份重新启动程序。";
                Logger.Log(msg, true);
                OnStatusUpdate?.Invoke(msg);
                return;
            }

            // 检查是否有监控目标
            bool hasSites = cfg.MonitoredSites != null && cfg.MonitoredSites.Length > 0;
            bool hasAppPools = cfg.MonitoredAppPools != null && cfg.MonitoredAppPools.Length > 0;
            if (!hasSites && !hasAppPools)
            {
                string msg = "未配置任何监控目标（站点和应用池均为空），请先添加监控项。";
                Logger.Log(msg, true);
                OnStatusUpdate?.Invoke(msg);
                return;
            }

            config = cfg;
            Logger.Initialize(config.LogPath);
            Logger.Log("监控服务启动");

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // 初始化告警服务
            if (config.AlertSettings != null &&
                (config.AlertSettings.EnableSmtp || config.AlertSettings.EnableWebhook))
            {
                _alertService = new AlertService(config.AlertSettings);
                Logger.Log("告警服务已初始化");
            }

            // 初始化资源监控
            if (config.EnableResourceMonitoring)
            {
                _resourceMonitor = new ResourceMonitor();
                Logger.Log("资源监控已初始化");
            }

            // 重置计数器
            siteFailureCount.Clear();
            appPoolFailureCount.Clear();
            _tickCount = 0;

            timer = new System.Threading.Timer(OnTimerTick, null, 1000, config.CheckIntervalSeconds * 1000);
            isRunning = true;
        }

        /// <summary>
        /// 停止监控服务
        /// </summary>
        public void Stop()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            isRunning = false;
            try { _cts?.Cancel(); } catch { }
            Logger.Log("监控服务停止");
        }

        /// <summary>
        /// 定时器回调（带重入保护）
        /// </summary>
        private void OnTimerTick(object state)
        {
            if (Interlocked.CompareExchange(ref _inProgressGuard, 1, 0) != 0)
            {
                Logger.Log("上一次健康检查尚未完成，跳过本次", false);
                return;
            }

            try
            {
                if (!isRunning) return;
                PerformHealthCheck();
                Interlocked.Increment(ref _tickCount);
            }
            finally
            {
                Interlocked.Exchange(ref _inProgressGuard, 0);
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        public void PerformHealthCheck()
        {
            try
            {
                var token = _cts?.Token ?? CancellationToken.None;

                // 1. 检查应用程序池状态
                if (config.EnableAppPoolCheck)
                {
                    CheckAppPools(token);
                }

                if (token.IsCancellationRequested) return;

                // 2. 检查站点 HTTP 状态
                if (config.EnableHttpCheck)
                {
                    CheckSitesHttp(token);
                }

                if (token.IsCancellationRequested) return;

                // 3. 资源监控（按指定间隔执行）
                if (config.EnableResourceMonitoring && _resourceMonitor != null)
                {
                    int interval = config.ResourceMonitorIntervalSeconds > 0
                        ? config.ResourceMonitorIntervalSeconds
                        : 300;
                    int checkInterval = Math.Max(1, config.CheckIntervalSeconds);
                    int everyNTicks = Math.Max(1, (int)Math.Ceiling((double)interval / checkInterval));
                    if (_tickCount % everyNTicks == 0)
                    {
                        System.Threading.Tasks.Task.Run(() => CollectResourceMetrics(), token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError("健康检查过程中发生异常", ex);
                OnStatusUpdate?.Invoke($"检查异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 采集服务器资源指标
        /// </summary>
        private void CollectResourceMetrics()
        {
            try
            {
                var snapshot = _resourceMonitor.GetSnapshot();
                string detail = $"CPU: {snapshot.CpuPercent:F1}%, 内存: {snapshot.MemoryPercent:F1}%, 磁盘: {snapshot.DiskPercent:F1}%";
                PersistHealthResult("Resource", "Server", true, detail);
                OnResourceSnapshot?.Invoke(snapshot);

                // 资源超阈值告警
                var thresholds = config.ResourceAlertThresholds ?? new ResourceAlertThresholds();
                if (snapshot.CpuPercent > thresholds.CpuPercent)
                {
                    string msg = $"CPU 使用率过高: {snapshot.CpuPercent:F1}% (阈值 {thresholds.CpuPercent:F0}%)";
                    Logger.Log(msg, true);
                    OnStatusUpdate?.Invoke(msg);
                    _alertService?.SendAlert("CPU", msg, AlertLevel.Warning);
                }
                if (snapshot.MemoryPercent > thresholds.MemoryPercent)
                {
                    string msg = $"内存使用率过高: {snapshot.MemoryPercent:F1}% (阈值 {thresholds.MemoryPercent:F0}%)";
                    Logger.Log(msg, true);
                    OnStatusUpdate?.Invoke(msg);
                    _alertService?.SendAlert("Memory", msg, AlertLevel.Warning);
                }
                if (snapshot.DiskPercent > thresholds.DiskPercent)
                {
                    string msg = $"磁盘使用率过高: {snapshot.DiskPercent:F1}% (阈值 {thresholds.DiskPercent:F0}%)";
                    Logger.Log(msg, true);
                    OnStatusUpdate?.Invoke(msg);
                    _alertService?.SendAlert("Disk", msg, AlertLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("采集资源指标失败", ex);
            }
        }

        /// <summary>
        /// 故障恢复退避检查（线程安全）
        /// </summary>
        private bool CheckRecoveryThrottle(string key)
        {
            lock (_recoveryLock)
            {
                if (!_recoveryHistory.ContainsKey(key))
                    _recoveryHistory[key] = new Queue<DateTime>();

                var queue = _recoveryHistory[key];
                DateTime now = DateTime.Now;

                // 移除 5 分钟前的记录
                while (queue.Count > 0 && now - queue.Peek() > RecoveryWindow)
                    queue.Dequeue();

                // 如果 [恢复次数已达上限，跳过本次操作
                if (queue.Count >= MaxRecoveriesPerWindow)
                {
                    Logger.Log($"告警: [{key}] 在 {RecoveryWindow.TotalMinutes} 分钟内已恢复 {queue.Count} 次，达到上限，跳过本次恢复操作", true);
                    OnStatusUpdate?.Invoke($"恢复频率超限: {key}，跳过本次恢复");
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
        }

        /// <summary>
        /// 持久化健康检查结果到 JSONL 文件
        /// </summary>
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions();

        private void PersistHealthResult(string checkType, string target, bool success, string detail = "")
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                string filePath = Path.Combine(logDir, "health_results.jsonl");
                var record = new HealthRecord
                {
                    Timestamp = DateTime.Now,
                    CheckType = checkType,
                    Target = target,
                    Result = success ? "成功" : "失败",
                    Detail = detail
                };
                string jsonLine = JsonSerializer.Serialize(record, s_jsonOptions);
                File.AppendAllText(filePath, jsonLine + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        private void CheckAppPools(CancellationToken token)
        {
            // 恢复操作进行中时，ServerManager 会阻塞在正在重启的 IIS 管理服务上，
            // 导致监控线程长时间卡死。此时跳过应用池状态检查，避免“跳过本次”雪崩。
            if (_recoveringKeys.Count > 0)
            {
                Logger.Log("恢复操作进行中，跳过本次应用池状态检查（避免 ServerManager 阻塞）");
                return;
            }

            var statuses = IISHelper.GetAppPoolStatuses();

            foreach (var poolName in config.MonitoredAppPools)
            {
                if (token.IsCancellationRequested) return;

                bool poolRunning = statuses.ContainsKey(poolName) && statuses[poolName];
                OnCheckResult?.Invoke(CheckType.AppPool, poolName, poolRunning);
                PersistHealthResult("AppPool", poolName, poolRunning);

                if (!poolRunning)
                {
                    int currentCount = appPoolFailureCount.AddOrUpdate(poolName, 1, (key, old) => old + 1);
                    string msg = $"应用程序池 [{poolName}] 状态异常 (失败次数: {currentCount})";
                    Logger.Log(msg);
                    OnStatusUpdate?.Invoke(msg);

                    if (currentCount >= config.ConsecutiveFailuresBeforeRestart)
                    {
                        HandleAppPoolFailure(poolName);
                        appPoolFailureCount[poolName] = 0;
                    }
                }
                else
                {
                    appPoolFailureCount[poolName] = 0;
                }
            }
        }

        private void CheckSitesHttp(CancellationToken token)
        {
            if (config.MonitoredSites == null || config.MonitoredSites.Length == 0)
                return;

            var sites = config.MonitoredSites;
            var results = new ConcurrentDictionary<string, bool>();

            // 并发执行 HTTP 探测（网络密集型），最多 8 路并发
            try
            {
                var options = new System.Threading.Tasks.ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = 8
                };
                System.Threading.Tasks.Parallel.ForEach(sites, options, siteUrl =>
                {
                    string keyword = null;
                    config.SiteExpectedKeywords?.TryGetValue(siteUrl, out keyword);
                    // localhost 用更短超时（3s），避免 IIS 半启动时 TCP 连接挂起拖慢整轮检查
                    int timeout = IsLocalhost(siteUrl) ? Math.Min(config.HttpTimeoutSeconds, 3) : config.HttpTimeoutSeconds;
                    bool isHealthy = IISHelper.CheckSiteHttp(siteUrl, timeout, keyword, token, HttpCheckAttempts);
                    results[siteUrl] = isHealthy;
                });
            }
            catch (OperationCanceledException) { return; }

            // 失败处理串行执行，避免并发触发 IIS 恢复
            foreach (var siteUrl in sites)
            {
                if (token.IsCancellationRequested) return;

                bool isHealthy = results.TryGetValue(siteUrl, out var h) && h;
                OnCheckResult?.Invoke(CheckType.Site, siteUrl, isHealthy);
                PersistHealthResult("SiteHttp", siteUrl, isHealthy);

                if (!isHealthy)
                {
                    int currentCount = siteFailureCount.AddOrUpdate(siteUrl, 1, (key, old) => old + 1);
                    string msg = $"站点 [{siteUrl}] 无法访问 (失败次数: {currentCount})";
                    Logger.Log(msg);
                    OnStatusUpdate?.Invoke(msg);

                    if (currentCount >= config.ConsecutiveFailuresBeforeRestart)
                    {
                        HandleSiteFailure(siteUrl);
                        siteFailureCount[siteUrl] = 0;
                    }
                }
                else
                {
                    siteFailureCount[siteUrl] = 0;
                }
            }
        }

        /// <summary>
        /// 判断 URL 是否指向本机（localhost / 127.0.0.1 / 本机名）
        /// </summary>
        private static bool IsLocalhost(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var uri = new Uri(url);
                string host = uri.Host;
                return host == "localhost" || host == "127.0.0.1" || host == "::1"
                    || host.Equals(System.Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// 处理应用程序池故障。恢复动作异步执行，不阻塞监控线程。
        /// </summary>
        private void HandleAppPoolFailure(string poolName)
        {
            string key = $"AppPool:{poolName}";

            if (!CheckRecoveryThrottle(key))
                return;

            // 正在恢复中则跳过，避免重复触发
            if (!_recoveringKeys.TryAdd(key, 0))
            {
                Logger.Log($"{key} 正在恢复中，跳过本次恢复触发");
                return;
            }

            OnStatusUpdate?.Invoke($"正在处理应用程序池故障: {poolName}");
            Logger.Log($"达到故障阈值，开始处理: {poolName}");
            _alertService?.SendAlert(key, $"应用程序池 {poolName} 故障，准备恢复", AlertLevel.Warning);

            System.Threading.Tasks.Task.Run(() =>
            {
                Logger.Log($"{key} 等待恢复信号量...");
                _recoverySemaphore.Wait();
                try
                {
                    Logger.Log($"{key} 开始执行恢复");
                    bool success = ExecuteAppPoolRecovery(poolName);
                    if (success)
                    {
                        Logger.Log($"故障恢复成功: {poolName}");
                        OnStatusUpdate?.Invoke($"恢复成功: {poolName}");
                        _alertService?.SendAlert(key, $"应用程序池 {poolName} 恢复成功", AlertLevel.Info);
                    }
                    else
                    {
                        Logger.LogError($"故障恢复失败: {poolName}");
                        OnStatusUpdate?.Invoke($"恢复失败: {poolName}");
                        _alertService?.SendAlert(key, $"应用程序池 {poolName} 恢复失败！", AlertLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"恢复应用程序池 {poolName} 时发生异常", ex);
                }
                finally
                {
                    _recoverySemaphore.Release();
                    _recoveringKeys.TryRemove(key, out _);
                }
            });
        }

        /// <summary>
        /// 按当前重启策略执行应用程序池恢复（同步、可阻塞，应在后台线程调用）
        /// </summary>
        private bool ExecuteAppPoolRecovery(string poolName)
        {
            switch (config.RestartStrategy)
            {
                case RestartStrategyType.AppPoolOnly:
                    return IISHelper.RecycleAppPool(poolName);

                case RestartStrategyType.AppPoolThenIIS:
                    if (IISHelper.RecycleAppPool(poolName)) return true;
                    OnStatusUpdate?.Invoke("应用池恢复失败，尝试重启整个 IIS");
                    return IISHelper.RestartIIS(config.IisResetTimeoutSeconds, config.IisReadyTimeoutSeconds);

                case RestartStrategyType.IISOnly:
                    return IISHelper.RestartIIS(config.IisResetTimeoutSeconds, config.IisReadyTimeoutSeconds);

                case RestartStrategyType.StopStartAppPool:
                    return IISHelper.StopStartAppPool(poolName);

                case RestartStrategyType.StopStartAppPoolThenIIS:
                    if (IISHelper.StopStartAppPool(poolName)) return true;
                    OnStatusUpdate?.Invoke("应用池恢复失败，尝试重启整个 IIS");
                    return IISHelper.RestartIIS(config.IisResetTimeoutSeconds, config.IisReadyTimeoutSeconds);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 处理站点故障。恢复动作异步执行，不阻塞监控线程。
        /// </summary>
        private void HandleSiteFailure(string siteUrl)
        {
            string key = $"Site:{siteUrl}";

            if (!CheckRecoveryThrottle(key))
                return;

            // 正在恢复中则跳过，避免重复触发
            if (!_recoveringKeys.TryAdd(key, 0))
            {
                Logger.Log($"{key} 正在恢复中，跳过本次恢复触发");
                return;
            }

            OnStatusUpdate?.Invoke($"站点无法访问: {siteUrl}");
            Logger.Log($"站点故障: {siteUrl}");
            _alertService?.SendAlert(key, $"站点 {siteUrl} 无法访问", AlertLevel.Warning);

            System.Threading.Tasks.Task.Run(() =>
            {
                Logger.Log($"{key} 等待恢复信号量...");
                _recoverySemaphore.Wait();
                try
                {
                    Logger.Log($"{key} 开始执行恢复");
                    ExecuteSiteRecovery(siteUrl);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"恢复站点 {siteUrl} 时发生异常", ex);
                }
                finally
                {
                    _recoverySemaphore.Release();
                    _recoveringKeys.TryRemove(key, out _);
                }
            });
        }

        /// <summary>
        /// 执行站点恢复并验证（同步、可阻塞，应在后台线程调用）。
        /// 若对应应用池已在运行（说明 AppPool 恢复链已成功重启 IIS），则跳过重复的 IIS 重启，
        /// 直接进入 HTTP 验证，避免 Site 恢复与 AppPool 恢复各跑一次 iisreset。
        /// </summary>
        private void ExecuteSiteRecovery(string siteUrl)
        {
            string inferredPool = InferAppPoolFromUrl(siteUrl);
            if (!string.IsNullOrEmpty(inferredPool))
            {
                // 先检查应用池当前是否已在运行（AppPool 恢复链可能刚成功）
                bool poolAlreadyRunning = IISHelper.IsAppPoolRunning(inferredPool);
                if (poolAlreadyRunning)
                {
                    Logger.Log($"站点 {siteUrl} 对应应用池 {inferredPool} 已在运行，跳过重复 IIS 重启，直接验证站点");
                }
                else
                {
                    // 应用池未运行，走应用池恢复链（同步执行，含 IIS 兜底）
                    bool success = ExecuteAppPoolRecovery(inferredPool);
                    if (!success)
                    {
                        Logger.LogError($"站点 {siteUrl} 对应应用池 {inferredPool} 恢复失败");
                    }
                }
                // 无论应用池恢复是否成功，都以站点 HTTP 实测为准
                VerifySiteRecovery(siteUrl);
                return;
            }

            // 无法推断应用池：仅当策略允许重启 IIS 时直接重启 IIS
            bool strategyAllowsIisRestart =
                config.RestartStrategy == RestartStrategyType.IISOnly ||
                config.RestartStrategy == RestartStrategyType.AppPoolThenIIS ||
                config.RestartStrategy == RestartStrategyType.StopStartAppPoolThenIIS;

            if (strategyAllowsIisRestart)
            {
                bool success = IISHelper.RestartIIS(config.IisResetTimeoutSeconds, config.IisReadyTimeoutSeconds);
                if (!success)
                {
                    Logger.LogError($"站点 {siteUrl} IIS 重启操作失败");
                }
                // 无论 IIS 操作是否成功，都以站点 HTTP 实测为准
                VerifySiteRecovery(siteUrl);
            }
            else
            {
                Logger.Log($"站点 {siteUrl} 故障但无法推断应用池，且当前策略({config.RestartStrategy})不含 IIS 重启，跳过自动恢复");
                _alertService?.SendAlert($"Site:{siteUrl}", $"站点 {siteUrl} 无法访问，且无法自动恢复（未匹配到应用池）", AlertLevel.Warning);
            }
        }

        /// <summary>
        /// 恢复动作后验证站点是否真正可达。
        /// 先确保 IIS 站点对象本身处于 Started（应用池启动 ≠ 站点启动），
        /// 再预热并做 HTTP 实测，避免“应用池起来但站点 Stopped”的假成功。
        /// </summary>
        private void VerifySiteRecovery(string siteUrl)
        {
            // 1. 确保 IIS 站点对象已启动（应用池与站点是两个独立对象）
            string siteName = IISHelper.InferSiteNameFromUrl(siteUrl);
            if (!string.IsNullOrEmpty(siteName))
            {
                var state = IISHelper.GetSiteState(siteName);
                if (state.HasValue && state.Value != ObjectState.Started)
                {
                    Logger.Log($"站点 {siteName} 当前状态为 {state.Value}（非 Started），尝试启动站点...");
                    IISHelper.StartSite(siteName);
                }
                else if (!state.HasValue)
                {
                    Logger.Log($"未找到与 {siteUrl} 匹配的 IIS 站点，跳过站点启动步骤");
                }
            }
            else
            {
                Logger.Log($"无法从 {siteUrl} 反查站点名称，跳过站点启动步骤");
            }

            // 2. 预热等待：应用池/站点刚启动时首个请求可能较慢
            const int warmupMs = 5000;
            Logger.Log($"等待 {warmupMs}ms 预热后验证站点 {siteUrl} 可达性...");
            Thread.Sleep(warmupMs);

            // 3. HTTP 实测
            string keyword = null;
            config.SiteExpectedKeywords?.TryGetValue(siteUrl, out keyword);

            // 用多次尝试，容忍预热未完成
            bool reachable = IISHelper.CheckSiteHttp(siteUrl, config.HttpTimeoutSeconds, keyword, CancellationToken.None, 3);

            if (reachable)
            {
                Logger.Log($"站点 {siteUrl} 恢复后验证通过，HTTP 可达");
                OnStatusUpdate?.Invoke($"站点 {siteUrl} 已恢复（HTTP 验证通过）");
                _alertService?.SendAlert($"Site:{siteUrl}", $"站点 {siteUrl} 已恢复", AlertLevel.Info);
                // 确认可达，清零失败计数器
                siteFailureCount[siteUrl] = 0;
            }
            else
            {
                Logger.LogError($"站点 {siteUrl} 恢复后验证仍不可达，可能需要人工介入");
                OnStatusUpdate?.Invoke($"站点 {siteUrl} 恢复后仍不可达，请人工检查");
                _alertService?.SendAlert($"Site:{siteUrl}", $"站点 {siteUrl} 恢复后仍无法访问，可能需要人工介入", AlertLevel.Error);
            }
        }

        /// <summary>
        /// 通过 URL 推断对应的应用程序池名称
        /// </summary>
        private string InferAppPoolFromUrl(string url)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var uri = new Uri(url);
                    foreach (var site in serverManager.Sites)
                    {
                        foreach (var binding in site.Bindings)
                        {
                            if (binding.Host == uri.Host ||
                                (string.IsNullOrEmpty(binding.Host) && (uri.Host == "localhost" || uri.Host == "127.0.0.1")))
                            {
                                return site.Applications["/"]?.ApplicationPoolName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("推断应用池失败", ex);
            }
            return null;
        }
    }
}
