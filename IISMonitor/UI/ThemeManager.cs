using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace IISMonitor.UI
{
    /// <summary>
    /// 主题管理器：递归遍历控件并应用亮/暗色调色板
    /// </summary>
    public static class ThemeManager
    {
        public static class Light
        {
            public static readonly Color FormBack = SystemColors.Control;
            public static readonly Color FormFore = SystemColors.ControlText;
            public static readonly Color ListBack = SystemColors.Window;
            public static readonly Color ListFore = SystemColors.WindowText;
            public static readonly Color GridBack = SystemColors.Window;
            public static readonly Color GridFore = SystemColors.ControlText;
            public static readonly Color GridHeaderBack = SystemColors.Control;
            public static readonly Color GridHeaderFore = SystemColors.ControlText;
            public static readonly Color GridLine = SystemColors.ControlDark;
            public static readonly Color ButtonBack = SystemColors.Control;
            public static readonly Color ButtonFore = SystemColors.ControlText;
            public static readonly Color InputBack = SystemColors.Window;
            public static readonly Color InputFore = SystemColors.WindowText;
        }

        public static class Dark
        {
            public static readonly Color FormBack = Color.FromArgb(30, 30, 30);
            public static readonly Color FormFore = Color.White;
            public static readonly Color ListBack = Color.FromArgb(45, 45, 45);
            public static readonly Color ListFore = Color.LightGray;
            public static readonly Color GridBack = Color.FromArgb(45, 45, 45);
            public static readonly Color GridFore = Color.White;
            public static readonly Color GridHeaderBack = Color.FromArgb(70, 70, 70);
            public static readonly Color GridHeaderFore = Color.White;
            public static readonly Color GridLine = Color.FromArgb(80, 80, 80);
            public static readonly Color ButtonBack = Color.FromArgb(60, 60, 60);
            public static readonly Color ButtonFore = Color.White;
            public static readonly Color InputBack = Color.FromArgb(55, 55, 55);
            public static readonly Color InputFore = Color.White;
        }

        public static void Apply(Control root, bool dark)
        {
            if (root == null) return;
            root.BackColor = dark ? Dark.FormBack : Light.FormBack;
            root.ForeColor = dark ? Dark.FormFore : Light.FormFore;

            ApplyToChildren(root, dark);
        }

        private static void ApplyToChildren(Control parent, bool dark)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is ListBox || child is TextBox)
                {
                    child.BackColor = dark ? Dark.ListBack : Light.ListBack;
                    child.ForeColor = dark ? Dark.ListFore : Light.ListFore;
                }
                else if (child is Button)
                {
                    child.BackColor = dark ? Dark.ButtonBack : Light.ButtonBack;
                    child.ForeColor = dark ? Dark.ButtonFore : Light.ButtonFore;
                }
                else if (child is ComboBox || child is NumericUpDown)
                {
                    child.BackColor = dark ? Dark.InputBack : Light.InputBack;
                    child.ForeColor = dark ? Dark.InputFore : Light.InputFore;
                }
                else if (child is CheckBox || child is Label)
                {
                    child.ForeColor = dark ? Dark.FormFore : Light.FormFore;
                }
                else if (child is DataGridView grid)
                {
                    grid.BackgroundColor = dark ? Dark.GridBack : Light.GridBack;
                    grid.DefaultCellStyle.BackColor = dark ? Dark.GridBack : Light.GridBack;
                    grid.DefaultCellStyle.ForeColor = dark ? Dark.GridFore : Light.GridFore;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Dark.GridHeaderBack : Light.GridHeaderBack;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Dark.GridHeaderFore : Light.GridHeaderFore;
                    grid.GridColor = dark ? Dark.GridLine : Light.GridLine;
                }
                else if (child is TabControl || child is Panel || child is GroupBox)
                {
                    child.BackColor = dark ? Dark.FormBack : Light.FormBack;
                    child.ForeColor = dark ? Dark.FormFore : Light.FormFore;
                }

                if (child.HasChildren)
                    ApplyToChildren(child, dark);
            }
        }
    }
}
