using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using IISMonitor;
using IISMonitor.Models;

namespace IISMonitor.Tests
{
    [TestFixture]
    public class ReportExporterTests
    {
        private string _jsonlPath;
        private string _outputPath;

        [SetUp]
        public void Setup()
        {
            _jsonlPath = Path.Combine(Path.GetTempPath(), $"IISMonitor_test_{Guid.NewGuid():N}.jsonl");
            _outputPath = Path.Combine(Path.GetTempPath(), $"IISMonitor_export_{Guid.NewGuid():N}.csv");
        }

        [TearDown]
        public void Teardown()
        {
            try { if (File.Exists(_jsonlPath)) File.Delete(_jsonlPath); } catch { }
            try { if (File.Exists(_outputPath)) File.Delete(_outputPath); } catch { }
        }

        [Test]
        public void ExportToCsv_ValidJsonl_WritesHeaderAndRows()
        {
            File.WriteAllText(_jsonlPath,
                "{\"timestamp\":\"2026-06-04T10:00:00\",\"checkType\":\"SiteHttp\",\"target\":\"http://a\",\"result\":\"success\",\"detail\":\"\"}\n" +
                "{\"timestamp\":\"2026-06-04T10:01:00\",\"checkType\":\"SiteHttp\",\"target\":\"http://a\",\"result\":\"fail\",\"detail\":\"timeout\"}\n",
                Encoding.UTF8);

            ReportExporter.ExportToCsv(_jsonlPath, _outputPath);

            Assert.IsTrue(File.Exists(_outputPath));
            var lines = File.ReadAllLines(_outputPath, Encoding.UTF8);
            Assert.AreEqual(3, lines.Length, "header + 2 data rows");
            Assert.IsTrue(lines[0].Contains("Timestamp") || lines[0].Contains("\u65F6\u95F4\u6233"));
            Assert.IsTrue(lines[1].Contains("http://a"));
        }

        [Test]
        public void ExportToCsv_FilterByDateRange_RespectsFromAndTo()
        {
            File.WriteAllText(_jsonlPath,
                "{\"timestamp\":\"2026-06-01T10:00:00\",\"checkType\":\"Site\",\"target\":\"old\",\"result\":\"ok\",\"detail\":\"\"}\n" +
                "{\"timestamp\":\"2026-06-04T10:00:00\",\"checkType\":\"Site\",\"target\":\"in\",\"result\":\"ok\",\"detail\":\"\"}\n" +
                "{\"timestamp\":\"2026-06-10T10:00:00\",\"checkType\":\"Site\",\"target\":\"future\",\"result\":\"ok\",\"detail\":\"\"}\n",
                Encoding.UTF8);

            ReportExporter.ExportToCsv(_jsonlPath, _outputPath,
                from: new DateTime(2026, 6, 2), to: new DateTime(2026, 6, 5));

            var lines = File.ReadAllLines(_outputPath, Encoding.UTF8);
            Assert.AreEqual(2, lines.Length, "header + 1 data row (only 'in')");
            Assert.IsTrue(lines[1].Contains("in"));
        }

        [Test]
        public void ExportToHtml_MissingFile_ProducesEmptyReport()
        {
            ReportExporter.ExportToHtml(_jsonlPath, _outputPath);
            Assert.IsTrue(File.Exists(_outputPath));
            var content = File.ReadAllText(_outputPath, Encoding.UTF8);
            Assert.IsTrue(content.Contains("html"));
            Assert.IsTrue(content.Contains("0"));
        }

        [Test]
        public void HealthRecord_Roundtrip_ThroughJson()
        {
            var r = new HealthRecord
            {
                Timestamp = new DateTime(2026, 6, 4, 10, 30, 0),
                CheckType = "AppPool",
                Target = "DefaultAppPool",
                Result = "ok",
                Detail = "running"
            };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(r);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<HealthRecord>(json);

            Assert.AreEqual(r.Timestamp, deserialized.Timestamp);
            Assert.AreEqual("AppPool", deserialized.CheckType);
            Assert.AreEqual("DefaultAppPool", deserialized.Target);
        }
    }
}
