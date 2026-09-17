using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    public class NoteEventPropertiesLayer : PropertiesLayerBase
    {
        public NoteEventPropertiesLayer (EventData eventData, ChartEditController edits = null)
            : base(eventData, edits)
        { }

        /// <summary>
        /// Duration of the current event
        /// </summary>
        [DisplayName("Duration")]
        public ushort Duration {
            get {
                return _eventData.Duration;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionDuration(value);
                }
                else
                {
                    _eventData.Duration = value;
                }
            }
        }

        /// <summary>
        /// Pan for this event
        /// </summary>
        [DisplayName("Pan")]
        public byte Pan {
            get {
                return _eventData.Pan;
            }

            set {
                if (_edits != null)
                {
                    _edits.SetSelectionPan(value);
                }
                else
                {
                    _eventData.Pan = value;
                }
            }
        }

        [DisplayName("Attribute")]
        public EventAttribute Attribute {
            get {
                return (EventAttribute)_eventData.Attribute;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionAttribute((byte)value);
                }
                else
                {
                    _eventData.Attribute = (byte)value;
                }
            }
        }

        [DisplayName("Attr")]
        public byte Attr {
            get {
                return _eventData.Attribute;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionAttribute(value);
                }
                else
                {
                    _eventData.Attribute = value;
                }
            }
        }

        [DisplayName("Sound")]
        public string Sound {
            get {
                InstrumentData instData = _eventData.Instrument;
                return instData == null ? "Nothing" : instData.Name;
            }
        }

        [DisplayName("Instrument")]
        public ushort Instrument {
            get {
                var instrument = _eventData.Instrument;

                if (null == instrument)
                {
                    return 0;
                }

                return instrument.InsNum;
            }
        }

        [DisplayName("Track")]
        public uint Track {
            get {
                return _eventData.TrackId;
            }
        }

        [DisplayName("Vel")]
        public byte Vel {
            get {
                return _eventData.Vel;
            }
            set
            {
                if (_edits != null)
                {
                    _edits.SetSelectionVel(value);
                }
                else
                {
                    _eventData.Vel = value;
                }
            }
        }

    }
}
