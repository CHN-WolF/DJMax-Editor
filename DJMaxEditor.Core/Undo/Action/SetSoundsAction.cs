using System.Collections.Generic;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Undo.Action
{
    public sealed class SetSoundsAction : UndoRedoAction
    {
        public sealed class EventSoundChange
        {
            public EventSoundChange(EventData item, InstrumentData previous, InstrumentData next)
            {
                Item = item;
                Previous = previous;
                Next = next;
            }

            public EventData Item { get; private set; }
            public InstrumentData Previous { get; private set; }
            public InstrumentData Next { get; private set; }
        }

        private readonly List<EventSoundChange> _changes;

        public SetSoundsAction(IEnumerable<EventSoundChange> changes)
        {
            _changes = changes == null
                ? new List<EventSoundChange>()
                : new List<EventSoundChange>(changes);
            Cancel = _changes.Count == 0;
        }

        public override void Undo()
        {
            Apply(false);
        }

        public override void Redo()
        {
            Apply(true);
        }

        private void Apply(bool forward)
        {
            foreach (EventSoundChange change in _changes)
            {
                change.Item.Instrument = forward ? change.Next : change.Previous;
            }
        }
    }
}
