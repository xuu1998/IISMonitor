using System;
using System.Drawing;
using System.Windows.Forms;

namespace IISMonitor.UI
{
    public class AlertSettingsForm : Form
    {
        private TextBox txtSmtpHost;
        private NumericUpDown numSmtpPort;
        private TextBox txtSmtpUser;
        private TextBox txtSmtpPass;
        private CheckBox chkSmtpSsl;
        private TextBox txtFromAddr;
        private TextBox txtToAddr;
        private CheckBox chkEnableSmtp;
        private CheckBox chkEnableWebhook;
        private TextBox txtWebhookUrl;
        private NumericUpDown numCooldown;
        private Button btnTestSmtp;
        private Button btnTestWebhook;
        private Button btnSave;
        private Button btnCancel;
        private Label lblStatus;

        private AlertConfig _config;
        private AlertConfig _updatedConfig;

        public AlertConfig UpdatedConfig => _updatedConfig;

        public AlertSettingsForm(AlertConfig config)
        {
            _config = config ?? new AlertConfig();
            _updatedConfig = _config;
            InitUI();
            LoadFromConfig();
        }

        private void InitUI()
        {
            this.Text = "告警通知设置";
            this.Size = new Size(420, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 12;
            int x1 = 12, x2 = 130;

            SectionLabel("=== SMTP 邮件 ===", x1, ref y);

            chkEnableSmtp = new CheckBox { Text = "启用 SMTP", Location = new Point(x1, y), AutoSize = true };
            Controls.Add(chkEnableSmtp); y += 22;

            AddRow("SMTP 主机:", x1, ref y, x2, out txtSmtpHost);
            AddRow("端口:", x1, ref y, x2, out numSmtpPort, 25, 1, 65535);
            AddRow("用户名:", x1, ref y, x2, out txtSmtpUser);
            AddRow("密码:", x1, ref y, x2, out txtSmtpPass);
            txtSmtpPass.UseSystemPasswordChar = true;

            chkSmtpSsl = new CheckBox { Text = "使用 SSL", Location = new Point(x2, y), AutoSize = true };
            Controls.Add(chkSmtpSsl); y += 22;

            AddRow("发件人:", x1, ref y, x2, out txtFromAddr);
            AddRow("收件人:", x1, ref y, x2, out txtToAddr);

            btnTestSmtp = new Button { Text = "测试发送", Location = new Point(x2, y), Width = 90, Height = 28 };
            btnTestSmtp.Click += TestSmtpClick;
            Controls.Add(btnTestSmtp); y += 32;

            y += 8;
            SectionLabel("=== Webhook ===", x1, ref y);

            chkEnableWebhook = new CheckBox { Text = "启用 Webhook", Location = new Point(x1, y), AutoSize = true };
            Controls.Add(chkEnableWebhook); y += 22;

            AddRow("Webhook URL:", x1, ref y, x2, out txtWebhookUrl);

            btnTestWebhook = new Button { Text = "测试发送", Location = new Point(x2, y), Width = 90, Height = 28 };
            btnTestWebhook.Click += TestWebhookClick;
            Controls.Add(btnTestWebhook); y += 32;

            y += 8;
            SectionLabel("=== 告警行为 ===", x1, ref y);

            lblStatus = new Label { Text = "", Location = new Point(x1, y), AutoSize = true, ForeColor = Color.Gray, Width = 360 };
            Controls.Add(lblStatus); y += 22;

            numCooldown = new NumericUpDown { Minimum = 10, Maximum = 3600, Value = 300, Location = new Point(x2, y), Width = 80 };
            Controls.Add(numCooldown); y += 28;

            y += 8;
            btnSave = new Button { Text = "保存", Location = new Point(x2 - 90, y), Width = 80, Height = 28 };
            btnSave.Click += (s, e) => { SaveToConfig(); this.DialogResult = DialogResult.OK; this.Close(); };
            Controls.Add(btnSave);

            btnCancel = new Button { Text = "取消", Location = new Point(x2 + 10, y), Width = 80, Height = 28 };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            Controls.Add(btnCancel);
        }

        private void SectionLabel(string text, int x, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            Controls.Add(lbl);
            y += 24;
        }

        private void AddRow(string labelText, int x1, ref int y, int x2, out TextBox txt)
        {
            var lbl = new Label { Text = labelText, Location = new Point(x1, y + 3), AutoSize = true };
            Controls.Add(lbl);
            txt = new TextBox { Location = new Point(x2, y), Width = 250 };
            Controls.Add(txt);
            y += 26;
        }

        private void AddRow(string labelText, int x1, ref int y, int x2, out NumericUpDown num, int val, int min, int max)
        {
            var lbl = new Label { Text = labelText, Location = new Point(x1, y + 3), AutoSize = true };
            Controls.Add(lbl);
            num = new NumericUpDown { Minimum = min, Maximum = max, Value = val, Location = new Point(x2, y), Width = 80 };
            Controls.Add(num);
            y += 26;
        }

        private void LoadFromConfig()
        {
            chkEnableSmtp.Checked = _config.EnableSmtp;
            txtSmtpHost.Text = _config.SmtpHost;
            numSmtpPort.Value = _config.SmtpPort;
            txtSmtpUser.Text = _config.SmtpUsername;
            txtSmtpPass.Text = _config.SmtpPassword;
            chkSmtpSsl.Checked = _config.SmtpUseSsl;
            txtFromAddr.Text = _config.FromAddress;
            txtToAddr.Text = _config.ToAddress;
            chkEnableWebhook.Checked = _config.EnableWebhook;
            txtWebhookUrl.Text = _config.WebhookUrl;
            numCooldown.Value = _config.AlertCooldownSeconds;
        }

        private void SaveToConfig()
        {
            if (_updatedConfig == null) _updatedConfig = new AlertConfig();
            _updatedConfig.EnableSmtp = chkEnableSmtp.Checked;
            _updatedConfig.SmtpHost = txtSmtpHost.Text.Trim();
            _updatedConfig.SmtpPort = (int)numSmtpPort.Value;
            _updatedConfig.SmtpUsername = txtSmtpUser.Text.Trim();
            _updatedConfig.SmtpPassword = txtSmtpPass.Text;
            _updatedConfig.SmtpUseSsl = chkSmtpSsl.Checked;
            _updatedConfig.FromAddress = txtFromAddr.Text.Trim();
            _updatedConfig.ToAddress = txtToAddr.Text.Trim();
            _updatedConfig.EnableWebhook = chkEnableWebhook.Checked;
            _updatedConfig.WebhookUrl = txtWebhookUrl.Text.Trim();
            _updatedConfig.AlertCooldownSeconds = (int)numCooldown.Value;
        }

        private void TestSmtpClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSmtpHost.Text))
            { lblStatus.Text = "请填写 SMTP 主机"; return; }
            lblStatus.Text = "正在测试 SMTP...";
            var testConfig = new AlertConfig
            {
                EnableSmtp = true,
                SmtpHost = txtSmtpHost.Text.Trim(),
                SmtpPort = (int)numSmtpPort.Value,
                SmtpUsername = txtSmtpUser.Text.Trim(),
                SmtpPassword = txtSmtpPass.Text,
                SmtpUseSsl = chkSmtpSsl.Checked,
                FromAddress = txtFromAddr.Text.Trim(),
                ToAddress = txtToAddr.Text.Trim(),
                AlertCooldownSeconds = 0
            };
            var svc = new AlertService(testConfig);
            svc.SendAlert("SMTP测试", $"IISMonitor SMTP 测试邮件 - {DateTime.Now}", AlertLevel.Info);
            lblStatus.Text = "SMTP 测试已发送（请检查收件箱）";
        }

        private void TestWebhookClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtWebhookUrl.Text))
            { lblStatus.Text = "请填写 Webhook URL"; return; }
            lblStatus.Text = "正在测试 Webhook...";
            var testConfig = new AlertConfig
            {
                EnableWebhook = true,
                WebhookUrl = txtWebhookUrl.Text.Trim(),
                AlertCooldownSeconds = 0
            };
            var svc = new AlertService(testConfig);
            svc.SendAlert("Webhook测试", $"IISMonitor Webhook 测试 - {DateTime.Now}", AlertLevel.Info);
            lblStatus.Text = "Webhook 测试已发送";
        }
    }
}
