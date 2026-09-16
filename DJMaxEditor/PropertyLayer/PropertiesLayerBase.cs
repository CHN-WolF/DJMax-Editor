using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    public abstract class PropertiesLayerBase
    {
        protected EventData _eventData;
        protected readonly ChartEditController _edits;

        public PropertiesLayerBase (EventData eventData, ChartEditController edits = null)
        {
            _eventData = eventData;
            _edits = edits;
        }

        /// <summary>
        /// Determine the current position of the event
        /// </summary>
        [Category("Base")]
        [DisplayName("Position")]
        public int Position {
            get {
                return _eventData.Tick;
            }
            set {
                if (_edits != null)
                {
                    _edits.MoveSelection(0, value * EventData.VirtualTickSize - _eventData.VirtualTick);
                }
                else
                {
                    _eventData.Tick = value;
                }
            }
        }

        /// <summary>
        /// Virtual tick position of the event
        /// </summary>
        [Category("Base")]
        [DisplayName("Virtual tick")]
        public int VirtualTick {
            get {
                return _eventData.VirtualTick;
            }
            set {
                if (_edits != null)
                {
                    _edits.MoveSelection(0, value - _eventData.VirtualTick);
                }
                else
                {
                    _eventData.VirtualTick = value;
                }
            }
        }

        /// <summary>
        /// Virtual duration of the event
        /// </summary>
        [Category("Base")]
        [DisplayName("Virtual duration")]
        public ushort VirtualDuration {
            get {
                return _eventData.VirtualDuration;
            }
            set {
                if (_edits != null)
                {
                    _edits.ResizeSelection(value - _eventData.VirtualDuration);
                }
                else
                {
                    _eventData.VirtualDuration = value;
                }
            }
        }
    }
}
