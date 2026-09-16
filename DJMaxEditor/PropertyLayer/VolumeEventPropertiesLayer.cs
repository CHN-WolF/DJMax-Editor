using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    public class VolumeEventPropertiesLayer : PropertiesLayerBase
    {
        public VolumeEventPropertiesLayer (EventData eventData, ChartEditController edits = null)
            : base(eventData, edits)
        {
            EventData = eventData;
        }

        [DisplayName("Volume")]
        public byte Volume {
            get {
                return _eventData.Volume;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionVolume(value);
                }
                else
                {
                    _eventData.Volume = value;
                }
            }
        }

        [Browsable(false)]
        public EventData EventData { get; }
    }
}
