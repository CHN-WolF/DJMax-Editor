using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.Editor;
using DJMaxEditor.UI;

namespace DJMaxEditor.Preview
{
    public sealed class GameplayPreviewControl : Control
    {
        private EditorDocumentContext _document;
        private GameplayPreviewProjection _projection;
        private GameplayPreviewFrame _frame;
        private GameplayPreviewProfile _profile = GameplayPreviewProfile.Generic;
        private TechnikaScrollDirection _scrollDirection = TechnikaScrollDirection.Clockwise;
        private float _noteZoom = 1.0f;
        private readonly TechnikaPlayfieldRenderer _technikaRenderer =
            new TechnikaPlayfieldRenderer();

        public GameplayPreviewControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = StudioDesignSystem.Void;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(320, 220);
            TabStop = true;

            // The persisted sprite style applies from construction so the preview draws the
            // owner's chosen set before any document is bound; the form's dropdown re-sends
            // the same id harmlessly when it initialises.
            SpriteStyleId = FeatureFlags.PreviewSpriteStyleId;
        }

        public EditorDocumentContext Document
        {
            get { return _document; }
        }

        public GameplayPreviewProfile Profile
        {
            get { return _profile; }
        }

        /// <summary>
        /// The TECHNIKA scroll-direction effector. Changing it re-projects the chart -
        /// note positions, scanlines, hold bodies and the approach glow all read the one
        /// direction - so it rebuilds the projection like a profile change does.
        /// </summary>
        public TechnikaScrollDirection ScrollDirection
        {
            get { return _scrollDirection; }
            set
            {
                if (_scrollDirection == value) return;
                _scrollDirection = value;
                RebuildProjection();
            }
        }

        /// <summary>The note-series effector (fade in / fade out), applied per paint from
        /// each note's distance to the sweep.</summary>
        internal TechnikaNoteFader NoteFader
        {
            get { return _technikaRenderer.NoteFader; }
            set
            {
                if (_technikaRenderer.NoteFader == value) return;
                _technikaRenderer.NoteFader = value;
                Invalidate();
            }
        }

        /// <summary>The timeline-series effector (blink / blind), applied per paint from
        /// the frame's musical phase.</summary>
        internal TechnikaLineEffector LineEffector
        {
            get { return _technikaRenderer.LineEffector; }
            set
            {
                if (_technikaRenderer.LineEffector == value) return;
                _technikaRenderer.LineEffector = value;
                Invalidate();
            }
        }

        /// <summary>"ARCADE SPRITES" or "PACKAGED GLYPHS" - which note set the TECHNIKA
        /// playfield resolved, for the panel header. A named style reports its own name
        /// (<c>T3 STAR #03</c>) instead.</summary>
        internal string SpriteSourceLabel
        {
            get { return _technikaRenderer.SpriteSourceLabel; }
        }

        /// <summary>
        /// The SPRITE SET choice, as a catalog style id. Resolving goes through the catalog
        /// rather than straight to the renderer so a persisted id that no longer matches
        /// anything on disk falls back to the same answer AUTO would give, instead of
        /// failing or rendering nothing.
        /// </summary>
        internal string SpriteStyleId
        {
            set
            {
                TechnikaSpriteStyle style = TechnikaSpriteCatalog.Resolve(value);
                if (_technikaRenderer.SetSpriteStyle(style))
                {
                    Invalidate();
                }
            }
        }

        /// <summary>The playback position the current frame was built at, or 0 before a
        /// document is bound. Surfaced in the header status line, which is where the
        /// floating overlay used to draw it.</summary>
        internal int CurrentTick
        {
            get { return _frame == null ? 0 : _frame.CurrentTick; }
        }

        public float NoteZoom
        {
            get { return _noteZoom; }
            set
            {
                _noteZoom = Math.Max(0.75f, Math.Min(2.5f, value));
                Invalidate();
            }
        }

        /// <summary>
        /// The chart's scroll speed as a SWEEP-rate multiplier: notes never move
        /// from their authored scan positions; the scanline crosses the field at
        /// this many times the musical rate (BYTES attribute 1 = half, 2 = double).
        /// Hit states, the hit flash and the approach glow measure from the sweep,
        /// so they follow the faster line.
        /// </summary>
        public double ScrollSpeed
        {
            get { return _scrollSpeed; }
            set
            {
                double clamped = Math.Max(0.25, Math.Min(4.0, value));
                if (Math.Abs(_scrollSpeed - clamped) < 1e-9) return;
                _scrollSpeed = clamped;
                RefreshPlayback();
            }
        }
        private double _scrollSpeed = 1.0;

        public string ProjectionStatus
        {
            get
            {
                return _projection == null
                    ? "NO DOCUMENT"
                    : _projection.StatusLabel;
            }
        }

        public int DiagnosticCount
        {
            get { return _projection == null ? 0 : _projection.Diagnostics.Count; }
        }

        public void Bind(EditorDocumentContext document)
        {
            if (_document != null)
            {
                _document.Model.Tracks.EventAdded -= ChartTopologyChanged;
                _document.Model.Tracks.EventRemoved -= ChartTopologyChanged;
                _document.UndoManager.OnUndoRedo -= DocumentUndoRedo;
            }

            _document = document;
            if (_document != null)
            {
                _document.Model.Tracks.EventAdded += ChartTopologyChanged;
                _document.Model.Tracks.EventRemoved += ChartTopologyChanged;
                _document.UndoManager.OnUndoRedo += DocumentUndoRedo;
            }
            RebuildProjection();
        }

        public void SetProfile(GameplayPreviewProfile profile)
        {
            if (_profile == profile) return;
            _profile = profile;
            RebuildProjection();
        }

        public void RefreshTopology()
        {
            RebuildProjection();
        }

        public void RefreshPlayback()
        {
            if (_projection == null || _document == null)
            {
                _frame = null;
            }
            else
            {
                _frame = _projection.CreateRenderableFrame(
                    _document.Model.CurrentTick, _scrollSpeed);
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _document != null)
            {
                _document.Model.Tracks.EventAdded -= ChartTopologyChanged;
                _document.Model.Tracks.EventRemoved -= ChartTopologyChanged;
                _document.UndoManager.OnUndoRedo -= DocumentUndoRedo;
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            NoteZoom += e.Delta > 0 ? 0.1f : -0.1f;
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(StudioDesignSystem.Void);

            Rectangle viewport = ClientRectangle;
            viewport.Inflate(-12, -12);
            if (viewport.Width <= 0 || viewport.Height <= 0) return;

            if (_projection == null || _frame == null)
            {
                DrawEmptyState(graphics, viewport);
                return;
            }

            if (_projection.Profile == GameplayPreviewProfile.Technika)
            {
                _technikaRenderer.Paint(
                    graphics, viewport, _projection, _frame, _noteZoom);
            }
            else
            {
                DrawGenericFrame(graphics, viewport);
            }
        }

        private void RebuildProjection()
        {
            // Re-probe for sprites an owner may have dropped in since startup; a changed set
            // is worth a repaint even when the projection itself is unchanged.
            if (_technikaRenderer.ReloadSprites())
            {
                Invalidate();
            }
            _projection = _document == null
                ? null
                : GameplayPreviewProjector.Project(_document.Model, _profile, _scrollDirection);
            RefreshPlayback();
        }

        private void ChartTopologyChanged(object sender, EventArgs e)
        {
            RebuildProjection();
        }

        private void DocumentUndoRedo(object sender, UndoManager.Action action)
        {
            RebuildProjection();
        }

        private void DrawEmptyState(Graphics graphics, Rectangle viewport)
        {
            using (var title = StudioDesignSystem.DisplayFont(14f))
            using (var body = StudioDesignSystem.BodyFont(9f))
            using (var primary = new SolidBrush(StudioDesignSystem.Frost))
            using (var muted = new SolidBrush(StudioDesignSystem.Muted))
            {
                graphics.DrawString("GAMEPLAY PREVIEW", title, primary,
                    viewport.Left + 18, viewport.Top + 18);
                graphics.DrawString(
                    "Open a chart to visualize the shared playback position.",
                    body,
                    muted,
                    viewport.Left + 18,
                    viewport.Top + 54);
            }
        }

        private void DrawGenericFrame(Graphics graphics, Rectangle viewport)
        {
            using (var deck = new SolidBrush(StudioDesignSystem.Deck))
            using (var border = new Pen(StudioDesignSystem.Border))
            using (var lanePen = new Pen(Color.FromArgb(100, StudioDesignSystem.Border)))
            {
                graphics.FillRectangle(deck, viewport);
                graphics.DrawRectangle(border, viewport);
                DrawLaneGrid(graphics, viewport, Math.Min(32, _projection.LaneCount), lanePen);
            }

            int playhead = viewport.Left + viewport.Width / 2;
            using (var glow = new Pen(Color.FromArgb(58, StudioDesignSystem.BeatViolet), 8f))
            using (var line = new Pen(StudioDesignSystem.BeatViolet, 2f))
            {
                graphics.DrawLine(glow, playhead, viewport.Top, playhead, viewport.Bottom);
                graphics.DrawLine(line, playhead, viewport.Top, playhead, viewport.Bottom);
            }

            foreach (ProjectedGameplayNote note in _frame.Notes)
            {
                if (note.X <= 0.04 || note.X >= 0.96) continue;
                int x = viewport.Left + (int)Math.Round(note.X * viewport.Width);
                int y = viewport.Top + (int)Math.Round(note.Y * viewport.Height);
                int size = Math.Max(6, (int)Math.Round(12 * _noteZoom));
                Color color = note.State == GameplayPreviewNoteState.Active
                    ? StudioDesignSystem.PulseCyan
                    : StudioDesignSystem.Muted;
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
                }
            }
        }

        private void DrawLaneGrid(
            Graphics graphics,
            Rectangle rectangle,
            int lanes,
            Pen pen)
        {
            lanes = Math.Max(1, lanes);
            for (int lane = 1; lane < lanes; lane++)
            {
                int y = rectangle.Top + (rectangle.Height * lane / lanes);
                graphics.DrawLine(pen, rectangle.Left, y, rectangle.Right, y);
            }
        }
    }
}
