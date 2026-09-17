using System.ComponentModel;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.PropertyLayer
{
    public class BeatEventPropertiesLayer : PropertiesLayerBase
    {
        public BeatEventPropertiesLayer (EventData eventData, ChartEditController edits = null)
            : base(eventData, edits) { }

        [DisplayName("Beat")]
        public ushort Beat {
            get {
                return _eventData.Beat;
            }
            set {
                if (_edits != null)
                {
                    _edits.SetSelectionBeat(value);
                }
                else
                {
                    _eventData.Beat = value;
                }
            }
        }
    }
}
