using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using IISMonitor.Models;

namespace IISMonitor
{
    /// <summary>
    /// 健康检查报告导出器（CSV / HTML），采用流式单遍扫描避免大文件 OOM
    /// </summary>
    public static class ReportExporter
    {
        private const int MaxDetailRows = 500;

        /// <summary>
        /// 将 JSONL 数据导出为 CSV 文件（按时间升序流式写入）
        /// </summary>
        public static void ExportToCsv(string jsonlPath, string csvPath, DateTime? from = null, DateTime? to = null)
        {
            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                // 写入 CSV 表头
                writer.WriteLine("时间戳,检查类型,目标,结果,详情");

                foreach (var record in ReadRecords(jsonlPath, from, to))
                {
                    // 转义 CSV 中的逗号和引号
                    string timestamp = EscapeCsv(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                    string checkType = EscapeCsv(record.CheckType);
                    string target = EscapeCsv(record.Target);
                    string result = EscapeCsv(record.Result);
                    string detail = EscapeCsv(record.Detail);

                    writer.WriteLine($"{timestamp},{checkType},{target},{result},{detail}");
                }
            }
        }

        /// <summary>
        /// 将 JSONL 数据导出为 HTML 报表（单遍扫描：统计 + 最近 N 条明细）
        /// </summary>
        public static void ExportToHtml(string jsonlPath, string htmlPath, DateTime? from = null, DateTime? to = null)
        {
            int totalChecks = 0;
            int successCount = 0;
            var perTarget = new Dictionary<string, TargetStat>();
            // 滚动保留最近 MaxDetailRows 条（文件按时间升序追加，末尾即最新）
            var recent = new LinkedList<HealthRecord>();

            foreach (var record in ReadRecords(jsonlPath, from, to))
            {
                totalChecks++;
                bool ok = record.Result == "成功";
                if (ok) successCount++;

                if (!perTarget.TryGetValue(record.Target, out var stat))
                    stat = new TargetStat();
                stat.Total++;
                if (ok) stat.Success++;
                perTarget[record.Target] = stat;

                recent.AddLast(record);
                while (recent.Count > MaxDetailRows)
                    recent.RemoveFirst();
            }

            int failCount = totalChecks - successCount;
            double successRate = totalChecks > 0 ? (double)successCount / totalChecks * 100 : 0;

            using (var writer = new StreamWriter(htmlPath, false, Encoding.UTF8))
            {
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html lang='zh-CN'>");
                writer.WriteLine("<head>");
                writer.WriteLine("<meta charset='UTF-8'>");
                writer.WriteLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
                writer.WriteLine("<title>IIS 监控报告</title>");
                writer.WriteLine("<style>");
                writer.WriteLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
                writer.WriteLine("h1 { color: #333; }");
                writer.WriteLine(".summary { background: #fff; padding: 15px; border-radius: 5px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
                writer.WriteLine(".summary span { margin-right: 20px; }");
                writer.WriteLine(".ok { color: green; }");
                writer.WriteLine(".fail { color: red; }");
                writer.WriteLine("table { width: 100%; border-collapse: collapse; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
                writer.WriteLine("th { background: #4CAF50; color: white; padding: 10px; text-align: left; }");
                writer.WriteLine("td { padding: 8px 10px; border-bottom: 1px solid #ddd; }");
                writer.WriteLine("tr:hover { background: #f1f1f1; }");
                writer.WriteLine(".row-ok { border-left: 4px solid #4CAF50; }");
                writer.WriteLine(".row-fail { border-left: 4px solid #f44336; }");
                writer.WriteLine("</style>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
                writer.WriteLine($"<h1>IIS 监控报告</h1>");
                writer.WriteLine($"<p>生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

                // 统计概览
                writer.WriteLine("<div class='summary'>");
                writer.WriteLine($"<span>总检查次数: <strong>{totalChecks}</strong></span>");
                writer.WriteLine($"<span class='ok'>正常: <strong>{successCount}</strong></span>");
                writer.WriteLine($"<span class='fail'>异常: <strong>{failCount}</strong></span>");
                writer.WriteLine($"<span>可用率: <strong>{successRate:F1}%</strong></span>");
                writer.WriteLine("</div>");

                // 各目标统计
                writer.WriteLine("<h2>各目标可用率</h2>");
                writer.WriteLine("<table>");
                writer.WriteLine("<tr><th>目标</th><th>总检查</th><th>正常</th><th>异常</th><th>可用率</th></tr>");
                foreach (var kv in perTarget)
                {
                    var s = kv.Value;
                    double gRate = s.Total > 0 ? (double)s.Success / s.Total * 100 : 0;
                    writer.WriteLine($"<tr><td>{EscapeHtml(kv.Key)}</td><td>{s.Total}</td><td class='ok'>{s.Success}</td><td class='fail'>{s.Total - s.Success}</td><td>{gRate:F1}%</td></tr>");
                }
                writer.WriteLine("</table>");

                // 详细记录（最近 MaxDetailRows 条，按时间升序）
                writer.WriteLine("<h2>详细检查记录</h2>");
                writer.WriteLine("<table>");
                writer.WriteLine("<tr><th>时间</th><th>类型</th><th>目标</th><th>结果</th><th>详情</th></tr>");

                foreach (var record in recent)
                {
                    string rowClass = record.Result == "成功" ? "row-ok" : "row-fail";
                    string resultDisplay = record.Result == "成功" ? "✓ 正常" : "✗ 异常";
                    string resultClass = record.Result == "成功" ? "ok" : "fail";
                    writer.WriteLine($"<tr class='{rowClass}'>");
                    writer.WriteLine($"<td>{EscapeHtml(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))}</td>");
                    writer.WriteLine($"<td>{EscapeHtml(record.CheckType)}</td>");
                    writer.WriteLine($"<td>{EscapeHtml(record.Target)}</td>");
                    writer.WriteLine($"<td class='{resultClass}'><strong>{resultDisplay}</strong></td>");
                    writer.WriteLine($"<td>{EscapeHtml(record.Detail)}</td>");
                    writer.WriteLine("</tr>");
                }

                if (totalChecks > MaxDetailRows)
                {
                    writer.WriteLine($"<tr><td colspan='5' style='text-align:center;color:#888;'>仅显示最近 {MaxDetailRows} 条记录（共 {totalChecks} 条）</td></tr>");
                }

                writer.WriteLine("</table>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }

        /// <summary>
        /// 流式读取并过滤 JSONL 记录（惰性枚举，不在内存中堆积全部记录）
        /// </summary>
        private static IEnumerable<HealthRecord> ReadRecords(string filePath, DateTime? from, DateTime? to)
        {
            if (!File.Exists(filePath))
                yield break;

            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                HealthRecord record;
                try
                {
                    record = JsonConvert.DeserializeObject<HealthRecord>(line);
                }
                catch
                {
                    continue;
                }

                if (record == null) continue;

                // 时间范围过滤
                if (from.HasValue && record.Timestamp < from.Value) continue;
                if (to.HasValue && record.Timestamp > to.Value) continue;

                yield return record;
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private class TargetStat
        {
            public int Total;
            public int Success;
        }
    }
}
