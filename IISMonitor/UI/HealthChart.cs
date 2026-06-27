using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace IISMonitor.UI
{
    /// <summary>
    /// 实时健康折线图：X 轴为时间，Y 轴为 0/1（健康/故障），每个监控目标一条折线
    /// </summary>
    public class HealthChart : IDisposable
    {
        public Chart Chart { get; }
        private readonly ConcurrentDictionary<string, Queue<DateTimePoint>> _data = new ConcurrentDictionary<string, Queue<DateTimePoint>>();
        private readonly int _maxPoints = 200;
        private readonly Timer _refreshTimer;
        private readonly object _syncLock = new object();
        private static readonly string[] TargetColors = {
            "#4CAF50", "#2196F3", "#FF9800", "#F44336", "#9C27B0",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#8BC34A"
        };

        public HealthChart()
        {
            Chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            var area = new ChartArea("Main")
            {
                AxisX = {
                    LabelStyle = { Format = "HH:mm:ss", Font = new Font("Segoe UI", 8f) },
                    Interval = 1,
                    IntervalType = DateTimeIntervalType.Auto,
                    MajorGrid = { LineColor = Color.FromArgb(40, 40, 40) }
                },
                AxisY = {
                    Minimum = 0,
                    Maximum = 1.1,
                    Interval = 1,
                    LabelStyle = { Font = new Font("Segoe UI", 8f) },
                    MajorGrid = { LineColor = Color.FromArgb(40, 40, 40) }
                },
                BackColor = Color.Transparent
            };
            Chart.ChartAreas.Add(area);

            var legend = new Legend
            {
                Docking = Docking.Bottom,
                Font = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent
            };
            Chart.Legends.Add(legend);

            _refreshTimer = new Timer { Interval = 2000 };
            _refreshTimer.Tick += (s, e) => RefreshDisplay();
            _refreshTimer.Start();
        }

        public void RecordResult(string target, bool healthy)
        {
            RecordValue(target, healthy ? 1.0 : 0.0);
        }

        public void RecordResource(string metricName, double value01)
        {
            if (string.IsNullOrEmpty(metricName)) return;
            // 资源类指标保留连续值（0~1），便于观察接近阈值的趋势，而非压缩为 0/1
            double clamped = Math.Max(0, Math.Min(1, value01));
            RecordValue($"[资源] {metricName}", clamped);
        }

        private void RecordValue(string target, double value)
        {
            if (string.IsNullOrEmpty(target)) return;

            var point = new DateTimePoint(DateTime.Now, value);

            _data.AddOrUpdate(target,
                _ => { var q = new Queue<DateTimePoint>(); q.Enqueue(point); return q; },
                (_, q) =>
                {
                    lock (_syncLock)
                    {
                        q.Enqueue(point);
                        while (q.Count > _maxPoints) q.Dequeue();
                    }
                    return q;
                });
        }

        private void RefreshDisplay()
        {
            if (Chart.IsDisposed) return;

            var targets = _data.Keys.ToList();
            var existing = Chart.Series.Select(s => s.Name).ToList();

            foreach (var target in targets)
            {
                Series series;
                if (!existing.Contains(target))
                {
                    int colorIdx = Math.Abs(target.GetHashCode()) % TargetColors.Length;
                    series = new Series(target)
                    {
                        ChartType = SeriesChartType.Line,
                        BorderWidth = 2,
                        Color = ColorTranslator.FromHtml(TargetColors[colorIdx]),
                        BorderDashStyle = ChartDashStyle.Solid,
                        MarkerStyle = MarkerStyle.None,
                        XValueType = ChartValueType.DateTime,
                        YValueType = ChartValueType.Double
                    };
                    Chart.Series.Add(series);
                }
                else
                {
                    series = Chart.Series[target];
                }

                Queue<DateTimePoint> points;
                if (!_data.TryGetValue(target, out points)) continue;

                lock (_syncLock)
                {
                    series.Points.Clear();
                    foreach (var p in points)
                    {
                        series.Points.AddXY(p.Time.ToOADate(), p.Value);
                    }
                }
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            Chart?.Dispose();
        }

        private class DateTimePoint
        {
            public DateTime Time;
            public double Value;
            public DateTimePoint(DateTime t, double v) { Time = t; Value = v; }
        }
    }
}
