using System.Collections.Generic;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Undo.Action
{
    public sealed class SetEventPropertiesAction : UndoRedoAction
    {
        public enum EventPropertyKind
        {
            Volume,
            Vel,
            Pan,
            Duration,
            Tempo,
            Beat
        }

        public sealed class EventPropertyChange
        {
            public EventPropertyChange(EventData item, EventPropertyKind property, float previous, float next)
            {
                Item = item;
                Property = property;
                Previous = previous;
                Next = next;
            }

            public EventData Item { get; private set; }
            public EventPropertyKind Property { get; private set; }
            public float Previous { get; private set; }
            public float Next { get; private set; }
        }

        private readonly List<EventPropertyChange> _changes;

        public SetEventPropertiesAction(IEnumerable<EventPropertyChange> changes)
        {
            _changes = changes == null
                ? new List<EventPropertyChange>()
                : new List<EventPropertyChange>(changes);
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
            foreach (EventPropertyChange change in _changes)
            {
                float value = forward ? change.Next : change.Previous;
                switch (change.Property)
                {
                    case EventPropertyKind.Volume:
                        change.Item.Volume = (byte)value;
                        break;
                    case EventPropertyKind.Vel:
                        change.Item.Vel = (byte)value;
                        break;
                    case EventPropertyKind.Pan:
                        change.Item.Pan = (byte)value;
                        break;
                    case EventPropertyKind.Duration:
                        change.Item.Duration = (ushort)value;
                        break;
                    case EventPropertyKind.Tempo:
                        change.Item.Tempo = value;
                        break;
                    case EventPropertyKind.Beat:
                        change.Item.Beat = (ushort)value;
                        break;
                }
            }
        }
    }
}
