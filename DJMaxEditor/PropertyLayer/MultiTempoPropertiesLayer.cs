using System;
using System.Collections.Generic;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    /// <summary>
    /// Inspector wrapper for a multi-tempo-event selection. The getter returns the
    /// first event's value; the setter commits one undoable edit for the whole selection.
    /// </summary>
    public class MultiTempoPropertiesLayer
    {
        private readonly IReadOnlyList<EventData> _items;
        private readonly ChartEditController _edits;

        public MultiTempoPropertiesLayer(IReadOnlyList<EventData> items, ChartEditController edits)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentNullException("items");
            }
            if (edits == null)
            {
                throw new ArgumentNullException("edits");
            }
            _items = items;
            _edits = edits;
        }

        [DisplayName("Count")]
        public int Count {
            get {
                return _items.Count;
            }
        }

        [DisplayName("Tempo")]
        public float Tempo {
            get {
                return _items[0].Tempo;
            }
            set {
                _edits.SetSelectionTempo(value);
            }
        }
    }
}
