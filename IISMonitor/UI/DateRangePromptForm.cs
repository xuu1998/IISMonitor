using System;
using System.Drawing;
using System.Windows.Forms;

namespace IISMonitor.UI
{
    public class DateRangePromptForm : Form
    {
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private CheckBox chkAll;
        private Button btnOk;
        private Button btnCancel;

        public DateTime? FromDate => chkAll.Checked ? (DateTime?)null : dtpFrom.Value.Date;
        public DateTime? ToDate => chkAll.Checked ? (DateTime?)null : dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

        public DateRangePromptForm()
        {
            this.Text = "选择导出时间范围";
            this.Size = new Size(340, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            chkAll = new CheckBox { Text = "导出全部数据", Location = new Point(12, 12), AutoSize = true, Checked = true };
            chkAll.CheckedChanged += (s, e) =>
            {
                dtpFrom.Enabled = !chkAll.Checked;
                dtpTo.Enabled = !chkAll.Checked;
            };
            Controls.Add(chkAll);

            var lbl1 = new Label { Text = "从:", Location = new Point(12, 44), AutoSize = true };
            Controls.Add(lbl1);
            dtpFrom = new DateTimePicker { Location = new Point(60, 40), Width = 240, Format = DateTimePickerFormat.Short, Enabled = false };
            Controls.Add(dtpFrom);

            var lbl2 = new Label { Text = "到:", Location = new Point(12, 74), AutoSize = true };
            Controls.Add(lbl2);
            dtpTo = new DateTimePicker { Location = new Point(60, 70), Width = 240, Format = DateTimePickerFormat.Short, Enabled = false };
            Controls.Add(dtpTo);

            btnOk = new Button { Text = "确定", Location = new Point(120, 120), Width = 80 };
            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            Controls.Add(btnOk);

            btnCancel = new Button { Text = "取消", Location = new Point(210, 120), Width = 80 };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            Controls.Add(btnCancel);
        }
    }
}
