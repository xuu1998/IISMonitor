using System;
using System.Text.Json.Serialization;

namespace IISMonitor.Models
{
    /// <summary>
    /// 单次健康检查结果（持久化到 health_results.jsonl）
    /// </summary>
    public class HealthRecord
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("checkType")]
        public string CheckType { get; set; } = "";

        [JsonPropertyName("target")]
        public string Target { get; set; } = "";

        [JsonPropertyName("result")]
        public string Result { get; set; } = "";

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";
    }
}
