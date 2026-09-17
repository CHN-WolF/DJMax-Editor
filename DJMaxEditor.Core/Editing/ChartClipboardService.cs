using System.Collections.Generic;
using System.Collections.ObjectModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Editor
{
    /// <summary>
    /// In-memory chart clipboard. It never serializes events or touches the system clipboard.
    /// The copied events are stored application-wide, so a selection copied in one
    /// document can be pasted into any other open document.
    /// </summary>
    public sealed class ChartClipboardService
    {
        private sealed class SharedClipboardStore
        {
            public readonly List<EventData> Templates = new List<EventData>();
            public int BaseTick;
            public EditorDocumentContext SourceDocument;
        }

        private static readonly SharedClipboardStore _shared = new SharedClipboardStore();

        private readonly EditorDocumentContext _document;

        internal ChartClipboardService(EditorDocumentContext document)
        {
            _document = document;
        }

        public bool HasEvents
        {
            get { return _shared.Templates.Count > 0; }
        }

        public bool CopySelection()
        {
            if (_document.Selection.Count == 0)
            {
                return false;
            }

            _shared.Templates.Clear();
            _shared.BaseTick = int.MaxValue;
            _shared.SourceDocument = _document;
            foreach (EventData selected in _document.Selection.Items)
            {
                var clone = (EventData)selected.Clone();
                _shared.Templates.Add(clone);
                if (clone.VirtualTick < _shared.BaseTick)
                {
                    _shared.BaseTick = clone.VirtualTick;
                }
            }
            return true;
        }

        public bool CutSelection()
        {
            return CopySelection() && _document.Edits.DeleteSelection();
        }

        public IList<EventData> DuplicateSelection(int virtualTickOffset)
        {
            if (_document.Selection.Count == 0 || virtualTickOffset <= 0)
            {
                return new ReadOnlyCollection<EventData>(new List<EventData>());
            }

            int earliestTick = int.MaxValue;
            foreach (EventData selected in _document.Selection.Items)
            {
                earliestTick = System.Math.Min(earliestTick, selected.VirtualTick);
            }

            if (!CopySelection())
            {
                return new ReadOnlyCollection<EventData>(new List<EventData>());
            }
            return PasteAt(earliestTick + virtualTickOffset);
        }

        public IList<EventData> PasteAt(int destinationVirtualTick)
        {
            if (!HasEvents || destinationVirtualTick < 0)
            {
                return new ReadOnlyCollection<EventData>(new List<EventData>());
            }

            long lastTrack = (long)_document.Model.Tracks.Count - 1;
            // Same-document paste lands at the requested tick; pasting into a
            // different document keeps the events at their original positions.
            bool foreignDocument = !object.ReferenceEquals(_shared.SourceDocument, _document);
            var events = new List<EventData>();
            foreach (EventData template in _shared.Templates)
            {
                var clone = (EventData)template.Clone();
                long tick = foreignDocument
                    ? template.VirtualTick
                    : (long)destinationVirtualTick + (template.VirtualTick - _shared.BaseTick);
                if (tick < 0 || tick > int.MaxValue)
                {
                    return new ReadOnlyCollection<EventData>(new List<EventData>());
                }
                clone.VirtualTick = (int)tick;
                if (lastTrack >= 0 && clone.TrackId > (uint)lastTrack)
                {
                    clone.TrackId = (uint)lastTrack;
                }
                clone.Instrument = ResolveInstrument(template.Instrument);
                events.Add(clone);
            }

            if (!_document.Edits.AddEvents(events))
            {
                return new ReadOnlyCollection<EventData>(new List<EventData>());
            }
            return new ReadOnlyCollection<EventData>(events);
        }

        // Pasted notes must reference instruments owned by the destination document:
        // reuse the instrument with the same InsNum when it exists, otherwise add it.
        private InstrumentData ResolveInstrument(InstrumentData source)
        {
            if (source == null)
            {
                return null;
            }

            var instruments = _document.Model.Instruments;
            for (int i = 0; i < instruments.Count; i++)
            {
                InstrumentData existing = instruments[i];
                if (existing != null && existing.InsNum == source.InsNum)
                {
                    return existing;
                }
            }

            var created = new InstrumentData { InsNum = source.InsNum, Name = source.Name };
            instruments.Add(created);
            return created;
        }
    }
}
