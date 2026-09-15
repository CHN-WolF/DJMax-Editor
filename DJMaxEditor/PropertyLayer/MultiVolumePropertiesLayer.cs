using System;
using System.Collections.Generic;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    /// <summary>
    /// Inspector wrapper for a multi-volume-event selection. The getter returns the
    /// first event's value; the setter commits one undoable edit for the whole selection.
    /// </summary>
    public class MultiVolumePropertiesLayer
    {
        private readonly IReadOnlyList<EventData> _items;
        private readonly ChartEditController _edits;

        public MultiVolumePropertiesLayer(IReadOnlyList<EventData> items, ChartEditController edits)
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

        [DisplayName("Volume")]
        public byte Volume {
            get {
                return _items[0].Volume;
            }
            set {
                _edits.SetSelectionVolume(value);
            }
        }
    }
}
