using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.FormatDetection;
using DJMaxEditor.UI;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// Dockable, read-only visualization of the active document. The profile is not a
    /// choice anymore: it is resolved from the chart on bind, so a TECHNIKA chart opens
    /// in the TECHNIKA projection with nothing to confirm. Effector toggles stay
    /// session-only; the sprite set is the other exception - its style id, and any
    /// explicit Technika 3 asset folder the owner picks, persist as user settings so the
    /// chosen note art survives a restart.
    /// </summary>
    public sealed class GameplayPreviewForm : ToolWindow
    {
        private readonly GameplayPreviewControl _preview;
        private readonly Label _status;
        private readonly TrackBar _zoom;
        private readonly ComboBox _scroll;
        private readonly ComboBox _fader;
        private readonly ComboBox _line;
        private readonly ComboBox _speed;
        private readonly ComboBox _spriteSet;
        private readonly Button _browseSpriteRoot;

        /// <summary>The owner-picked manual speed, remembered across binds so a
        /// PT/TECH chart keeps the last choice instead of snapping back.</summary>
        private int _manualSpeedIndex = 1;

        private bool _updatingSpeed;

        /// <summary>The catalog the dropdown currently lists. Rebuilt (and the dropdown
        /// refilled) when a newly browsed asset path changes what exists; the combo's item
        /// order is this list's order.</summary>
        private List<TechnikaSpriteStyle> _spriteStyles;

        /// <summary>Guards the dropdown while it is being (re)filled, so programmatic index
        /// changes are not mistaken for the owner's choice and persisted.</summary>
        private bool _updatingSpriteSet;
        private const long PlaybackFrameIntervalMilliseconds = 15;
        private readonly Stopwatch _playbackClock = Stopwatch.StartNew();
        private long _lastPlaybackFrameMilliseconds = -PlaybackFrameIntervalMilliseconds;

        public GameplayPreviewForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = StudioDesignSystem.Void;
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(360, 260);
            ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockRight;
            TabText = "Gameplay Preview";
            Text = "Gameplay Preview";

            // The header wraps: the form docks right and is often narrower than the
            // full control row, so everything below flows (FlowLayoutPanel) instead
            // of sitting at fixed x positions and getting clipped. The header's
            // height is synced to the flow's wrapped height - AutoSize on the
            // header itself does not follow a docked autosize child reliably.
            var header = new Panel
            {
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(12, 8, 12, 8)
            };
            var title = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Font = StudioDesignSystem.DisplayFont(10f),
                ForeColor = StudioDesignSystem.Frost,
                Text = "PLAYBACK VISUALIZER"
            };
            _status = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Top,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Muted,
                Height = 22,
                Text = "NO DOCUMENT"
            };
            _zoom = new TrackBar
            {
                AutoSize = false,
                BackColor = StudioDesignSystem.Deck,
                LargeChange = 2,
                Maximum = 250,
                Minimum = 75,
                Size = new Size(180, 30),
                TickStyle = TickStyle.None,
                Value = 100
            };
            _zoom.ValueChanged += delegate
            {
                _preview.NoteZoom = _zoom.Value / 100f;
            };

            // The arcade's chart effectors, as session-only toggles: scroll direction
            // re-projects the field, fader and line gate what each paint draws.
            _scroll = BuildEffectorCombo(new object[]
            {
                "OFF", "Reverse", "Left ALL", "Right ALL"
            });
            _scroll.SelectedIndexChanged += delegate
            {
                if (_scroll.SelectedIndex >= 0)
                {
                    _preview.ScrollDirection = (TechnikaScrollDirection)_scroll.SelectedIndex;
                }
            };
            _fader = BuildEffectorCombo(new object[]
            {
                "OFF", "FADE IN", "FADE IN 2", "FADE OUT", "FADE OUT 2"
            });
            _fader.SelectedIndexChanged += delegate
            {
                if (_fader.SelectedIndex >= 0)
                {
                    _preview.NoteFader = (TechnikaNoteFader)_fader.SelectedIndex;
                }
            };
            _line = BuildEffectorCombo(new object[]
            {
                "ON", "BLINK", "BLINK2", "BLIND"
            });
            _line.SelectedIndexChanged += delegate
            {
                if (_line.SelectedIndex >= 0)
                {
                    _preview.LineEffector = (TechnikaLineEffector)_line.SelectedIndex;
                }
            };

            // Chart scroll speed: BYTES charts carry theirs on Track 19 (read on
            // bind, combo locked to the detected value); PT and TECH charts take
            // the owner's pick here.
            _speed = BuildEffectorCombo(new object[]
            {
                "1/2", "1 (DEFAULT)", "2"
            });
            _speed.SelectedIndexChanged += delegate
            {
                if (_updatingSpeed || _speed.SelectedIndex < 0)
                {
                    return;
                }
                _manualSpeedIndex = _speed.SelectedIndex;
                _preview.ScrollSpeed = SpeedAtIndex(_speed.SelectedIndex);
            };

            // The sprite set persists: the combo lists the whole catalog (AUTO
            // first, arcade sources by arcade numbering, PACKAGED last) and the
            // "…" button points the probe at an install the automatic search did
            // not find. In the header flow it leads the row, before the note-size
            // slider and the session-only effectors.
            _spriteStyles = new List<TechnikaSpriteStyle>(TechnikaSpriteCatalog.Build());
            var spriteSetNames = new object[_spriteStyles.Count];
            for (int i = 0; i < _spriteStyles.Count; i++)
            {
                spriteSetNames[i] = _spriteStyles[i].DisplayName;
            }
            _spriteSet = BuildEffectorCombo(spriteSetNames);
            _spriteSet.SelectedIndexChanged += delegate
            {
                if (_updatingSpriteSet || _spriteSet.SelectedIndex < 0)
                {
                    return;
                }
                ApplySpriteStyle(_spriteStyles[_spriteSet.SelectedIndex]);
            };
            _browseSpriteRoot = StudioDesignSystem.CreateDeckButton("…");
            _browseSpriteRoot.Size = new Size(24, 21);
            _browseSpriteRoot.Click += delegate { BrowseSpriteRoot(); };

            var effectorsCaption = new Label
            {
                AutoSize = true,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Muted,
                Margin = new Padding(4, 16, -8, 0),
                Text = "EFFECTORS"
            };

            var effectorRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = true
            };
            // Sprite set first (it persists, unlike the session effectors), then
            // the note-size slider; the EFFECTORS caption gets a row of its own
            // above the arcade effector group, and the speed combo trails LINE.
            Control spriteSetUnit = BuildEffectorUnit(
                "SPRITE SET", _spriteSet, 126, 28, 35, _browseSpriteRoot);
            Control noteSizeUnit = BuildEffectorUnit("NOTE SIZE", _zoom, 180, 0, 44);
            effectorRow.Controls.Add(spriteSetUnit);
            effectorRow.Controls.Add(noteSizeUnit);
            effectorRow.SetFlowBreak(noteSizeUnit, true);
            effectorRow.Controls.Add(effectorsCaption);
            effectorRow.SetFlowBreak(effectorsCaption, true);
            effectorRow.Controls.Add(BuildEffectorUnit("TimeLine", _scroll));
            effectorRow.Controls.Add(BuildEffectorUnit("NOTE FADER", _fader));
            effectorRow.Controls.Add(BuildEffectorUnit("LINE", _line));
            effectorRow.Controls.Add(BuildEffectorUnit("SPEED", _speed));

            // Docking stacks children in reverse add order: title, then status,
            // then the wrapping effector row at the bottom of the header.
            header.Controls.Add(effectorRow);
            header.Controls.Add(_status);
            header.Controls.Add(title);
            SyncHeaderHeight(header, title, _status, effectorRow);
            EventHandler sync = delegate { SyncHeaderHeight(header, title, _status, effectorRow); };
            effectorRow.Resize += sync;
            title.Resize += sync;
            _status.Resize += sync;

            _preview = new GameplayPreviewControl();
            Controls.Add(_preview);
            Controls.Add(header);
            SelectSpriteStyle(TechnikaSpriteCatalog.Resolve(
                FeatureFlags.PreviewSpriteStyleId, _spriteStyles));
            SetProfile(GameplayPreviewProfile.Technika);
        }

        public EditorDocumentContext Document
        {
            get { return _preview.Document; }
        }

        public bool SupportsEditing
        {
            get { return false; }
        }

        public GameplayPreviewProfile Profile
        {
            get { return _preview.Profile; }
        }

        public void Bind(EditorDocumentContext document)
        {
            _preview.Bind(document);
            // The profile follows the chart: TECHNIKA-shaped data opens in the TECHNIKA
            // projection (PTFF included - no confirmation step), anything else stays on
            // the generic lanes through the guard inside SetProfile.
            SetProfile(document == null
                ? GameplayPreviewProfile.Technika
                : GameplayPreviewProfileResolver.Suggest(document.Model).Profile);
            ApplyScrollSpeed(document);
        }

        public void ConfirmTechnikaProfile()
        {
            SetProfile(GameplayPreviewProfile.Technika);
        }

        public void UseGenericProfile()
        {
            SetProfile(GameplayPreviewProfile.Generic);
        }

        public void RefreshPlayback()
        {
            if (!IsPlaybackVisible())
            {
                return;
            }

            long elapsed = _playbackClock.ElapsedMilliseconds;
            if (elapsed - _lastPlaybackFrameMilliseconds <
                PlaybackFrameIntervalMilliseconds)
            {
                return;
            }

            _lastPlaybackFrameMilliseconds = elapsed;
            _preview.RefreshPlayback();
            UpdateStatus();
        }

        public void RefreshPlaybackImmediately()
        {
            if (!IsPlaybackVisible())
            {
                return;
            }

            _lastPlaybackFrameMilliseconds = _playbackClock.ElapsedMilliseconds;
            _preview.RefreshPlayback();
            UpdateStatus();
        }

        public void RefreshTopology()
        {
            _preview.RefreshTopology();
            UpdateStatus();
        }

        /// <summary>
        /// Reads the chart's scroll speed. BYTES (Respect V trailer) charts declare
        /// theirs on Track 19: a note with attribute 1 plays at half speed, attribute 2
        /// at double speed, and an empty or missing marker keeps the default. Every
        /// other format answers 1 here because its speed is the owner's manual pick.
        /// </summary>
        internal static double DetectScrollSpeed(PlayerData model)
        {
            if (model == null || model.SourceFormat != ChartFormat.TrailerRespectV)
            {
                return 1.0;
            }
            TrackData speedTrack = null;
            foreach (TrackData track in model.Tracks)
            {
                if (track.Idx == 19)
                {
                    speedTrack = track;
                    break;
                }
            }
            if (speedTrack == null)
            {
                return 1.0;
            }
            foreach (EventData marker in speedTrack.Events)
            {
                if (marker.EventType != EventType.Note)
                {
                    continue;
                }
                if (marker.Attribute == 1)
                {
                    return 0.5;
                }
                if (marker.Attribute == 2)
                {
                    return 2.0;
                }
                return 1.0;
            }
            return 1.0;
        }

        private static double SpeedAtIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return 0.5;
                case 2:
                    return 2.0;
                default:
                    return 1.0;
            }
        }

        private static int IndexForSpeed(double speed)
        {
            if (speed < 0.75)
            {
                return 0;
            }
            return speed > 1.5 ? 2 : 1;
        }

        /// <summary>Applies the bound chart's speed rule: BYTES reads Track 19 and locks
        /// the combo on the detected value; PT and TECH charts unlock it and keep the
        /// owner's last manual pick.</summary>
        private void ApplyScrollSpeed(EditorDocumentContext document)
        {
            bool auto = document != null &&
                document.Model.SourceFormat == ChartFormat.TrailerRespectV;
            double speed = auto ? DetectScrollSpeed(document.Model) : 1.0;

            _updatingSpeed = true;
            try
            {
                _speed.Enabled = !auto;
                _speed.SelectedIndex = auto ? IndexForSpeed(speed) : _manualSpeedIndex;
            }
            finally
            {
                _updatingSpeed = false;
            }
            _preview.ScrollSpeed = auto ? speed : SpeedAtIndex(_manualSpeedIndex);
        }

        private void SetProfile(GameplayPreviewProfile profile)
        {
            if (profile == GameplayPreviewProfile.Technika &&
                _preview.Document != null &&
                GameplayPreviewProfileResolver.Suggest(_preview.Document.Model).Profile !=
                    GameplayPreviewProfile.Technika)
            {
                // The chart does not speak TECHNIKA note vocabulary (a BMS chart, an XML
                // chart): the two-way projection would misread its tracks, so the generic
                // lanes stay in force no matter what the caller asked for.
                profile = GameplayPreviewProfile.Generic;
            }
            _preview.SetProfile(profile);
            UpdateStatus();
        }

        /// <summary>The dropdown changed because the owner picked a style: persist the id
        /// and hand it to the preview, which resolves it against a fresh catalog.</summary>
        private void ApplySpriteStyle(TechnikaSpriteStyle style)
        {
            FeatureFlags.SetPreviewSpriteStyleId(style.Id);
            _preview.SpriteStyleId = style.Id;
            UpdateStatus();
        }

        /// <summary>Shows the folder picker for the Technika 3 data, stores the resolved
        /// MainGame root (or the raw pick when it is not recognisable, so a repair later
        /// still has what the owner meant), and rebuilds the catalog so the new source
        /// appears in the dropdown immediately.</summary>
        private void BrowseSpriteRoot()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Locate the DJMax Technika 3 MainGame folder " +
                    "(the one containing 'note' and 'CoolBomb').";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string root = TechnikaSpriteCatalog.ResolveMainGameRoot(dialog.SelectedPath);
                FeatureFlags.SetTechnika3AssetPath(root ?? dialog.SelectedPath);
            }
            RebuildSpriteCatalog();
        }

        /// <summary>Rebuilds the catalog after the asset path changed and re-points the
        /// dropdown at the still-current style, falling back the same way an unknown
        /// persisted id does when the style no longer exists. The fallback is displayed
        /// but not persisted: what the owner chose stays chosen, and repairs itself if
        /// the data ever comes back.</summary>
        private void RebuildSpriteCatalog()
        {
            string currentId = _spriteSet.SelectedIndex >= 0
                ? _spriteStyles[_spriteSet.SelectedIndex].Id
                : FeatureFlags.PreviewSpriteStyleId;
            _spriteStyles = new List<TechnikaSpriteStyle>(TechnikaSpriteCatalog.Build());
            TechnikaSpriteStyle resolved = TechnikaSpriteCatalog.Resolve(currentId, _spriteStyles);

            _updatingSpriteSet = true;
            try
            {
                _spriteSet.BeginUpdate();
                _spriteSet.Items.Clear();
                for (int i = 0; i < _spriteStyles.Count; i++)
                {
                    _spriteSet.Items.Add(_spriteStyles[i].DisplayName);
                }
                _spriteSet.SelectedIndex = IndexOfSpriteStyle(resolved.Id);
                _spriteSet.EndUpdate();
            }
            finally
            {
                _updatingSpriteSet = false;
            }

            _preview.SpriteStyleId = resolved.Id;
            UpdateStatus();
        }

        /// <summary>Points the dropdown at one style without persisting anything: startup
        /// selection shows what the settings file asked for (already resolved through the
        /// catalog by the caller) but only an owner action writes settings.</summary>
        private void SelectSpriteStyle(TechnikaSpriteStyle style)
        {
            int index = IndexOfSpriteStyle(style.Id);
            if (index < 0)
            {
                return;
            }
            _updatingSpriteSet = true;
            try
            {
                _spriteSet.SelectedIndex = index;
            }
            finally
            {
                _updatingSpriteSet = false;
            }
            _preview.SpriteStyleId = style.Id;
        }

        private int IndexOfSpriteStyle(string id)
        {
            for (int i = 0; i < _spriteStyles.Count; i++)
            {
                if (string.Equals(_spriteStyles[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateStatus()
        {
            if (_preview.Document == null)
            {
                _status.Text = "NO DOCUMENT";
                return;
            }
            string source = _preview.Profile == GameplayPreviewProfile.Technika
                ? "  |  " + _preview.SpriteSourceLabel
                : string.Empty;
            _status.Text = _preview.ProjectionStatus +
                "  |  TICK " + _preview.CurrentTick +
                source +
                (_preview.DiagnosticCount == 0
                    ? string.Empty
                    : "  |  " + _preview.DiagnosticCount + " WARNING(S)");
            _status.ForeColor = _preview.DiagnosticCount == 0
                ? StudioDesignSystem.Muted
                : StudioDesignSystem.SignalAmber;
        }

        private bool IsPlaybackVisible()
        {
            return !IsDisposed && Visible && _preview.Visible;
        }

        /// <summary>The flow panel wraps when the docked window is narrow; the
        /// header follows its height so no row is cut off.</summary>
        private static void SyncHeaderHeight(Panel header, Control title, Control status, Control effectorRow)
        {
            int height = header.Padding.Vertical +
                title.Height +
                status.Height +
                effectorRow.Height;
            if (header.Height != height)
            {
                header.Height = height;
            }
        }

        /// <summary>A caption glued above its editor control, so the flow panel
        /// wraps each pair as one unit instead of separating label from combo.</summary>
        private static Control BuildEffectorUnit(
            string caption,
            Control editor,
            int editorWidth = 126,
            int extraRight = 0,
            int unitHeight = 35,
            Control extra = null)
        {
            var unit = new Panel
            {
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 12, 2),
                Size = new Size(editorWidth + extraRight + (extra != null ? extra.Width + 2 : 0), unitHeight)
            };
            var label = new Label
            {
                AutoSize = true,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Muted,
                Location = new Point(0, 0),
                Text = caption
            };
            editor.Location = new Point(0, 14);
            if (editorWidth > 0 && editor.Width != editorWidth)
            {
                editor.Width = editorWidth;
            }
            unit.Controls.Add(label);
            unit.Controls.Add(editor);
            if (extra != null)
            {
                extra.Location = new Point(editor.Width + 2, 14);
                unit.Controls.Add(extra);
            }
            return unit;
        }

        private static ComboBox BuildEffectorCombo(object[] items)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = StudioDesignSystem.UtilityFont(8f),
                Size = new Size(126, 21)
            };
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
            return combo;
        }
    }
}
