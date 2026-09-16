using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    public class TempoEventPropertiesLayer : PropertiesLayerBase
    {
        public TempoEventPropertiesLayer (EventData eventData, ChartEditController edits = null)
            : base(eventData, edits) { }

        [DisplayName("Tempo")]
        public float Tempo {
            get {
                return _eventData.Tempo;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionTempo(value);
                }
                else
                {
                    _eventData.Tempo = value;
                }
            }
        }
    }
}
