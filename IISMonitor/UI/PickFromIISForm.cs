using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace IISMonitor.UI
{
    /// <summary>
    /// 从本机 IIS 选择监控目标的模式
    /// </summary>
    public enum PickMode
    {
        Sites,
        AppPools
    }

    /// <summary>
    /// 从本机 IIS 列表中选择站点 URL 或应用程序池名称的多选窗体。
    /// 选中项通过 SelectedItems 返回；已存在的项会在列表中标记并默认不勾选。
    /// </summary>
    public class PickFromIISForm : Form
    {
        private CheckedListBox _list;
        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnSelectAll;
        private Button _btnSelectNone;
        private Label _lblHint;

        private readonly PickMode _mode;
        private readonly HashSet<string> _existing;

        /// <summary>
        /// 用户选中的条目（站点为 URL，应用池为名称）
        /// </summary>
        public List<string> SelectedItems { get; } = new List<string>();

        public PickFromIISForm(PickMode mode, IEnumerable<string> existing)
        {
            _mode = mode;
            _existing = new HashSet<string>(existing ?? new string[0], StringComparer.OrdinalIgnoreCase);
            InitUI();
            LoadItems();
        }

        private void InitUI()
        {
            this.Text = _mode == PickMode.Sites ? "从本机 IIS 选择站点" : "从本机 IIS 选择应用程序池";
            this.Size = new Size(520, 460);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _lblHint = new Label
            {
                Text = "勾选要添加的条目（已在监控列表中的标记为「已存在」，不会重复添加）:",
                Location = new Point(12, 12),
                AutoSize = true,
                Width = 480
            };
            Controls.Add(_lblHint);

            _list = new CheckedListBox
            {
                Location = new Point(12, 36),
                Size = new Size(480, 320),
                CheckOnClick = true
            };
            Controls.Add(_list);

            _btnSelectAll = new Button { Text = "全选", Location = new Point(12, 366), Width = 80, Height = 28 };
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);
            Controls.Add(_btnSelectAll);

            _btnSelectNone = new Button { Text = "全不选", Location = new Point(100, 366), Width = 80, Height = 28 };
            _btnSelectNone.Click += (s, e) => SetAllChecked(false);
            Controls.Add(_btnSelectNone);

            _btnOk = new Button { Text = "确定", Location = new Point(330, 366), Width = 75, Height = 28 };
            _btnOk.Click += (s, e) =>
            {
                SelectedItems.Clear();
                foreach (var item in _list.CheckedItems)
                {
                    string raw = ExtractRawValue(item.ToString());
                    SelectedItems.Add(raw);
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            Controls.Add(_btnOk);

            _btnCancel = new Button { Text = "取消", Location = new Point(415, 366), Width = 75, Height = 28 };
            _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
        }

        private void SetAllChecked(bool check)
        {
            for (int i = 0; i < _list.Items.Count; i++)
            {
                string raw = ExtractRawValue(_list.Items[i].ToString());
                // 已存在的项不自动勾选，避免误重复添加
                if (IsExisting(raw)) continue;
                _list.SetItemChecked(i, check);
            }
        }

        private bool IsExisting(string value)
        {
            return _existing.Contains(value);
        }

        private void LoadItems()
        {
            _list.Items.Clear();

            try
            {
                if (_mode == PickMode.Sites)
                {
                    var sites = IISHelper.GetSiteEntries();
                    if (sites.Count == 0)
                    {
                        _lblHint.Text = "未发现本机 IIS 站点（请确认 IIS 已安装、本程序以管理员身份运行）。";
                        return;
                    }
                    foreach (var s in sites)
                    {
                        string display = $"{s.Url}    [站点: {s.SiteName}, {(s.IsRunning ? "运行中" : "已停止")}]";
                        _list.Items.Add(s.Url);
                        int idx = _list.Items.Count - 1;
                        // 用辅助映射保留显示文本：直接覆盖为展示文本
                        _list.Items[idx] = display;
                    }
                }
                else
                {
                    var statuses = IISHelper.GetAppPoolStatuses();
                    if (statuses.Count == 0)
                    {
                        _lblHint.Text = "未发现本机应用程序池（请确认 IIS 已安装、本程序以管理员身份运行）。";
                        return;
                    }
                    foreach (var kv in statuses)
                    {
                        string display = $"{kv.Key}    [{(kv.Value ? "运行中" : "已停止")}]";
                        _list.Items.Add(display);
                    }
                }

                // 标记已存在的项（置灰并提示）
                for (int i = 0; i < _list.Items.Count; i++)
                {
                    string raw = ExtractRawValue(_list.Items[i].ToString());
                    if (IsExisting(raw))
                    {
                        // 通过禁用单项勾选来表达“已存在”
                        // CheckedListBox 没有单项禁用，改用默认不勾选 + 文本前缀
                        _list.Items[i] = "（已存在）" + _list.Items[i];
                    }
                }
            }
            catch (Exception ex)
            {
                _lblHint.Text = "读取本机 IIS 失败: " + ex.Message;
            }
        }

        /// <summary>
        /// 从展示文本中提取原始值（站点为 URL，应用池为名称）。
        /// 展示格式为 "[值]    [描述]" 或 "（已存在）[值]    [描述]"。
        /// </summary>
        private string ExtractRawValue(string display)
        {
            string s = display;
            if (s.StartsWith("（已存在）")) s = s.Substring("（已存在）".Length);
            int sep = s.IndexOf("    ");
            return sep >= 0 ? s.Substring(0, sep).Trim() : s.Trim();
        }
    }
}
