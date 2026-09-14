using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.PropertyLayer
{
    public abstract class PropertiesLayerBase
    {
        protected EventData _eventData;

        public PropertiesLayerBase (EventData eventData)
        {
            _eventData = eventData;
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
                _eventData.Tick = value;
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
                _eventData.VirtualTick = value;
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
                _eventData.VirtualDuration = value;
            }
        }
    }
}
