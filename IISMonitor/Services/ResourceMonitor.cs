using System;
using System.Diagnostics;
using System.Management;

namespace IISMonitor
{
    /// <summary>
    /// 资源快照——CPU、内存、磁盘使用率
    /// </summary>
    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryUsedMb { get; set; }
        public double MemoryPercent { get; set; }
        public double DiskPercent { get; set; }
    }

    /// <summary>
    /// 服务器资源监控（CPU/内存/磁盘）
    /// </summary>
    public class ResourceMonitor
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memAvailableCounter;
        private PerformanceCounter _diskFreeCounter;
        private bool _countersInitialized = false;
        private double _totalMemoryMb = 0;

        public ResourceMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // 第一次读取始终为 0，需要先读取一次
                _cpuCounter.NextValue();

                _memAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");
                _memAvailableCounter.NextValue();

                _diskFreeCounter = new PerformanceCounter("LogicalDisk", "% Free Space", "_Total");
                _diskFreeCounter.NextValue();

                // 获取总物理内存
                try
                {
                    using (var mc = new ManagementClass("Win32_ComputerSystem"))
                    {
                        foreach (var mo in mc.GetInstances())
                        {
                            _totalMemoryMb = Convert.ToDouble(mo["TotalPhysicalMemory"]) / (1024 * 1024);
                            break;
                        }
                    }
                }
                catch
                {
                    _totalMemoryMb = 16384; // 默认 16GB
                }

                _countersInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("初始化 PerformanceCounter 失败", ex);
            }
        }

        /// <summary>
        /// 获取当前资源快照
        /// </summary>
        public ResourceSnapshot GetSnapshot()
        {
            var snapshot = new ResourceSnapshot
            {
                Timestamp = DateTime.Now
            };

            if (_countersInitialized)
            {
                try
                {
                    snapshot.CpuPercent = Math.Round(_cpuCounter.NextValue(), 1);
                    double availableMb = _memAvailableCounter.NextValue();
                    snapshot.MemoryUsedMb = Math.Round(_totalMemoryMb - availableMb, 0);
                    snapshot.MemoryPercent = _totalMemoryMb > 0
                        ? Math.Round((_totalMemoryMb - availableMb) / _totalMemoryMb * 100, 1)
                        : 0;
                    snapshot.DiskPercent = Math.Round(100 - _diskFreeCounter.NextValue(), 1);
                }
                catch (Exception ex)
                {
                    Logger.LogError("获取资源快照失败", ex);
                }
            }

            return snapshot;
        }
    }
}
