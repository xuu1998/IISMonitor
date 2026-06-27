using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace IISMonitor
{
    /// <summary>
    /// 重启策略类型
    /// </summary>
    public enum RestartStrategyType
    {
        [Description("仅回收应用程序池")]
        AppPoolOnly,

        [Description("先回收应用池，失败则重启 IIS")]
        AppPoolThenIIS,

        [Description("直接重启 IIS")]
        IISOnly,

        [Description("先停止再启动应用池")]
        StopStartAppPool,

        [Description("先停止再启动应用池，失败则重启 IIS")]
        StopStartAppPoolThenIIS,

        [Description("仅重启站点（不影响其他站点和应用池）")]
        SiteOnly,

        [Description("先重启站点，失败则重启整个 IIS")]
        SiteThenIIS,

        [Description("回收资源并重启应用池，失败则重启 IIS")]
        RecycleThenVerify,

        [Description("先回收应用池，再重启应用池")]
        RecycleThenRestart
    }

    /// <summary>
    /// 监控配置，序列化为 MonitorConfig.xml
    /// </summary>
    [Serializable]
    public class MonitorConfig
    {
        // === 基础监控配置 ===
        public int CheckIntervalSeconds { get; set; } = 5;
        public int HttpTimeoutSeconds { get; set; } = 10;
        public int ConsecutiveFailuresBeforeRestart { get; set; } = 2;
        public bool EnableHttpCheck { get; set; } = true;
        public bool EnableAppPoolCheck { get; set; } = true;
        /// <summary>
        /// 启动程序时是否自动开始监控（无需手动点击“开始监控”按钮）
        /// </summary>
        public bool StartMonitoringOnLaunch { get; set; } = false;
        public RestartStrategyType RestartStrategy { get; set; } = RestartStrategyType.AppPoolOnly;
        public string LogPath { get; set; } = "logs\\";
        public string[] MonitoredSites { get; set; } = new string[0];
        public string[] MonitoredAppPools { get; set; } = new string[0];

        [XmlIgnore]
        public System.Collections.Generic.Dictionary<string, string> SiteExpectedKeywords { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>
        /// SiteExpectedKeywords 的 XML 可序列化投影，避免 Dictionary 无法被 XmlSerializer 序列化导致配置丢失
        /// </summary>
        public StringKeyValuePair[] SiteExpectedKeywordsXml
        {
            get
            {
                if (SiteExpectedKeywords == null || SiteExpectedKeywords.Count == 0)
                    return new StringKeyValuePair[0];
                return SiteExpectedKeywords
                    .Select(kv => new StringKeyValuePair { Key = kv.Key, Value = kv.Value })
                    .ToArray();
            }
            set
            {
                SiteExpectedKeywords = new System.Collections.Generic.Dictionary<string, string>();
                if (value != null)
                {
                    foreach (var item in value)
                    {
                        if (item != null && item.Key != null)
                            SiteExpectedKeywords[item.Key] = item.Value ?? string.Empty;
                    }
                }
            }
        }

        // === 扩展配置：主题与 UI ===
        public bool EnableDarkMode { get; set; } = false;
        public bool AutoMinimizeToTray { get; set; } = false;

        // === 扩展配置：资源监控 ===
        public bool EnableResourceMonitoring { get; set; } = false;
        public int ResourceMonitorIntervalSeconds { get; set; } = 300;
        public ResourceAlertThresholds ResourceAlertThresholds { get; set; } = new ResourceAlertThresholds();

        // === IIS 重启相关 ===
        public int IisResetTimeoutSeconds { get; set; } = 90;
        public int IisReadyTimeoutSeconds { get; set; } = 30;

        // === 扩展配置：告警通知 ===
        public AlertConfig AlertSettings { get; set; } = new AlertConfig();

        // === 内部状态（不序列化） ===
        [XmlIgnore]
        public bool IsCorrupted { get; set; } = false;

        public static MonitorConfig Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                var defaultConfig = new MonitorConfig();
                defaultConfig.Save(configPath);
                return defaultConfig;
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(MonitorConfig));
                using (FileStream fs = new FileStream(configPath, FileMode.Open))
                {
                    return (MonitorConfig)serializer.Deserialize(fs);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"配置文件损坏或无法解析: {configPath}", ex);
                try
                {
                    string backup = $"{configPath}.corrupt.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(configPath, backup, true);
                    Logger.Log($"损坏的配置已备份至: {backup}");
                }
                catch (Exception backupEx)
                {
                    Logger.LogError("备份损坏配置文件失败", backupEx);
                }
                var fallback = new MonitorConfig();
                fallback.IsCorrupted = true;
                return fallback;
            }
        }

        public void Save(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentException("配置文件路径不能为空", nameof(configPath));
            }

            string dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(MonitorConfig));
            using (FileStream fs = new FileStream(configPath, FileMode.Create))
            {
                serializer.Serialize(fs, this);
            }
        }

        /// <summary>
        /// 校验监控项（站点 URL 与应用程序池名称）。供 UI 与服务模式共用。
        /// </summary>
        public static bool ValidateItems(string[] sites, string[] appPools, out string errorMessage)
        {
            errorMessage = null;

            if (sites != null)
            {
                foreach (var url in sites)
                {
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    string u = url.Trim();
                    if (!Uri.TryCreate(u, UriKind.Absolute, out Uri uri) ||
                        (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                         !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                    {
                        errorMessage = $"无效的站点 URL（必须以 http:// 或 https:// 开头）: {u}";
                        return false;
                    }
                }
            }

            if (appPools != null)
            {
                foreach (var name in appPools)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    string p = name.Trim();
                    if (p.Length > 64 || !Regex.IsMatch(p, @"^[a-zA-Z0-9\-_.]+$"))
                    {
                        errorMessage = $"无效的应用程序池名称（仅允许字母、数字、横线、下划线和点，最长 64 字符）: {p}";
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 告警通知配置
    /// </summary>
    [Serializable]
    public class AlertConfig
    {
        // === 通道选择 ===
        public bool EnableSmtp { get; set; } = false;
        public bool EnableWebhook { get; set; } = false;

        // === SMTP 配置 ===
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 25;
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public bool SmtpUseSsl { get; set; } = false;
        public string FromAddress { get; set; } = "";
        public string ToAddress { get; set; } = "";

        // === Webhook 配置 ===
        public string WebhookUrl { get; set; } = "";

        // === 告警行为 ===
        public int AlertCooldownSeconds { get; set; } = 300;
    }

    /// <summary>
    /// 服务器资源告警阈值（百分比 0-100）
    /// </summary>
    [Serializable]
    public class ResourceAlertThresholds
    {
        public double CpuPercent { get; set; } = 90;
        public double MemoryPercent { get; set; } = 90;
        public double DiskPercent { get; set; } = 90;
    }

    /// <summary>
    /// 用于 XML 序列化的键值对
    /// </summary>
    [Serializable]
    public class StringKeyValuePair
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
