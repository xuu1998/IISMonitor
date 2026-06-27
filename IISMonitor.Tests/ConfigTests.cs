using System;
using System.IO;
using NUnit.Framework;
using IISMonitor;
using IISMonitor.Infrastructure;

namespace IISMonitor.Tests
{
    [TestFixture]
    public class ConfigTests
    {
        private string _tempPath;

        [SetUp]
        public void Setup()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"IISMonitor_test_{Guid.NewGuid():N}.xml");
        }

        [TearDown]
        public void Teardown()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
            var dir = Path.GetDirectoryName(_tempPath);
            if (dir != null && Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "IISMonitor_test_*.xml*"))
                    try { File.Delete(f); } catch { }
            }
        }

        [Test]
        public void Load_FileNotExists_CreatesDefault()
        {
            var cfg = MonitorConfig.Load(_tempPath);
            Assert.IsNotNull(cfg);
            Assert.IsFalse(cfg.IsCorrupted);
            Assert.AreEqual(5, cfg.CheckIntervalSeconds);
            Assert.AreEqual(2, cfg.ConsecutiveFailuresBeforeRestart);
            Assert.IsTrue(File.Exists(_tempPath));
        }

        [Test]
        public void SaveAndLoad_Roundtrip_PreservesValues()
        {
            var original = new MonitorConfig
            {
                CheckIntervalSeconds = 120,
                ConsecutiveFailuresBeforeRestart = 5,
                HttpTimeoutSeconds = 30,
                EnableHttpCheck = false,
                EnableAppPoolCheck = true,
                RestartStrategy = RestartStrategyType.AppPoolThenIIS,
                MonitoredSites = new[] { "http://a", "http://b" },
                MonitoredAppPools = new[] { "Pool1" },
                EnableDarkMode = true,
                IisResetTimeoutSeconds = 120
            };
            original.Save(_tempPath);
            var loaded = MonitorConfig.Load(_tempPath);

            Assert.AreEqual(120, loaded.CheckIntervalSeconds);
            Assert.AreEqual(5, loaded.ConsecutiveFailuresBeforeRestart);
            Assert.AreEqual(30, loaded.HttpTimeoutSeconds);
            Assert.IsFalse(loaded.EnableHttpCheck);
            Assert.IsTrue(loaded.EnableAppPoolCheck);
            Assert.AreEqual(RestartStrategyType.AppPoolThenIIS, loaded.RestartStrategy);
            Assert.AreEqual(2, loaded.MonitoredSites.Length);
            Assert.AreEqual("http://a", loaded.MonitoredSites[0]);
            Assert.AreEqual("Pool1", loaded.MonitoredAppPools[0]);
            Assert.IsTrue(loaded.EnableDarkMode);
            Assert.AreEqual(120, loaded.IisResetTimeoutSeconds);
        }

        [Test]
        public void Load_CorruptedFile_CreatesBackupAndReturnsDefault()
        {
            File.WriteAllText(_tempPath, "<<<not valid xml>>>");
            var cfg = MonitorConfig.Load(_tempPath);

            Assert.IsTrue(cfg.IsCorrupted);
            Assert.IsNotNull(cfg);

            var dir = Path.GetDirectoryName(_tempPath);
            var backups = Directory.GetFiles(dir, Path.GetFileName(_tempPath) + ".corrupt.*");
            Assert.AreEqual(1, backups.Length, "should create backup file");
            File.Delete(backups[0]);
        }

        [Test]
        public void ResourceAlertThresholds_DefaultValues()
        {
            var t = new ResourceAlertThresholds();
            Assert.AreEqual(90.0, t.CpuPercent);
            Assert.AreEqual(90.0, t.MemoryPercent);
            Assert.AreEqual(90.0, t.DiskPercent);
        }

        [Test]
        public void RestartStrategy_AllEnumValues_AreDefined()
        {
            Assert.AreEqual(5, Enum.GetValues(typeof(RestartStrategyType)).Length);
        }

        [Test]
        public void RestartStrategy_AllEnumValues_HaveChineseDescription()
        {
            foreach (RestartStrategyType value in Enum.GetValues(typeof(RestartStrategyType)))
            {
                var desc = value.GetDescription();
                Assert.IsNotNull(desc, $"{value} 应有中文描述");
                Assert.IsFalse(string.IsNullOrWhiteSpace(desc), $"{value} 描述不能为空");
            }
        }

        [Test]
        public void GetDescription_AppPoolOnly_ReturnsCorrectChinese()
        {
            Assert.AreEqual("仅回收应用程序池",
                RestartStrategyType.AppPoolOnly.GetDescription());
            Assert.AreEqual("先回收应用池，失败则重启 IIS",
                RestartStrategyType.AppPoolThenIIS.GetDescription());
            Assert.AreEqual("直接重启 IIS",
                RestartStrategyType.IISOnly.GetDescription());
            Assert.AreEqual("先停止再启动应用池",
                RestartStrategyType.StopStartAppPool.GetDescription());
            Assert.AreEqual("先停止再启动应用池，失败则重启 IIS",
                RestartStrategyType.StopStartAppPoolThenIIS.GetDescription());
        }

        [Test]
        public void TryParseByDescription_ValidChinese_ReturnsCorrectEnum()
        {
            Assert.IsTrue(EnumExtensions.TryParseByDescription("仅回收应用程序池", out RestartStrategyType s1));
            Assert.AreEqual(RestartStrategyType.AppPoolOnly, s1);

            Assert.IsTrue(EnumExtensions.TryParseByDescription("直接重启 IIS", out RestartStrategyType s2));
            Assert.AreEqual(RestartStrategyType.IISOnly, s2);
        }

        [Test]
        public void TryParseByDescription_InvalidString_ReturnsFalse()
        {
            Assert.IsFalse(EnumExtensions.TryParseByDescription("不存在的策略", out RestartStrategyType result));
        }

        [Test]
        public void SaveAndLoad_Roundtrip_PreservesStrategyByEnumName()
        {
            // 验证 XML 序列化存的是枚举名（不是中文描述），保持向后兼容
            var original = new MonitorConfig
            {
                RestartStrategy = RestartStrategyType.StopStartAppPoolThenIIS
            };
            original.Save(_tempPath);
            string xml = File.ReadAllText(_tempPath);
            Assert.IsTrue(xml.Contains("StopStartAppPoolThenIIS"),
                "配置文件应存枚举名而非中文描述");
        }

        [Test]
        public void SaveAndLoad_Roundtrip_PreservesSiteExpectedKeywords()
        {
            var original = new MonitorConfig();
            original.SiteExpectedKeywords["http://a"] = "welcome";
            original.SiteExpectedKeywords["http://b"] = "ok";
            original.Save(_tempPath);

            var loaded = MonitorConfig.Load(_tempPath);
            Assert.IsNotNull(loaded.SiteExpectedKeywords);
            Assert.AreEqual(2, loaded.SiteExpectedKeywords.Count);
            Assert.AreEqual("welcome", loaded.SiteExpectedKeywords["http://a"]);
            Assert.AreEqual("ok", loaded.SiteExpectedKeywords["http://b"]);
        }

        [Test]
        public void ValidateItems_InvalidUrl_ReturnsFalse()
        {
            bool ok = MonitorConfig.ValidateItems(new[] { "ftp://x" }, new[] { "Pool1" }, out string err);
            Assert.IsFalse(ok);
            Assert.IsFalse(string.IsNullOrEmpty(err));
        }

        [Test]
        public void ValidateItems_InvalidPoolName_ReturnsFalse()
        {
            bool ok = MonitorConfig.ValidateItems(new[] { "http://x" }, new[] { "bad pool!" }, out string err);
            Assert.IsFalse(ok);
            Assert.IsFalse(string.IsNullOrEmpty(err));
        }

        [Test]
        public void ValidateItems_ValidItems_ReturnsTrue()
        {
            bool ok = MonitorConfig.ValidateItems(new[] { "http://a", "https://b" }, new[] { "Pool-1", "Pool_2" }, out string err);
            Assert.IsTrue(ok);
            Assert.IsNull(err);
        }
    }
}
