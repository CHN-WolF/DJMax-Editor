using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DJMaxEditor.UI;

namespace DJMaxEditor.Files.Tech
{
    /// <summary>
    /// Open-time difficulty chooser for a multi-pattern track.tech container. One pattern
    /// opens for editing; every other slot is retained verbatim for save-back.
    /// </summary>
    internal sealed class TechPatternPickerForm : Form
    {
        public int SelectedPatternIndex { get; private set; }

        public TechPatternPickerForm(IList<TechPatternInfo> patterns)
        {
            if (patterns == null || patterns.Count == 0)
            {
                throw new ArgumentException("at least one pattern is required", "patterns");
            }

            Text = "Open TECHMANIA track";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = StudioDesignSystem.Void;
            ForeColor = StudioDesignSystem.Frost;
            Font = StudioDesignSystem.BodyFont(9f);
            ClientSize = new Size(360, 260);
            Padding = new Padding(12);

            var caption = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = StudioDesignSystem.Muted,
                Text = "This track packs several difficulties. Open which one?",
                Padding = new Padding(0, 0, 0, 8)
            };

            _list = new ListBox
            {
                BackColor = StudioDesignSystem.Deck,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                ForeColor = StudioDesignSystem.Frost,
                IntegralHeight = false,
                ItemHeight = 20
            };
            foreach (TechPatternInfo pattern in patterns)
            {
                _list.Items.Add(pattern);
            }
            _list.SelectedIndex = 0;
            _list.DoubleClick += delegate { Accept(); };

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };
            var open = StudioDesignSystem.CreateDeckButton("Open");
            open.DialogResult = DialogResult.None;
            open.Width = 96;
            open.Click += delegate { Accept(); };
            var cancel = StudioDesignSystem.CreateDeckButton("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Width = 96;
            buttons.Controls.Add(open);
            buttons.Controls.Add(cancel);

            Controls.Add(_list);
            Controls.Add(buttons);
            Controls.Add(caption);
            caption.Dock = DockStyle.Top;
            buttons.Dock = DockStyle.Bottom;

            AcceptButton = open;
            CancelButton = cancel;
        }

        private readonly ListBox _list;

        private void Accept()
        {
            var pattern = _list.SelectedItem as TechPatternInfo;
            if (pattern == null)
            {
                return;
            }
            SelectedPatternIndex = pattern.Index;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
