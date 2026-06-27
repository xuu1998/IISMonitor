using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IISMonitor
{
    /// <summary>
    /// 异步日志记录器，使用 BlockingCollection + 后台线程避免 I/O 阻塞
    /// </summary>
    public static class Logger
    {
        private static string logDirectory = "";
        private static string logFilePath = "";
        private static readonly object lockObj = new object();
        private const long MaxLogFileSize = 10 * 1024 * 1024; // 10MB
        private const int MaxLogDays = 30;

        // 异步日志队列
        private static BlockingCollection<string> _logQueue = new BlockingCollection<string>(new ConcurrentQueue<string>(), 1024);
        private static Thread _logWriterThread;
        private static bool _initialized = false;
        private static long _droppedCount = 0;
        private static long _lastReportedDropped = 0;
        private static readonly object _dropLock = new object();

        /// <summary>
        /// 初始化日志记录器，启动后台���入线程
        /// </summary>
        public static void Initialize(string logDirectoryPath)
        {
            logDirectory = logDirectoryPath;
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            // 启动时清理过期日志
            CleanupOldLogs();

            logFilePath = GetLogFilePath(0);

            if (!_initialized)
            {
                _initialized = true;
                _logWriterThread = new Thread(LogWriterLoop)
                {
                    IsBackground = true,
                    Name = "LogWriter"
                };
                _logWriterThread.Start();
            }
        }

        /// <summary>
        /// 后台日志写入循环
        /// </summary>
        private static void LogWriterLoop()
        {
            try
            {
                foreach (var line in _logQueue.GetConsumingEnumerable())
                {
                    if (line == null) // 终止信号
                        break;

                    lock (lockObj)
                    {
                        try
                        {
                            // 检查文件大小，超过 10MB 时自动轮转
                            if (File.Exists(logFilePath))
                            {
                                FileInfo fi = new FileInfo(logFilePath);
                                if (fi.Length >= MaxLogFileSize)
                                {
                                    int index = 1;
                                    while (File.Exists(GetLogFilePath(index)))
                                        index++;
                                    logFilePath = GetLogFilePath(index);
                                }
                            }

                            File.AppendAllText(logFilePath, line + Environment.NewLine, Encoding.UTF8);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 关闭日志后台线程（优雅关闭）
        /// </summary>
        public static void Shutdown()
        {
            if (_initialized)
            {
                _initialized = false;
                try
                {
                    _logQueue.CompleteAdding();
                    if (_logWriterThread != null && _logWriterThread.IsAlive)
                    {
                        _logWriterThread.Join(2000);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 清理超过 30 天的旧日志
        /// </summary>
        private static void CleanupOldLogs()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-MaxLogDays);
                var logFiles = Directory.GetFiles(logDirectory, "IISMonitor_*.log");
                foreach (var file in logFiles)
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        private static string GetLogFilePath(int index)
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            if (index == 0)
                return Path.Combine(logDirectory, $"IISMonitor_{date}.log");
            else
                return Path.Combine(logDirectory, $"IISMonitor_{date}_{index}.log");
        }

        /// <summary>
        /// 写入日志（异步）
        /// </summary>
        public static void Log(string message, bool isError = false)
        {
            if (string.IsNullOrEmpty(logFilePath))
                return;

            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{(isError ? "ERROR" : "INFO")}] {message}";

            // 入队异步写入
            if (_initialized)
            {
                if (!_logQueue.TryAdd(logLine))
                {
                    // 队列满，记录丢弃计数并周期性输出告警
                    long total = System.Threading.Interlocked.Increment(ref _droppedCount);
                    lock (_dropLock)
                    {
                        if (total - _lastReportedDropped >= 100)
                        {
                            _lastReportedDropped = total;
                            // 直接写一条应急记录（同步，绕过队列）
                            try
                            {
                                lock (lockObj)
                                {
                                    File.AppendAllText(logFilePath,
                                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [WARN] 日志队列溢出，已累计丢弃 {total} 条日志" + Environment.NewLine,
                                        Encoding.UTF8);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 写入错误日志
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            string fullMsg = message;
            if (ex != null)
                fullMsg += $" - {ex.Message}";
            Log(fullMsg, true);
        }
    }
}
