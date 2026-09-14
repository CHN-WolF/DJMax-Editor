using System;
using System.Windows.Forms;
using DJMaxEditor.Editor;
using DJMaxEditor.UI;

namespace DJMaxEditor
{
    public partial class PropertiesForm : ToolWindow
    {
        private EditorDocumentContext _document;

        private const float PropertyFontSize = 8.5f;

        public PropertiesForm()
        {
            InitializeComponent();
            TabText = "Inspector";
            Text = "Inspector";
            propertyGrid1.Visible = true;
            propertyGrid1.Dock = DockStyle.Fill;
            propertyGrid1.BackColor = StudioDesignSystem.Deck;
            propertyGrid1.CategoryForeColor = StudioDesignSystem.PulseCyan;
            propertyGrid1.CommandsBackColor = StudioDesignSystem.Deck;
            propertyGrid1.CommandsForeColor = StudioDesignSystem.Frost;
            propertyGrid1.HelpBackColor = StudioDesignSystem.Deck;
            propertyGrid1.HelpForeColor = StudioDesignSystem.Muted;
            propertyGrid1.LineColor = StudioDesignSystem.Border;
            propertyGrid1.ViewBackColor = StudioDesignSystem.Void;
            propertyGrid1.ViewForeColor = StudioDesignSystem.Frost;

            var advancedHeader = new Label
            {
                AutoEllipsis = true,
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Top,
                Font = StudioDesignSystem.UtilityFont(PropertyFontSize),
                ForeColor = StudioDesignSystem.BeatViolet,
                Height = 26,
                Padding = new Padding(10, 7, 0, 0),
                Text = "ADVANCED  //  FORMAT-SPECIFIC PROPERTIES"
            };

            Controls.Add(advancedHeader);
        }

        public object PropertyObject
        {
            get { return propertyGrid1.SelectedObject; }
            set { propertyGrid1.SelectedObject = value; }
        }

        public void Bind(EditorDocumentContext document)
        {
            if (_document != null)
            {
                _document.Selection.SelectionChanged -= SelectionChanged;
                _document.UndoManager.OnUndoRedo -= DocumentUndoRedo;
            }
            _document = document;
            if (_document != null)
            {
                _document.Selection.SelectionChanged += SelectionChanged;
                _document.UndoManager.OnUndoRedo += DocumentUndoRedo;
            }
            ShowSelection();
        }

        private void SelectionChanged(object sender, EventArgs e)
        {
            ShowSelection();
        }

        private void DocumentUndoRedo(object sender, UndoManager.Action action)
        {
            ShowSelection();
        }

        private void ShowSelection()
        {
            propertyGrid1.Refresh();
        }
    }
}
