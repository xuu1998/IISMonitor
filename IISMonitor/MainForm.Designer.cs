namespace IISMonitor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Timer uiTimer;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing && notifyIcon != null)
            {
                notifyIcon.Dispose();
            }
            if (disposing && trayMenu != null)
            {
                trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.Windows.Forms.ListBox lstStatus;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.CheckBox chkAutoStart;
        private System.Windows.Forms.Button btnConfig;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnAlertSettings;
        private System.Windows.Forms.Button btnLogViewer;
        private System.Windows.Forms.DataGridView dgvSites;
        private System.Windows.Forms.DataGridView dgvAppPools;
        private System.Windows.Forms.Button btnPickSites;
        private System.Windows.Forms.Button btnPickAppPools;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtCheckInterval;
        private System.Windows.Forms.NumericUpDown numFailThreshold;
        private System.Windows.Forms.ComboBox cmbRestartStrategy;
        private System.Windows.Forms.CheckBox chkHttpCheck;
        private System.Windows.Forms.CheckBox chkAppPoolCheck;
        private System.Windows.Forms.CheckBox chkDarkMode;
        private System.Windows.Forms.CheckBox chkAutoMinimize;
        private System.Windows.Forms.CheckBox chkResourceMonitor;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayMenu;

        private void InitializeComponent()
        {
            this.lstStatus = new System.Windows.Forms.ListBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.chkAutoStart = new System.Windows.Forms.CheckBox();
            this.btnConfig = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnAlertSettings = new System.Windows.Forms.Button();
            this.btnLogViewer = new System.Windows.Forms.Button();
            this.dgvSites = new System.Windows.Forms.DataGridView();
            this.dgvAppPools = new System.Windows.Forms.DataGridView();
            this.btnPickSites = new System.Windows.Forms.Button();
            this.btnPickAppPools = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtCheckInterval = new System.Windows.Forms.TextBox();
            this.numFailThreshold = new System.Windows.Forms.NumericUpDown();
            this.cmbRestartStrategy = new System.Windows.Forms.ComboBox();
            this.chkHttpCheck = new System.Windows.Forms.CheckBox();
            this.chkAppPoolCheck = new System.Windows.Forms.CheckBox();
            this.chkDarkMode = new System.Windows.Forms.CheckBox();
            this.chkAutoMinimize = new System.Windows.Forms.CheckBox();
            this.chkResourceMonitor = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSites)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAppPools)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFailThreshold)).BeginInit();
            this.SuspendLayout();
            // 
            // lstStatus
            // 
            this.lstStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.lstStatus.Font = new System.Drawing.Font("Consolas", 9F);
            this.lstStatus.FormattingEnabled = true;
            this.lstStatus.ItemHeight = 22;
            this.lstStatus.Location = new System.Drawing.Point(18, 445);
            this.lstStatus.Margin = new System.Windows.Forms.Padding(4);
            this.lstStatus.Name = "lstStatus";
            this.lstStatus.Size = new System.Drawing.Size(1108, 224);
            this.lstStatus.TabIndex = 0;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(18, 18);
            this.btnStart.Margin = new System.Windows.Forms.Padding(4);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(120, 45);
            this.btnStart.TabIndex = 1;
            this.btnStart.Text = "启动监控";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(147, 18);
            this.btnStop.Margin = new System.Windows.Forms.Padding(4);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 45);
            this.btnStop.TabIndex = 2;
            this.btnStop.Text = "停止监控";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            // 
            // chkAutoStart
            // 
            this.chkAutoStart.AutoSize = true;
            this.chkAutoStart.Location = new System.Drawing.Point(588, 405);
            this.chkAutoStart.Name = "chkAutoStart";
            this.chkAutoStart.Size = new System.Drawing.Size(160, 22);
            this.chkAutoStart.TabIndex = 20;
            this.chkAutoStart.Text = "启动时自动监控";
            this.chkAutoStart.UseVisualStyleBackColor = true;
            // 
            // btnConfig
            // 
            this.btnConfig.Location = new System.Drawing.Point(276, 18);
            this.btnConfig.Margin = new System.Windows.Forms.Padding(4);
            this.btnConfig.Name = "btnConfig";
            this.btnConfig.Size = new System.Drawing.Size(120, 45);
            this.btnConfig.TabIndex = 3;
            this.btnConfig.Text = "保存配置";
            this.btnConfig.UseVisualStyleBackColor = true;
            this.btnConfig.Click += new System.EventHandler(this.BtnConfig_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(456, 20);
            this.btnExport.Margin = new System.Windows.Forms.Padding(4);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(120, 45);
            this.btnExport.TabIndex = 15;
            this.btnExport.Text = "导出报告";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.BtnExport_Click);
            // 
            // btnAlertSettings
            // 
            this.btnAlertSettings.Location = new System.Drawing.Point(584, 20);
            this.btnAlertSettings.Margin = new System.Windows.Forms.Padding(4);
            this.btnAlertSettings.Name = "btnAlertSettings";
            this.btnAlertSettings.Size = new System.Drawing.Size(120, 45);
            this.btnAlertSettings.TabIndex = 19;
            this.btnAlertSettings.Text = "告警设置";
            this.btnAlertSettings.UseVisualStyleBackColor = true;
            this.btnAlertSettings.Click += new System.EventHandler(this.BtnAlertSettings_Click);
            // 
            // btnLogViewer
            // 
            this.btnLogViewer.Location = new System.Drawing.Point(712, 18);
            this.btnLogViewer.Margin = new System.Windows.Forms.Padding(4);
            this.btnLogViewer.Name = "btnLogViewer";
            this.btnLogViewer.Size = new System.Drawing.Size(120, 45);
            this.btnLogViewer.TabIndex = 20;
            this.btnLogViewer.Text = "日志查看";
            this.btnLogViewer.UseVisualStyleBackColor = true;
            this.btnLogViewer.Click += new System.EventHandler(this.BtnLogViewer_Click);
            // 
            // dgvSites
            // 
            this.dgvSites.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSites.Location = new System.Drawing.Point(18, 120);
            this.dgvSites.Margin = new System.Windows.Forms.Padding(4);
            this.dgvSites.Name = "dgvSites";
            this.dgvSites.RowHeadersWidth = 62;
            this.dgvSites.Size = new System.Drawing.Size(540, 270);
            this.dgvSites.TabIndex = 4;
            this.dgvSites.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvSites_CellDoubleClick);
            // 
            // dgvAppPools
            // 
            this.dgvAppPools.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAppPools.Location = new System.Drawing.Point(588, 120);
            this.dgvAppPools.Margin = new System.Windows.Forms.Padding(4);
            this.dgvAppPools.Name = "dgvAppPools";
            this.dgvAppPools.RowHeadersWidth = 62;
            this.dgvAppPools.Size = new System.Drawing.Size(540, 270);
            this.dgvAppPools.TabIndex = 5;
            this.dgvAppPools.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvAppPools_CellDoubleClick);
            // 
            // btnPickSites
            // 
            this.btnPickSites.Location = new System.Drawing.Point(856, 18);
            this.btnPickSites.Name = "btnPickSites";
            this.btnPickSites.Size = new System.Drawing.Size(120, 45);
            this.btnPickSites.TabIndex = 21;
            this.btnPickSites.Text = "从本机选站点";
            this.btnPickSites.UseVisualStyleBackColor = true;
            this.btnPickSites.Click += new System.EventHandler(this.BtnPickSites_Click);
            // 
            // btnPickAppPools
            // 
            this.btnPickAppPools.Location = new System.Drawing.Point(996, 20);
            this.btnPickAppPools.Name = "btnPickAppPools";
            this.btnPickAppPools.Size = new System.Drawing.Size(120, 45);
            this.btnPickAppPools.TabIndex = 22;
            this.btnPickAppPools.Text = "从本机选应用池";
            this.btnPickAppPools.UseVisualStyleBackColor = true;
            this.btnPickAppPools.Click += new System.EventHandler(this.BtnPickAppPools_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblStatus.Location = new System.Drawing.Point(18, 405);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(124, 27);
            this.lblStatus.TabIndex = 6;
            this.lblStatus.Text = "状态: 未启动";
            // 
            // txtCheckInterval
            // 
            this.txtCheckInterval.Location = new System.Drawing.Point(147, 82);
            this.txtCheckInterval.Margin = new System.Windows.Forms.Padding(4);
            this.txtCheckInterval.Name = "txtCheckInterval";
            this.txtCheckInterval.Size = new System.Drawing.Size(73, 28);
            this.txtCheckInterval.TabIndex = 7;
            this.txtCheckInterval.Text = "5";
            // 
            // numFailThreshold
            // 
            this.numFailThreshold.Location = new System.Drawing.Point(348, 82);
            this.numFailThreshold.Margin = new System.Windows.Forms.Padding(4);
            this.numFailThreshold.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numFailThreshold.Name = "numFailThreshold";
            this.numFailThreshold.Size = new System.Drawing.Size(75, 28);
            this.numFailThreshold.TabIndex = 8;
            this.numFailThreshold.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            // 
            // cmbRestartStrategy
            // 
            this.cmbRestartStrategy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRestartStrategy.Items.AddRange(new object[] {
            "仅回收应用程序池",
            "先回收应用池，失败则重启 IIS",
            "直接重启 IIS",
            "先停止再启动应用池",
            "先停止再启动应用池，失败则重启 IIS"});
            this.cmbRestartStrategy.Location = new System.Drawing.Point(558, 82);
            this.cmbRestartStrategy.Margin = new System.Windows.Forms.Padding(4);
            this.cmbRestartStrategy.Name = "cmbRestartStrategy";
            this.cmbRestartStrategy.Size = new System.Drawing.Size(178, 26);
            this.cmbRestartStrategy.TabIndex = 9;
            // 
            // chkHttpCheck
            // 
            this.chkHttpCheck.AutoSize = true;
            this.chkHttpCheck.Checked = true;
            this.chkHttpCheck.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkHttpCheck.Location = new System.Drawing.Point(762, 86);
            this.chkHttpCheck.Margin = new System.Windows.Forms.Padding(4);
            this.chkHttpCheck.Name = "chkHttpCheck";
            this.chkHttpCheck.Size = new System.Drawing.Size(70, 22);
            this.chkHttpCheck.TabIndex = 10;
            this.chkHttpCheck.Text = "HTTP";
            // 
            // chkAppPoolCheck
            // 
            this.chkAppPoolCheck.AutoSize = true;
            this.chkAppPoolCheck.Checked = true;
            this.chkAppPoolCheck.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAppPoolCheck.Location = new System.Drawing.Point(852, 86);
            this.chkAppPoolCheck.Margin = new System.Windows.Forms.Padding(4);
            this.chkAppPoolCheck.Name = "chkAppPoolCheck";
            this.chkAppPoolCheck.Size = new System.Drawing.Size(124, 22);
            this.chkAppPoolCheck.TabIndex = 11;
            this.chkAppPoolCheck.Text = "应用程序池";
            // 
            // chkDarkMode
            // 
            this.chkDarkMode.AutoSize = true;
            this.chkDarkMode.Location = new System.Drawing.Point(762, 405);
            this.chkDarkMode.Margin = new System.Windows.Forms.Padding(4);
            this.chkDarkMode.Name = "chkDarkMode";
            this.chkDarkMode.Size = new System.Drawing.Size(106, 22);
            this.chkDarkMode.TabIndex = 16;
            this.chkDarkMode.Text = "暗色主题";
            // 
            // chkAutoMinimize
            // 
            this.chkAutoMinimize.AutoSize = true;
            this.chkAutoMinimize.Location = new System.Drawing.Point(879, 405);
            this.chkAutoMinimize.Margin = new System.Windows.Forms.Padding(4);
            this.chkAutoMinimize.Name = "chkAutoMinimize";
            this.chkAutoMinimize.Size = new System.Drawing.Size(142, 22);
            this.chkAutoMinimize.TabIndex = 17;
            this.chkAutoMinimize.Text = "启动时最小化";
            // 
            // chkResourceMonitor
            // 
            this.chkResourceMonitor.AutoSize = true;
            this.chkResourceMonitor.Location = new System.Drawing.Point(1022, 405);
            this.chkResourceMonitor.Margin = new System.Windows.Forms.Padding(4);
            this.chkResourceMonitor.Name = "chkResourceMonitor";
            this.chkResourceMonitor.Size = new System.Drawing.Size(106, 22);
            this.chkResourceMonitor.TabIndex = 18;
            this.chkResourceMonitor.Text = "资源监控";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(18, 87);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(125, 18);
            this.label1.TabIndex = 12;
            this.label1.Text = "检查间隔(秒):";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(249, 87);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 18);
            this.label2.TabIndex = 13;
            this.label2.Text = "失败阈值:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(438, 87);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(89, 18);
            this.label3.TabIndex = 14;
            this.label3.Text = "重启策略:";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1137, 837);
            this.Controls.Add(this.chkResourceMonitor);
            this.Controls.Add(this.chkAutoMinimize);
            this.Controls.Add(this.chkDarkMode);
            this.Controls.Add(this.btnLogViewer);
            this.Controls.Add(this.btnAlertSettings);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnPickSites);
            this.Controls.Add(this.btnPickAppPools);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.chkAppPoolCheck);
            this.Controls.Add(this.chkHttpCheck);
            this.Controls.Add(this.cmbRestartStrategy);
            this.Controls.Add(this.numFailThreshold);
            this.Controls.Add(this.txtCheckInterval);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.dgvAppPools);
            this.Controls.Add(this.dgvSites);
            this.Controls.Add(this.btnConfig);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.chkAutoStart);
            this.Controls.Add(this.lstStatus);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MainForm";
            this.Text = "IIS 监控看板";
            ((System.ComponentModel.ISupportInitialize)(this.dgvSites)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAppPools)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFailThreshold)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}
