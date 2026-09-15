using System;
using System.Collections.Generic;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    /// <summary>
    /// Inspector wrapper for a multi-note selection. Getters return the value shared
    /// by every selected note (falling back to the first note when values differ);
    /// setters commit one undoable edit for the whole selection.
    /// </summary>
    public class MultiNotePropertiesLayer
    {
        private readonly IReadOnlyList<EventData> _items;
        private readonly ChartEditController _edits;

        public MultiNotePropertiesLayer(IReadOnlyList<EventData> items, ChartEditController edits)
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

        [DisplayName("Attribute")]
        public EventAttribute Attribute {
            get {
                return (EventAttribute)CommonValue(item => item.Attribute);
            }
            set {
                _edits.SetSelectionAttribute((byte)value);
            }
        }

        [DisplayName("Attr")]
        public byte Attr {
            get {
                return (byte)CommonValue(item => item.Attribute);
            }
            set {
                _edits.SetSelectionAttribute(value);
            }
        }

        /// <summary>
        /// Duration of the selected notes
        /// </summary>
        [DisplayName("Duration")]
        public ushort Duration {
            get {
                return (ushort)CommonValue(item => item.Duration);
            }
            set {
                _edits.SetSelectionDuration(value);
            }
        }

        [DisplayName("Vel")]
        public byte Vel {
            get {
                return (byte)CommonValue(item => item.Vel);
            }
            set {
                _edits.SetSelectionVel(value);
            }
        }

        [DisplayName("Pan")]
        public byte Pan {
            get {
                return (byte)CommonValue(item => item.Pan);
            }
            set {
                _edits.SetSelectionPan(value);
            }
        }

        [DisplayName("Volume")]
        public byte Volume {
            get {
                return (byte)CommonValue(item => item.Volume);
            }
            set {
                _edits.SetSelectionVolume(value);
            }
        }

        [DisplayName("Sound")]
        public string Sound {
            get {
                InstrumentData first = _items[0].Instrument;
                for (int i = 1; i < _items.Count; i++)
                {
                    if (!object.ReferenceEquals(first, _items[i].Instrument))
                    {
                        return "(mixed)";
                    }
                }
                return first == null ? "Nothing" : first.Name;
            }
        }

        private int CommonValue(Func<EventData, int> reader)
        {
            // When every selected note shares the value it is the common value;
            // when they differ the first note's value is shown instead.
            return reader(_items[0]);
        }
    }
}
