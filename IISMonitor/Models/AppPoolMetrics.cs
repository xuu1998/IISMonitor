using System;
using System.Collections.Generic;

namespace IISMonitor.Models
{
    /// <summary>
    /// 单个应用池的性能指标快照
    /// </summary>
    public class AppPoolMetrics
    {
        public string PoolName { get; set; }
        public bool IsRunning { get; set; }
        public List<int> WorkerProcessPids { get; set; } = new List<int>();
        public int WorkerProcessCount { get { return WorkerProcessPids.Count; } }
        /// <summary>工作进程总内存(MB)</summary>
        public double MemoryMb { get; set; }
        /// <summary>当前活动请求数</summary>
        public long ActiveRequests { get; set; }
        /// <summary>每秒请求数</summary>
        public double RequestsPerSec { get; set; }
        /// <summary>请求队列长度</summary>
        public long QueueLength { get; set; }
        /// <summary>采集失败时的错误提示</summary>
        public string Error { get; set; }
    }
}
