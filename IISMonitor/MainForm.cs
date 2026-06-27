using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using IISMonitor.Infrastructure;
using Timer = System.Windows.Forms.Timer;

namespace IISMonitor
{
    public partial class MainForm : Form
    {
        private MonitorService monitor;
        private MonitorConfig config;
        private int _hasAnyFailure = 0;
        private IISMonitor.UI.HealthChart _healthChart;

        public MainForm()
        {
            InitializeComponent();
            InitializeTrayIcon();
            LoadConfig();

            monitor = new MonitorService();
            monitor.OnStatusUpdate += UpdateStatus;
            monitor.OnCheckResult += UpdateCheckResult;
            monitor.OnResourceSnapshot += UpdateResourceChart;

            InitHealthChart();

            uiTimer = new Timer();
            uiTimer.Interval = 2000;
            uiTimer.Tick += (s, e) => RefreshStatus();
            uiTimer.Start();

            this.Load += MainForm_FirstShown;
        }

        /// <summary>
        /// 窗体首次显示后，若启用“启动时自动监控”且配置了监控目标，则自动开始监控。
        /// 放在 Load 事件而非构造函数，确保 UI 已就绪（按钮状态、主题等）。
        /// </summary>
        private bool _autoStartAttempted = false;
        private void MainForm_FirstShown(object sender, EventArgs e)
        {
            if (_autoStartAttempted) return;
            _autoStartAttempted = true;

            if (!config.StartMonitoringOnLaunch) return;

            // 没配置监控目标则不自动启动（避免弹一堆校验错误）
            bool hasSites = config.MonitoredSites != null && config.MonitoredSites.Length > 0;
            bool hasPools = config.MonitoredAppPools != null && config.MonitoredAppPools.Length > 0;
            if (!hasSites && !hasPools)
            {
                UpdateStatus("已启用自动监控，但未配置监控目标，请先添加");
                return;
            }

            // 校验通过则自动启动（复用启动按钮逻辑，但出错用状态栏提示而非弹窗，避免启动时打扰）
            if (!ValidateMonitoredItems(out string errMsg))
            {
                UpdateStatus($"自动监控启动失败: {errMsg}");
                return;
            }

            monitor.Start(config);
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            lblStatus.Text = "状态: 监控运行中（自动启动）";

            if (config.AutoMinimizeToTray)
            {
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
        }

        private void InitHealthChart()
        {
            _healthChart = new IISMonitor.UI.HealthChart();
            var chartPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Padding = new Padding(0)
            };
            _healthChart.Chart.Dock = DockStyle.Fill;
            chartPanel.Controls.Add(_healthChart.Chart);
            this.Controls.Add(chartPanel);
            chartPanel.BringToFront();
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示窗口", null, OnShowFromTray);
            trayMenu.Items.Add("退出", null, OnExitFromTray);

            notifyIcon = new NotifyIcon
            {
                Text = "IIS 监控看板",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            notifyIcon.DoubleClick += OnShowFromTray;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                Logger.Shutdown();
                base.OnFormClosing(e);
            }
        }

        private void OnShowFromTray(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void OnExitFromTray(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            monitor?.Stop();
            Logger.Shutdown();
            Application.Exit();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MonitorConfig.xml");
            config = MonitorConfig.Load(configPath);

            // 配置损坏时弹窗提醒
            if (config.IsCorrupted)
            {
                MessageBox.Show("配置文件已损坏，已使用默认配置启动。请重新配置并保存。",
                    "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            txtCheckInterval.Text = config.CheckIntervalSeconds.ToString();
            numFailThreshold.Value = config.ConsecutiveFailuresBeforeRestart;
            // 从枚举动态填充重启策略下拉框（新策略只需加 Description 特性即可）
            cmbRestartStrategy.Items.Clear();
            foreach (RestartStrategyType value in Enum.GetValues(typeof(RestartStrategyType)))
            {
                cmbRestartStrategy.Items.Add(value.GetDescription());
            }
            cmbRestartStrategy.SelectedItem = config.RestartStrategy.GetDescription();
            chkHttpCheck.Checked = config.EnableHttpCheck;
            chkAppPoolCheck.Checked = config.EnableAppPoolCheck;
            chkDarkMode.Checked = config.EnableDarkMode;
            chkAutoMinimize.Checked = config.AutoMinimizeToTray;
            chkResourceMonitor.Checked = config.EnableResourceMonitoring;
            chkAutoStart.Checked = config.StartMonitoringOnLaunch;

            ApplyTheme(config.EnableDarkMode);
            LoadMonitoredItems();
        }

        private void LoadMonitoredItems()
        {
            dgvSites.DataSource = null;
            dgvSites.Columns.Clear();
            dgvSites.Rows.Clear();
            dgvSites.Columns.Add("Url", "监控站点 URL");
            dgvSites.AllowUserToAddRows = true;
            if (config.MonitoredSites != null)
            {
                foreach (var url in config.MonitoredSites)
                    dgvSites.Rows.Add(url);
            }

            dgvAppPools.DataSource = null;
            dgvAppPools.Columns.Clear();
            dgvAppPools.Rows.Clear();
            dgvAppPools.Columns.Add("Name", "应用程序池");
            dgvAppPools.AllowUserToAddRows = true;
            if (config.MonitoredAppPools != null)
            {
                foreach (var pool in config.MonitoredAppPools)
                    dgvAppPools.Rows.Add(pool);
            }
        }

        private void SaveConfigFromUI()
        {
            config.CheckIntervalSeconds = int.Parse(txtCheckInterval.Text);
            config.ConsecutiveFailuresBeforeRestart = (int)numFailThreshold.Value;
            if (EnumExtensions.TryParseByDescription(cmbRestartStrategy.SelectedItem?.ToString() ?? string.Empty, out RestartStrategyType strategy))
                config.RestartStrategy = strategy;
            else
                config.RestartStrategy = RestartStrategyType.AppPoolOnly;
            config.EnableHttpCheck = chkHttpCheck.Checked;
            config.EnableAppPoolCheck = chkAppPoolCheck.Checked;
            config.EnableDarkMode = chkDarkMode.Checked;
            config.AutoMinimizeToTray = chkAutoMinimize.Checked;
            config.EnableResourceMonitoring = chkResourceMonitor.Checked;
            config.StartMonitoringOnLaunch = chkAutoStart.Checked;

            var siteList = new System.Collections.Generic.List<string>();
            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrWhiteSpace(row.Cells[0].Value.ToString()))
                    siteList.Add(row.Cells[0].Value.ToString().Trim());
            }
            config.MonitoredSites = siteList.ToArray();

            var poolList = new System.Collections.Generic.List<string>();
            foreach (DataGridViewRow row in dgvAppPools.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrWhiteSpace(row.Cells[0].Value.ToString()))
                    poolList.Add(row.Cells[0].Value.ToString().Trim());
            }
            config.MonitoredAppPools = poolList.ToArray();

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MonitorConfig.xml");
            config.Save(configPath);
        }

        /// <summary>
        /// 校验输入的站点 URL 和应用程序池名称是否合法（委托给 MonitorConfig 共用校验逻辑）
        /// </summary>
        private bool ValidateMonitoredItems(out string errorMessage)
        {
            var siteList = new System.Collections.Generic.List<string>();
            foreach (DataGridViewRow row in dgvSites.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrWhiteSpace(row.Cells[0].Value.ToString()))
                    siteList.Add(row.Cells[0].Value.ToString().Trim());
            }

            var poolList = new System.Collections.Generic.List<string>();
            foreach (DataGridViewRow row in dgvAppPools.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrWhiteSpace(row.Cells[0].Value.ToString()))
                    poolList.Add(row.Cells[0].Value.ToString().Trim());
            }

            return MonitorConfig.ValidateItems(siteList.ToArray(), poolList.ToArray(), out errorMessage);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            SaveConfigFromUI();

            if (!ValidateMonitoredItems(out string errorMsg))
            {
                MessageBox.Show(errorMsg, "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (config.MonitoredSites.Length == 0 && config.MonitoredAppPools.Length == 0)
            {
                MessageBox.Show("请至少添加一个监控站点或应用程序池。", "配置错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            monitor.Start(config);
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            lblStatus.Text = "状态: 监控运行中";

            // 自动最小化到托盘
            if (config.AutoMinimizeToTray && monitor.IsRunning)
            {
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            monitor.Stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblStatus.Text = "状态: 已停止";
        }

        private void BtnAlertSettings_Click(object sender, EventArgs e)
        {
            using (var form = new UI.AlertSettingsForm(config.AlertSettings ?? new AlertConfig()))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    config.AlertSettings = form.UpdatedConfig;
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MonitorConfig.xml");
                    config.Save(configPath);
                    UpdateStatus("告警设置已保存");
                }
            }
        }

        private void BtnLogViewer_Click(object sender, EventArgs e)
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir))
            {
                MessageBox.Show("日志目录不存在", "日志查看", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var files = Directory.GetFiles(logDir, "IISMonitor_*.log");
            if (files.Length == 0)
            {
                MessageBox.Show("尚无日志文件", "日志查看", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            System.Diagnostics.Process.Start("explorer.exe", logDir);
        }

        /// <summary>
        /// 关于对话框：显示程序信息与 GitHub 仓库地址，点击链接可在浏览器打开。
        /// </summary>
        private void BtnAbout_Click(object sender, EventArgs e)
        {
            const string repoUrl = "https://github.com/xuu1998/IISMonitor";
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

            using (var form = new Form())
            {
                form.Text = "关于 IISMonitor";
                form.Size = new Size(520, 320);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var lblTitle = new Label
                {
                    Text = "IISMonitor",
                    Font = new Font("Segoe UI", 16, FontStyle.Bold),
                    Location = new Point(20, 20),
                    AutoSize = true
                };
                form.Controls.Add(lblTitle);

                var lblVersion = new Label
                {
                    Text = $"版本 {version}",
                    Location = new Point(20, 65),
                    AutoSize = true
                };
                form.Controls.Add(lblVersion);

                var lblDesc = new Label
                {
                    Text = "IIS 站点与应用程序池健康监控、自动恢复工具",
                    Location = new Point(20, 95),
                    AutoSize = true
                };
                form.Controls.Add(lblDesc);

                var lblRepoCaption = new Label
                {
                    Text = "GitHub 仓库:",
                    Location = new Point(20, 140),
                    AutoSize = true
                };
                form.Controls.Add(lblRepoCaption);

                var linkRepo = new LinkLabel
                {
                    Text = repoUrl,
                    Location = new Point(20, 165),
                    AutoSize = true,
                    MaximumSize = new Size(460, 0)
                };
                linkRepo.LinkClicked += (s, ev) =>
                {
                    try { System.Diagnostics.Process.Start(repoUrl); }
                    catch { }
                };
                form.Controls.Add(linkRepo);

                var btnClose = new Button
                {
                    Text = "关闭",
                    Location = new Point(400, 240),
                    Size = new Size(90, 28)
                };
                btnClose.Click += (s, ev) => form.Close();
                form.Controls.Add(btnClose);
                form.AcceptButton = btnClose;

                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// 从本机 IIS 选择站点 URL，追加到监控站点表格（不覆盖已存在的项）
        /// </summary>
        private void BtnPickSites_Click(object sender, EventArgs e)
        {
            var existing = CollectGridValues(dgvSites);
            using (var form = new UI.PickFromIISForm(UI.PickMode.Sites, existing))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;

                var toAdd = form.SelectedItems;
                if (toAdd == null || toAdd.Count == 0)
                {
                    UpdateStatus("未选择任何站点");
                    return;
                }

                int added = AppendToGrid(dgvSites, toAdd, existing);
                UpdateStatus($"已从本机 IIS 添加 {added} 个站点");
            }
        }

        /// <summary>
        /// 从本机 IIS 选择应用程序池，追加到监控应用池表格（不覆盖已存在的项）
        /// </summary>
        private void BtnPickAppPools_Click(object sender, EventArgs e)
        {
            var existing = CollectGridValues(dgvAppPools);
            using (var form = new UI.PickFromIISForm(UI.PickMode.AppPools, existing))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;

                var toAdd = form.SelectedItems;
                if (toAdd == null || toAdd.Count == 0)
                {
                    UpdateStatus("未选择任何应用程序池");
                    return;
                }

                int added = AppendToGrid(dgvAppPools, toAdd, existing);
                UpdateStatus($"已从本机 IIS 添加 {added} 个应用程序池");
            }
        }

        /// <summary>
        /// 收集 DataGridView 第一列的非空值（用于去重判断）
        /// </summary>
        private System.Collections.Generic.List<string> CollectGridValues(DataGridView grid)
        {
            var values = new System.Collections.Generic.List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrWhiteSpace(row.Cells[0].Value.ToString()))
                    values.Add(row.Cells[0].Value.ToString().Trim());
            }
            return values;
        }

        /// <summary>
        /// 将选中的值追加到 DataGridView，自动跳过已存在的项。
        /// 返回实际新增的行数。
        /// </summary>
        private int AppendToGrid(DataGridView grid, System.Collections.Generic.IEnumerable<string> toAdd, System.Collections.Generic.List<string> existing)
        {
            var set = new System.Collections.Generic.HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var value in toAdd)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                string v = value.Trim();
                if (set.Contains(v)) continue;
                set.Add(v);
                grid.Rows.Add(v);
                added++;
            }
            return added;
        }

        private void BtnConfig_Click(object sender, EventArgs e)
        {
            SaveConfigFromUI();

            if (!ValidateMonitoredItems(out string errorMsg))
            {
                MessageBox.Show(errorMsg, "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 应用暗色主题切换
            ApplyTheme(config.EnableDarkMode);

            UpdateStatus("配置已保存");
        }

        /// <summary>
        /// 导出健康检查报告
        /// </summary>
        private void BtnExport_Click(object sender, EventArgs e)
        {
            string jsonlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "health_results.jsonl");
            if (!File.Exists(jsonlPath))
            {
                MessageBox.Show("尚无健康检查数据，请先启动监控。", "导出报告", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rangeForm = new UI.DateRangePromptForm();
            if (rangeForm.ShowDialog(this) != DialogResult.OK) return;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "CSV 文件 (*.csv)|*.csv|HTML 文件 (*.html)|*.html";
            dialog.DefaultExt = "csv";
            dialog.FileName = $"IISMonitor_Report_{DateTime.Now:yyyyMMdd_HHmmss}";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    DateTime? from = rangeForm.FromDate;
                    DateTime? to = rangeForm.ToDate;

                    if (dialog.FilterIndex == 1)
                    {
                        ReportExporter.ExportToCsv(jsonlPath, dialog.FileName, from, to);
                    }
                    else
                    {
                        ReportExporter.ExportToHtml(jsonlPath, dialog.FileName, from, to);
                    }

                    UpdateStatus($"报告已导出: {dialog.FileName}");
                    MessageBox.Show($"报告导出成功！\n{dialog.FileName}", "导出报告", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "导出错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateStatus(string message)
        {
            if (lstStatus.InvokeRequired)
            {
                lstStatus.Invoke(new Action<string>(UpdateStatus), message);
                return;
            }
            lstStatus.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            if (lstStatus.Items.Count > 100)
                lstStatus.Items.RemoveAt(lstStatus.Items.Count - 1);
        }

        private void UpdateCheckResult(CheckType type, string target, bool isHealthy)
        {
            if (dgvSites.InvokeRequired)
            {
                dgvSites.Invoke(new Action<CheckType, string, bool>(UpdateCheckResult), type, target, isHealthy);
                return;
            }

            DataGridView grid = type == CheckType.Site ? dgvSites : dgvAppPools;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Cells[0].Value?.ToString() == target)
                {
                    row.DefaultCellStyle.BackColor = isHealthy ? Color.LightGreen : Color.LightCoral;
                    row.DefaultCellStyle.ForeColor = isHealthy ? Color.DarkGreen : Color.DarkRed;
                    break;
                }
            }

            _healthChart?.RecordResult(target, isHealthy);

            if (!isHealthy)
                Interlocked.Exchange(ref _hasAnyFailure, 1);
        }

        private void UpdateResourceChart(ResourceSnapshot snapshot)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<ResourceSnapshot>(UpdateResourceChart), snapshot);
                return;
            }
            _healthChart?.RecordResource("CPU", snapshot.CpuPercent / 100.0);
            _healthChart?.RecordResource("内存", snapshot.MemoryPercent / 100.0);
            _healthChart?.RecordResource("磁盘", snapshot.DiskPercent / 100.0);
        }

        private void RefreshStatus()
        {
            if (monitor.IsRunning)
                lblStatus.Text = "状态: 监控运行中 ✓";
            else
                lblStatus.Text = "状态: 已停止 ✗";

            // 更新托盘图标状态
            UpdateTrayIcon();
        }

        /// <summary>
        /// 应用暗色/亮色主题
        /// </summary>
        private void ApplyTheme(bool useDark)
        {
            IISMonitor.UI.ThemeManager.Apply(this, useDark);
        }

        /// <summary>
        /// 更新系统托盘图标颜色（绿/黄/红）
        /// </summary>
        private void UpdateTrayIcon()
        {
            try
            {
                Color iconColor;
                if (!monitor.IsRunning)
                {
                    iconColor = Color.Red; // 已停止
                }
                else if (System.Threading.Thread.VolatileRead(ref _hasAnyFailure) == 1)
                {
                    iconColor = Color.Orange; // 有故障
                }
                else
                {
                    iconColor = Color.Green; // 全部正常
                }

                using (Bitmap bmp = new Bitmap(16, 16))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        using (Brush brush = new SolidBrush(iconColor))
                        {
                            g.FillEllipse(brush, 1, 1, 14, 14);
                        }
                        // 外圈边框
                        using (Pen pen = new Pen(Color.Black, 1))
                        {
                            g.DrawEllipse(pen, 1, 1, 14, 14);
                        }
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    Icon newIcon = Icon.FromHandle(hIcon);
                    notifyIcon.Icon = newIcon;
                }

                // 重置故障标记（下次 RefreshStatus 重新计算）
                Interlocked.Exchange(ref _hasAnyFailure, 0);
            }
            catch
            {
                // 图标创建失败时忽略
            }
        }

        /// <summary>
        /// 手动测试单个站点（双击 DataGridView 单元格触发）
        /// </summary>
        private void DgvSites_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvSites.Rows[e.RowIndex];
            if (row.Cells[0].Value == null) return;

            string url = row.Cells[0].Value.ToString().Trim();
            if (string.IsNullOrEmpty(url)) return;

            UpdateStatus($"正在测试站点: {url}");
            bool isHealthy = IISHelper.CheckSiteHttp(url, 10);
            row.DefaultCellStyle.BackColor = isHealthy ? Color.LightGreen : Color.LightCoral;
            row.DefaultCellStyle.ForeColor = isHealthy ? Color.DarkGreen : Color.DarkRed;
            UpdateStatus($"站点 [{url}] 测试结果: {(isHealthy ? "正常" : "不可达")}");
        }

        /// <summary>
        /// 手动测试单个应用程序池（双击 DataGridView 单元格触发）
        /// </summary>
        private void DgvAppPools_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvAppPools.Rows[e.RowIndex];
            if (row.Cells[0].Value == null) return;

            string poolName = row.Cells[0].Value.ToString().Trim();
            if (string.IsNullOrEmpty(poolName)) return;

            UpdateStatus($"正在测试应用程序池: {poolName}");
            var statuses = IISHelper.GetAppPoolStatuses();
            bool isRunning = statuses.ContainsKey(poolName) && statuses[poolName];
            row.DefaultCellStyle.BackColor = isRunning ? Color.LightGreen : Color.LightCoral;
            row.DefaultCellStyle.ForeColor = isRunning ? Color.DarkGreen : Color.DarkRed;
            UpdateStatus($"应用程序池 [{poolName}] 测试结果: {(isRunning ? "运行中" : "已停止")}");
        }
    }
}
