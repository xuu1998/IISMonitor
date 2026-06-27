using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Microsoft.Web.Administration;

namespace IISMonitor
{
    /// <summary>
    /// IIS 操作辅助类，封装应用池和站点的操作
    /// </summary>
    public static class IISHelper
    {
        /// <summary>
        /// 检查站点 HTTP 是否可达，可选验证页面是否包含指定关键字。
        /// 支持取消令牌与失败重试（仅对网络异常/超时重试，减少瞬时抖动误报）。
        /// </summary>
        public static bool CheckSiteHttp(string url, int timeoutSeconds, string expectedKeyword = null, CancellationToken token = default, int maxAttempts = 1)
        {
            if (maxAttempts < 1) maxAttempts = 1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (token.IsCancellationRequested) return false;

                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = timeoutSeconds * 1000;
                    request.Method = string.IsNullOrEmpty(expectedKeyword) ? "HEAD" : "GET";
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 400)
                            return false;

                        if (string.IsNullOrEmpty(expectedKeyword))
                            return true;

                        using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        {
                            string body = reader.ReadToEnd();
                            return body.Contains(expectedKeyword);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (System.Net.WebException)
                {
                    // 网络/超时异常视为瞬时故障，进行重试
                }
                catch
                {
                    return false;
                }

                if (attempt < maxAttempts - 1 && !token.IsCancellationRequested)
                    Thread.Sleep(1000);
            }
            return false;
        }

        /// <summary>
        /// 使用 Microsoft.Web.Administration API 获取所有应用程序池状态
        /// </summary>
        public static Dictionary<string, bool> GetAppPoolStatuses()
        {
            var result = new Dictionary<string, bool>();
            try
            {
                using (var serverManager = new ServerManager())
                {
                    foreach (var pool in serverManager.ApplicationPools)
                    {
                        result[pool.Name] = pool.State == ObjectState.Started;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("获取应用程序池状态失败", ex);
            }
            return result;
        }

        /// <summary>
        /// 获取本机所有 IIS 站点及其可用监控 URL（从 bindings 拼接）。
        /// 每个 http/https binding 生成一条候选 URL；host 为空时用 localhost。
        /// </summary>
        public static List<IisSiteEntry> GetSiteEntries()
        {
            var result = new List<IisSiteEntry>();
            try
            {
                using (var serverManager = new ServerManager())
                {
                    foreach (var site in serverManager.Sites)
                    {
                        bool running = site.State == ObjectState.Started;
                        foreach (var binding in site.Bindings)
                        {
                            string scheme = binding.Protocol;
                            if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                                !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string host = !string.IsNullOrEmpty(binding.Host) ? binding.Host : "localhost";

                            // binding.EndPoint 含 IP 和端口；端口为 80/443 时省略
                            int port = binding.EndPoint != null ? binding.EndPoint.Port : 0;
                            bool defaultPort = (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && port == 80)
                                || (scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && port == 443)
                                || port == 0;
                            string url = defaultPort
                                ? $"{scheme}://{host}"
                                : $"{scheme}://{host}:{port}";

                            result.Add(new IisSiteEntry
                            {
                                SiteName = site.Name,
                                Url = url,
                                IsRunning = running
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("获取 IIS 站点列表失败", ex);
            }
            return result;
        }

        /// <summary>
        /// 启动指定名称的 IIS 站点。若站点已启动则视为成功。
        /// 返回 true 表示启动后站点处于 Started 状态。
        /// </summary>
        public static bool StartSite(string siteName)
        {
            if (string.IsNullOrEmpty(siteName)) return false;
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var site = serverManager.Sites[siteName];
                    if (site == null)
                    {
                        Logger.LogError($"站点 {siteName} 不存在，无法启动");
                        return false;
                    }

                    if (site.State == ObjectState.Started)
                    {
                        Logger.Log($"站点 {siteName} 已处于启动状态");
                        return true;
                    }

                    Logger.Log($"站点 {siteName} 当前状态为 {site.State}，尝试启动...");
                    site.Start();
                    serverManager.CommitChanges();

                    // 启动后确认状态
                    site = serverManager.Sites[siteName];
                    bool started = site.State == ObjectState.Started;
                    Logger.Log(started
                        ? $"站点 {siteName} 启动成功"
                        : $"站点 {siteName} 启动后状态为 {site.State}");
                    return started;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"启动站点 {siteName} 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 停止指定名称的 IIS 站点。若已停止则视为成功。
        /// </summary>
        public static bool StopSite(string siteName)
        {
            if (string.IsNullOrEmpty(siteName)) return false;
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var site = serverManager.Sites[siteName];
                    if (site == null)
                    {
                        Logger.LogError($"站点 {siteName} 不存在，无法停止");
                        return false;
                    }
                    if (site.State == ObjectState.Stopped)
                    {
                        Logger.Log($"站点 {siteName} 已处于停止状态");
                        return true;
                    }
                    Logger.Log($"站点 {siteName} 当前状态为 {site.State}，尝试停止...");
                    site.Stop();
                    serverManager.CommitChanges();
                    Logger.Log($"站点 {siteName} 已停止");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止站点 {siteName} 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 重启单个 IIS 站点（Stop + Start），不影响其他站点和应用池。
        /// 返回 true 表示启动后站点处于 Started 状态。
        /// </summary>
        public static bool RestartSite(string siteName)
        {
            if (string.IsNullOrEmpty(siteName)) return false;
            Logger.Log($"正在重启站点 {siteName}（不影响其他站点）...");
            if (!StopSite(siteName))
            {
                Logger.LogError($"站点 {siteName} 停止失败，跳过重启");
                return false;
            }
            Thread.Sleep(1000); // 给 IIS 一点时间释放端口
            return StartSite(siteName);
        }

        /// <summary>
        /// 获取指定站点名称的运行状态。返回 null 表示站点不存在或读取失败。
        /// </summary>
        public static ObjectState? GetSiteState(string siteName)
        {
            if (string.IsNullOrEmpty(siteName)) return null;
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var site = serverManager.Sites[siteName];
                    return site?.State;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据监控 URL 反查 IIS 站点名称（匹配 host 与端口）。
        /// 例如 http://localhost:8080 → 匹配 host=localhost/空 且端口 8080 的站点。
        /// </summary>
        public static string InferSiteNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var uri = new Uri(url);
                string host = uri.Host;
                int port = uri.Port;

                using (var serverManager = new ServerManager())
                {
                    foreach (var site in serverManager.Sites)
                    {
                        foreach (var binding in site.Bindings)
                        {
                            if (!binding.Protocol.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
                                continue;

                            int bPort = binding.EndPoint != null ? binding.EndPoint.Port : 0;
                            if (bPort != port && !(bPort == 0 && (port == 80 || port == 443)))
                                continue;

                            // host 匹配：绑定 host 为空（任意）或与 URL host 相同
                            string bHost = binding.Host ?? "";
                            if (string.IsNullOrEmpty(bHost) ||
                                bHost.Equals(host, StringComparison.OrdinalIgnoreCase))
                            {
                                return site.Name;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"根据 URL {url} 反查站点名称失败", ex);
            }
            return null;
        }

        public static bool StartAppPool(string poolName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[poolName];
                    if (pool == null)
                    {
                        Logger.Log($"应用程序池 '{poolName}' 不存在", true);
                        return false;
                    }
                    if (pool.State == ObjectState.Started)
                    {
                        Logger.Log($"应用程序池 '{poolName}' 已在运行中");
                        return true;
                    }
                    pool.Start();
                    serverManager.CommitChanges();
                    Logger.Log($"已启动应用程序池: {poolName}");
                    Thread.Sleep(3000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"启动应用程序池 '{poolName}' 失败", ex);
                return false;
            }
        }

        public static bool StopAppPool(string poolName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[poolName];
                    if (pool == null)
                    {
                        Logger.Log($"应用程序池 '{poolName}' 不存在", true);
                        return false;
                    }
                    if (pool.State == ObjectState.Stopped)
                    {
                        Logger.Log($"应用程序池 '{poolName}' 已停止");
                        return true;
                    }
                    pool.Stop();
                    serverManager.CommitChanges();
                    Logger.Log($"已停止应用程序池: {poolName}");
                    Thread.Sleep(2000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止应用程序池 '{poolName}' 失败", ex);
                return false;
            }
        }

        public static bool StopStartAppPool(string poolName)
        {
            if (!StopAppPool(poolName))
                return false;
            Thread.Sleep(1000);
            return StartAppPool(poolName);
        }

        public static bool RecycleAppPool(string poolName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[poolName];
                    if (pool == null)
                    {
                        Logger.Log($"应用程序池 '{poolName}' 不存在", true);
                        return false;
                    }

                    if (pool.State == ObjectState.Stopped)
                    {
                        pool.Start();
                        serverManager.CommitChanges();
                        Logger.Log($"已启动应用程序池: {poolName}");
                    }
                    else
                    {
                        pool.Recycle();
                        serverManager.CommitChanges();
                        Logger.Log($"已回收应用程序池: {poolName}");
                    }

                    Thread.Sleep(3000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"操作应用程序池 '{poolName}' 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 重启整个 IIS 服务。先尝试 iisreset，失败（如服务未启动 1062）则回退到启动 WAS/W3SVC 服务。
        /// </summary>
        public static bool RestartIIS(int resetTimeoutSeconds = 90, int readyTimeoutSeconds = 30)
        {
            try
            {
                Logger.Log($"正在重启整个 IIS 服务 (超时 {resetTimeoutSeconds}s)...");

                string stdout, stderr;
                int exitCode = RunProcess("iisreset.exe", "", resetTimeoutSeconds, out stdout, out stderr);

                if (exitCode != 0)
                {
                    Logger.LogError($"iisreset.exe 失败，退出码 {exitCode}");
                    if (!string.IsNullOrEmpty(stdout)) Logger.Log($"iisreset 输出: {stdout}");
                    if (!string.IsNullOrEmpty(stderr)) Logger.Log($"iisreset 错误: {stderr}", true);

                    // 1062 等：IIS 服务未启动，iisreset 无法“重启”。直接启动服务。
                    Logger.Log("iisreset 失败，尝试直接启动 IIS 相关服务 (WAS/W3SVC)...");
                    StartIisServices();
                }
                else
                {
                    // iisreset 退出码 0，但服务可能仍未真正起来（日志显示常见）
                    Logger.Log("iisreset 完成，等待 IIS 就绪...");
                }

                // 第一轮就绪等待
                bool ready = WaitForIisReady(readyTimeoutSeconds);
                if (ready)
                {
                    Logger.Log("IIS 重启成功并就绪");
                    return true;
                }

                // iisreset 退出 0 但未就绪：服务可能没真正启动，回退到显式启动服务 + 应用池
                Logger.Log($"IIS 重启完成但 {readyTimeoutSeconds}s 内未就绪，尝试显式启动服务与应用池...");
                StartIisServices();

                // 第二轮就绪等待（给服务启动后额外时间）
                ready = WaitForIisReady(readyTimeoutSeconds);
                Logger.Log(ready ? "IIS 经二次恢复后已就绪" : $"IIS 二次恢复后 {readyTimeoutSeconds}s 内仍未就绪");
                return ready;
            }
            catch (Exception ex)
            {
                Logger.LogError("重启 IIS 失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 启动 IIS 依赖的核心服务：Windows Process Activation Service (WAS) 与
        /// World Wide Web Publishing Service (W3SVC)。W3SVC 依赖 WAS，先启动 WAS。
        /// </summary>
        public static bool StartIisServices()
        {
            // 顺序很重要：WAS 是 W3SVC 的依赖，必须先启动
            bool wasOk = StartWindowsService("WAS");
            bool w3svcOk = StartWindowsService("W3SVC");

            if (wasOk || w3svcOk)
            {
                // 给服务一点时间完成初始化
                Thread.Sleep(2000);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 启动指定 Windows 服务（若已启动则视为成功）。用 sc.exe 而非 ServiceController，
        /// 避免在服务控制管理器繁忙时阻塞。
        /// </summary>
        private static bool StartWindowsService(string serviceName)
        {
            try
            {
                string stdout, stderr;
                int code = RunProcess("sc.exe", $"start {serviceName}", 30, out stdout, out stderr);
                // sc start 对已运行的服务返回 1056(已在运行)，视为成功
                if (code == 0 || code == 1056)
                {
                    Logger.Log($"服务 {serviceName} 已启动 (或已在运行)");
                    return true;
                }
                Logger.LogError($"启动服务 {serviceName} 失败，退出码 {code}");
                if (!string.IsNullOrEmpty(stderr)) Logger.Log($"sc 错误: {stderr}", true);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"启动服务 {serviceName} 异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 运行外部进程并捕获标准输出/错误，超时则终止。
        /// </summary>
        private static int RunProcess(string fileName, string args, int timeoutSeconds, out string stdout, out string stderr)
        {
            stdout = "";
            stderr = "";
            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // 异步读取避免死锁（进程写满管道而 WaitForExit 阻塞）
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            bool exited = process.WaitForExit(timeoutSeconds * 1000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException($"{fileName} 在 {timeoutSeconds}s 内未结束");
            }
            return process.ExitCode;
        }

        /// <summary>
        /// 轮询 ServerManager 直到任意应用池处于 Started 状态，或超时
        /// </summary>
        /// <summary>
        /// 轮询 ServerManager 直到任意应用池处于 Started 状态，或超时。
        /// 轮询过程中若发现应用池处于 Stopped，主动尝试 Start（不干等 IIS 自行启动）。
        /// </summary>
        public static bool WaitForIisReady(int timeoutSeconds)
        {
            int elapsed = 0;
            const int pollMs = 1000;
            bool attemptedStart = false;
            while (elapsed < timeoutSeconds * 1000)
            {
                try
                {
                    using (var serverManager = new ServerManager())
                    {
                        foreach (var pool in serverManager.ApplicationPools)
                        {
                            if (pool.State == ObjectState.Started)
                                return true;

                            // 主动启动停止的应用池（仅尝试一次，避免循环重启崩溃的池）
                            if (!attemptedStart && pool.State == ObjectState.Stopped)
                            {
                                try
                                {
                                    Logger.Log($"就绪检查：应用池 {pool.Name} 处于停止状态，尝试启动...");
                                    pool.Start();
                                    serverManager.CommitChanges();
                                    Logger.Log($"已启动应用池 {pool.Name}");
                                }
                                catch (Exception startEx)
                                {
                                    Logger.LogError($"就绪检查中启动应用池 {pool.Name} 失败", startEx);
                                }
                            }
                        }
                    }
                    attemptedStart = true;
                }
                catch
                {
                }
                Thread.Sleep(pollMs);
                elapsed += pollMs;
            }
            return false;
        }

        /// <summary>
        /// 检查指定应用程序池是否在运行
        /// </summary>
        public static bool IsAppPoolRunning(string poolName)
        {
            try
            {
                using (var serverManager = new ServerManager())
                {
                    var pool = serverManager.ApplicationPools[poolName];
                    return pool != null && pool.State == ObjectState.Started;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 本机 IIS 站点候选项（站点名 + 监控 URL + 运行状态）
    /// </summary>
    public class IisSiteEntry
    {
        public string SiteName { get; set; }
        public string Url { get; set; }
        public bool IsRunning { get; set; }
    }
}
