using System;
using Newtonsoft.Json;

namespace IISMonitor.Models
{
    /// <summary>
    /// 单次健康检查结果（持久化到 health_results.jsonl）
    /// </summary>
    public class HealthRecord
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("checkType")]
        public string CheckType { get; set; } = "";

        [JsonProperty("target")]
        public string Target { get; set; } = "";

        [JsonProperty("result")]
        public string Result { get; set; } = "";

        [JsonProperty("detail")]
        public string Detail { get; set; } = "";
    }
}
