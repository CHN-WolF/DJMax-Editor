using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.PropertyLayer;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunSharedEditingTests()
        {
            Test("ChartSelection_UsesIdentityAndRaisesOneChangePerOperation", () =>
            {
                var first = new EventData { VirtualTick = 12, TrackId = 0 };
                var second = new EventData { VirtualTick = 24, TrackId = 0 };
                var selection = new ChartSelectionService();
                int changes = 0;
                selection.SelectionChanged += delegate { changes++; };

                selection.Replace(new[] { first, first, second });

                AssertTrue(selection.Count == 2, "selection should contain each event identity once");
                AssertTrue(object.ReferenceEquals(selection.Items[0], first),
                    "selection must preserve authoritative event identity");
                AssertTrue(object.ReferenceEquals(selection.Items[1], second),
                    "selection order should follow the requested logical selection");
                AssertTrue(changes == 1, "replace should emit one logical selection change");
            });

            Test("ChartEdit_MoveSelectionIsAtomicAndUndoable", () =>
            {
                var model = EditingModel(2);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 1, 24);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                bool rejected = context.Edits.MoveSelection(1, 6);

                AssertTrue(!rejected, "group move should reject when any destination track is invalid");
                AssertTrue(first.TrackId == 0 && first.VirtualTick == 12,
                    "rejected group move must not partially move the first event");
                AssertTrue(second.TrackId == 1 && second.VirtualTick == 24,
                    "rejected group move must leave every event unchanged");
                AssertTrue(!undo.CanUndo, "rejected move must not create an undo entry");

                bool moved = context.Edits.MoveSelection(0, 6);

                AssertTrue(moved, "valid group move should commit");
                AssertTrue(first.VirtualTick == 18 && second.VirtualTick == 30,
                    "valid group move should preserve relative timing");
                AssertTrue(undo.CanUndo, "one logical move should create one undo entry");

                undo.Undo();
                AssertTrue(first.VirtualTick == 12 && second.VirtualTick == 24,
                    "undo should restore every moved event");
            });

            Test("ChartEdit_DeleteSelectionClearsSelectionAndUndoRestoresEvents", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                bool deleted = context.Edits.DeleteSelection();

                AssertTrue(deleted, "editable selection should delete");
                AssertTrue(!model.Tracks.GetTrackAtIndex(0).Events.Any(),
                    "delete should remove the complete logical selection");
                AssertTrue(context.Selection.Count == 0, "delete should clear shared selection");

                undo.Undo();
                AssertTrue(model.Tracks.GetTrackAtIndex(0).Events.Count() == 2,
                    "one undo should restore the complete logical deletion");
            });

            Test("ChartEdit_RespectsDocumentCapabilities", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                model.IsReadOnly = true;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "locked.pt", undo);
                context.Selection.Replace(new[] { item });

                bool moved = context.Edits.MoveSelection(0, 6);
                bool deleted = context.Edits.DeleteSelection();

                AssertTrue(!moved && !deleted, "read-only capabilities must block shared mutations");
                AssertTrue(item.VirtualTick == 12, "blocked move must preserve event timing");
                AssertTrue(model.Tracks.GetTrackAtIndex(0).Events.Contains(item),
                    "blocked delete must preserve the event");
                AssertTrue(!undo.CanUndo, "blocked commands must not create undo entries");
            });

            Test("ChartEdit_CreateUsesCloneAndOneUndoEntry", () =>
            {
                var model = EditingModel(2);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                var template = new EventData
                {
                    EventType = EventType.Note,
                    Attribute = 10,
                    VirtualDuration = 42
                };

                EventData created = context.Edits.CreateEvent(template, 1, 120);

                AssertTrue(created != null, "valid creation should return the authoritative new event");
                AssertTrue(!object.ReferenceEquals(created, template),
                    "creation must clone the palette/template event");
                AssertTrue(created.TrackId == 1 && created.VirtualTick == 120,
                    "created event should use the requested track and exact virtual tick");
                AssertTrue(created.Attribute == 10 && created.VirtualDuration == 42,
                    "creation should preserve event semantics from the template");
                AssertTrue(context.Selection.Count == 1 &&
                    object.ReferenceEquals(context.Selection.Items[0], created),
                    "new event should become the shared selection");

                undo.Undo();
                AssertTrue(!model.Tracks.GetTrackAtIndex(1).Events.Contains(created),
                    "one undo should remove the created event");
            });

            Test("ChartClipboard_PastePreservesRelativeTimingAndIsUndoable", () =>
            {
                var model = EditingModel(3);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 2, 30);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                AssertTrue(context.Clipboard.CopySelection(), "copy should capture an existing selection");
                IList<EventData> pasted = context.Clipboard.PasteAt(120);

                AssertTrue(pasted.Count == 2, "paste should create every copied event");
                AssertTrue(pasted[0].VirtualTick == 120 && pasted[1].VirtualTick == 138,
                    "paste should preserve the selection's relative timing");
                AssertTrue(pasted[0].TrackId == 0 && pasted[1].TrackId == 2,
                    "paste should preserve track relationships");
                AssertTrue(!object.ReferenceEquals(pasted[0], first) &&
                    !object.ReferenceEquals(pasted[1], second),
                    "paste must create new authoritative event identities");
                AssertTrue(context.Selection.Count == 2 &&
                    object.ReferenceEquals(context.Selection.Items[0], pasted[0]),
                    "pasted events should become the shared selection");

                undo.Undo();
                AssertTrue(model.Tracks.Events.Length == 2,
                    "one undo should remove the complete pasted group");
            });

            Test("ChartEdit_ResizeSelectionRejectsInvalidGroupAtomically", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                first.VirtualDuration = 12;
                second.VirtualDuration = 6;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                bool rejected = context.Edits.ResizeSelection(-12);

                AssertTrue(!rejected, "resize should reject when any selected duration would be invalid");
                AssertTrue(first.VirtualDuration == 12 && second.VirtualDuration == 6,
                    "rejected resize must not partially mutate the group");
                AssertTrue(!undo.CanUndo, "rejected resize must not create an undo entry");

                bool resized = context.Edits.ResizeSelection(6);
                AssertTrue(resized, "valid group resize should commit");
                AssertTrue(first.VirtualDuration == 18 && second.VirtualDuration == 12,
                    "valid resize should apply the same duration delta");

                undo.Undo();
                AssertTrue(first.VirtualDuration == 12 && second.VirtualDuration == 6,
                    "undo should restore every duration");
            });

            Test("TimelineInteraction_CancelReturnsToSelectWithoutCommit", () =>
            {
                var interaction = new TimelineInteractionState();
                interaction.Tool = TimelineTool.Draw;

                bool began = interaction.Begin(
                    TimelineInteractionKind.Creating,
                    new TimelineInteractionAnchor(120, 2));
                bool cancelled = interaction.Cancel();

                AssertTrue(began && cancelled, "active creation should be cancellable");
                AssertTrue(interaction.Kind == TimelineInteractionKind.Idle,
                    "cancel should cleanly return the interaction to idle");
                AssertTrue(interaction.Tool == TimelineTool.Select,
                    "escape-style cancellation should return to the Select tool");
                AssertTrue(!interaction.HasPendingMutation,
                    "cancelled interaction must not retain a pending mutation");
            });

            Test("ChartEdit_RepeatedGestureMovesCollapseIntoOneUndoEntry", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { item });
                object gesture = new object();

                AssertTrue(context.Edits.MoveSelection(0, 6, gesture), "first drag step failed");
                AssertTrue(context.Edits.MoveSelection(0, 6, gesture), "second drag step failed");
                AssertTrue(item.VirtualTick == 24, "drag steps did not reach the preview destination");

                undo.Undo();
                AssertTrue(item.VirtualTick == 12,
                    "one undo should restore the position before the complete gesture");
                undo.Redo();
                AssertTrue(item.VirtualTick == 24,
                    "one redo should restore the final gesture destination");
            });

            Test("ChartClipboard_CutAndDuplicateUseSharedUndoableEdits", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { item });

                AssertTrue(context.Clipboard.CutSelection(), "cut should copy and delete selection");
                AssertTrue(!model.Tracks.Events.Any(), "cut did not remove the selected event");
                undo.Undo();
                AssertTrue(model.Tracks.Events.Single() == item, "undo did not restore the cut event");

                context.Selection.Replace(new[] { item });
                IList<EventData> duplicated = context.Clipboard.DuplicateSelection(6);
                AssertTrue(duplicated.Count == 1, "duplicate did not create one event");
                AssertTrue(duplicated[0].VirtualTick == 18, "duplicate offset is incorrect");
                AssertTrue(!object.ReferenceEquals(duplicated[0], item),
                    "duplicate reused the authoritative source event identity");
            });

            Test("ChartClipboard_PastesAcrossDocumentsWithInstrumentRemap", () =>
            {
                var sourceModel = EditingModel(1);
                var sourceSound = new InstrumentData { InsNum = 7, Name = "kick.wav" };
                sourceModel.Instruments.Add(sourceSound);
                var sourceItem = AddEditingEvent(sourceModel, 0, 12);
                sourceItem.Instrument = sourceSound;
                var source = new EditorDocumentContext(sourceModel, "source.pt", new UndoManager());
                source.Selection.Replace(new[] { sourceItem });

                var targetModel = EditingModel(1);
                var targetSound = new InstrumentData { InsNum = 7, Name = "target-kick.wav" };
                targetModel.Instruments.Add(targetSound);
                var targetUndo = new UndoManager();
                var target = new EditorDocumentContext(targetModel, "target.pt", targetUndo);

                AssertTrue(source.Clipboard.CopySelection(), "copy should commit");
                IList<EventData> pasted = target.Clipboard.PasteAt(120);
                AssertTrue(pasted.Count == 1, "cross-document paste did not insert the event");
                AssertTrue(pasted[0].VirtualTick == 12,
                    "cross-document paste must keep the event at its original position");
                AssertTrue(object.ReferenceEquals(pasted[0].Instrument, targetSound),
                    "paste must rebind the instrument owned by the destination document");
                AssertTrue(targetModel.Tracks.GetTrackAtIndex(0).Events.Contains(pasted[0]),
                    "pasted event was not added to the destination model");

                targetUndo.Undo();
                AssertTrue(!targetModel.Tracks.GetTrackAtIndex(0).Events.Any(),
                    "one undo should remove the cross-document paste");
            });

            Test("ChartClipboard_PasteAddsMissingInstrumentAndClampsTrack", () =>
            {
                var sourceModel = EditingModel(3);
                var sourceSound = new InstrumentData { InsNum = 9, Name = "snare.wav" };
                sourceModel.Instruments.Add(sourceSound);
                var sourceItem = AddEditingEvent(sourceModel, 2, 12);
                sourceItem.Instrument = sourceSound;
                var source = new EditorDocumentContext(sourceModel, "source.pt", new UndoManager());
                source.Selection.Replace(new[] { sourceItem });

                var targetModel = EditingModel(1);
                var target = new EditorDocumentContext(targetModel, "target.pt", new UndoManager());

                AssertTrue(source.Clipboard.CopySelection(), "copy should commit");
                IList<EventData> pasted = target.Clipboard.PasteAt(48);
                AssertTrue(pasted.Count == 1, "paste into a narrower chart was rejected");
                AssertTrue(pasted[0].TrackId == 0,
                    "out-of-range track was not clamped to the destination tracks");
                AssertTrue(pasted[0].Instrument != null &&
                    !object.ReferenceEquals(pasted[0].Instrument, sourceSound),
                    "missing instrument must be recreated inside the destination document");
                AssertTrue(pasted[0].Instrument.InsNum == 9 &&
                    pasted[0].Instrument.Name == "snare.wav",
                    "recreated instrument lost its source identity");
                AssertTrue(targetModel.Instruments.Contains(pasted[0].Instrument),
                    "recreated instrument was not added to the destination model");
            });

            Test("ChartEdit_InspectorAttributeMutationIsGroupedAndUndoable", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                first.Attribute = 1;
                second.Attribute = 2;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                AssertTrue(context.Edits.SetSelectionAttribute(9),
                    "valid Inspector attribute edit should commit");
                AssertTrue(first.Attribute == 9 && second.Attribute == 9,
                    "Inspector edit did not update the complete shared selection");

                undo.Undo();
                AssertTrue(first.Attribute == 1 && second.Attribute == 2,
                    "one undo should restore each event's original attribute");
            });

            Test("ChartEdit_NoOpInspectorMutationsDoNotCreateUndoEntries", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                item.Attribute = 4;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { item });

                AssertTrue(!context.Edits.MoveSelection(0, 0),
                    "zero-distance move should report no mutation");
                AssertTrue(!context.Edits.ResizeSelection(0),
                    "zero-duration resize should report no mutation");
                AssertTrue(!context.Edits.SetSelectionAttribute(4),
                    "assigning the current attribute should report no mutation");
                AssertTrue(!undo.CanUndo,
                    "no-op Inspector validation created an undo entry");
            });

            Test("ChartEdit_RepeatedResizeGestureCollapsesIntoOneUndoEntry", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                item.VirtualDuration = 12;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { item });
                object gesture = new object();
                MethodInfo groupedResize = typeof(ChartEditController).GetMethod(
                    "ResizeSelection",
                    new[] { typeof(int), typeof(object) });

                AssertTrue(groupedResize != null,
                    "shared edit controller does not expose grouped resize gestures");
                AssertTrue((bool)groupedResize.Invoke(context.Edits, new[] { (object)6, gesture }),
                    "first resize step failed");
                AssertTrue((bool)groupedResize.Invoke(context.Edits, new[] { (object)6, gesture }),
                    "second resize step failed");
                AssertTrue(item.VirtualDuration == 24,
                    "resize gesture did not reach its preview duration");

                undo.Undo();
                AssertTrue(item.VirtualDuration == 12,
                    "one undo should restore the duration before the complete gesture");
                undo.Redo();
                AssertTrue(item.VirtualDuration == 24,
                    "one redo should restore the final gesture duration");
            });

            Test("TimelineInteraction_NotifiesToolChangesIncludingCancel", () =>
            {
                var interaction = new TimelineInteractionState();
                EventInfo toolChanged = typeof(TimelineInteractionState).GetEvent("ToolChanged");
                int notifications = 0;
                EventHandler handler = delegate { notifications++; };

                AssertTrue(toolChanged != null,
                    "timeline interaction state does not publish tool changes to shell controls");
                toolChanged.AddEventHandler(interaction, handler);
                interaction.Tool = TimelineTool.Draw;
                interaction.Cancel();

                AssertTrue(interaction.Tool == TimelineTool.Select,
                    "cancel did not restore the Select tool");
                AssertTrue(notifications == 2,
                    "tool change and cancel did not both notify the shell");
            });

            Test("ChartEdit_InspectorBatchPropertiesAreGroupedAndUndoable", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                first.Vel = 10;
                second.Vel = 20;
                first.Pan = 30;
                second.Pan = 40;
                first.Volume = 50;
                second.Volume = 60;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                AssertTrue(context.Edits.SetSelectionVel(90),
                    "batch vel edit should commit");
                AssertTrue(first.Vel == 90 && second.Vel == 90,
                    "batch vel edit did not update the complete shared selection");
                AssertTrue(context.Edits.SetSelectionPan(70),
                    "batch pan edit should commit");
                AssertTrue(first.Pan == 70 && second.Pan == 70,
                    "batch pan edit did not update the complete shared selection");
                AssertTrue(context.Edits.SetSelectionVolume(100),
                    "batch volume edit should commit");
                AssertTrue(first.Volume == 100 && second.Volume == 100,
                    "batch volume edit did not update the complete shared selection");

                undo.Undo();
                AssertTrue(first.Volume == 50 && second.Volume == 60,
                    "one undo should restore each event's original volume");
                undo.Undo();
                AssertTrue(first.Pan == 30 && second.Pan == 40,
                    "one undo should restore each event's original pan");
                undo.Undo();
                AssertTrue(first.Vel == 10 && second.Vel == 20,
                    "one undo should restore each event's original velocity");
            });

            Test("ChartEdit_InspectorBatchDurationIsGroupedAndSkipsNonNotes", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                first.Duration = 2;
                second.Duration = 4;
                var tempo = new EventData
                {
                    EventType = EventType.Tempo,
                    VirtualTick = 36
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(tempo);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second, tempo });

                AssertTrue(context.Edits.SetSelectionDuration(8),
                    "batch duration edit should commit");
                AssertTrue(first.Duration == 8 && second.Duration == 8,
                    "batch duration edit did not update every selected note");
                AssertTrue(first.VirtualDuration == 48,
                    "duration edit should keep the virtual duration conversion");
                AssertTrue(tempo.Duration == 6,
                    "batch duration edit must not touch non-note events");

                undo.Undo();
                AssertTrue(first.Duration == 2 && second.Duration == 4,
                    "one undo should restore each note's original duration");
            });

            Test("ChartEdit_InspectorBatchInstrumentIsGroupedAndUndoable", () =>
            {
                var model = EditingModel(1);
                var first = AddEditingEvent(model, 0, 12);
                var second = AddEditingEvent(model, 0, 24);
                var oldSound = new InstrumentData { InsNum = 1, Name = "old" };
                var newSound = new InstrumentData { InsNum = 2, Name = "new" };
                first.Instrument = oldSound;
                second.Instrument = null;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                AssertTrue(context.Edits.SetSelectionInstrument(newSound),
                    "batch instrument edit should commit");
                AssertTrue(object.ReferenceEquals(first.Instrument, newSound) &&
                    object.ReferenceEquals(second.Instrument, newSound),
                    "batch instrument edit did not update the complete shared selection");

                undo.Undo();
                AssertTrue(object.ReferenceEquals(first.Instrument, oldSound) &&
                    second.Instrument == null,
                    "one undo should restore each event's original instrument");
            });

            Test("ChartEdit_InspectorBatchTempoIsGroupedUndoableAndSkipsNonTempo", () =>
            {
                var model = EditingModel(1);
                var first = new EventData
                {
                    EventType = EventType.Tempo,
                    VirtualTick = 12,
                    Tempo = 120f
                };
                var second = new EventData
                {
                    EventType = EventType.Tempo,
                    VirtualTick = 24,
                    Tempo = 140.5f
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(first);
                model.Tracks.GetTrackAtIndex(0).AddEvent(second);
                var note = AddEditingEvent(model, 0, 36);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second, note });

                AssertTrue(context.Edits.SetSelectionTempo(172.5f),
                    "batch tempo edit should commit");
                AssertTrue(first.Tempo == 172.5f && second.Tempo == 172.5f,
                    "batch tempo edit did not update every selected tempo event");
                AssertTrue(note.Tempo == 140f,
                    "batch tempo edit must not touch non-tempo events");
                AssertTrue(!context.Edits.SetSelectionTempo(172.5f),
                    "assigning the current tempo should report no mutation");

                undo.Undo();
                AssertTrue(first.Tempo == 120f && second.Tempo == 140.5f,
                    "one undo should restore each event's original fractional tempo");
            });

            Test("ChartEdit_InspectorBatchBeatIsGroupedUndoableAndSkipsNonBeat", () =>
            {
                var model = EditingModel(1);
                var first = new EventData
                {
                    EventType = EventType.Beat,
                    VirtualTick = 12,
                    Beat = 3
                };
                var second = new EventData
                {
                    EventType = EventType.Beat,
                    VirtualTick = 24,
                    Beat = 4
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(first);
                model.Tracks.GetTrackAtIndex(0).AddEvent(second);
                var note = AddEditingEvent(model, 0, 36);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second, note });

                AssertTrue(context.Edits.SetSelectionBeat(6),
                    "batch beat edit should commit");
                AssertTrue(first.Beat == 6 && second.Beat == 6,
                    "batch beat edit did not update every selected beat event");
                AssertTrue(note.Beat == 4,
                    "batch beat edit must not touch non-beat events");
                AssertTrue(!context.Edits.SetSelectionBeat(6),
                    "assigning the current beat should report no mutation");

                undo.Undo();
                AssertTrue(first.Beat == 3 && second.Beat == 4,
                    "one undo should restore each event's original beat");
            });

            Test("ChartEdit_InspectorBatchVolumeAppliesToVolumeEvents", () =>
            {
                var model = EditingModel(1);
                var first = new EventData
                {
                    EventType = EventType.Volume,
                    VirtualTick = 12,
                    Volume = 80
                };
                var second = new EventData
                {
                    EventType = EventType.Volume,
                    VirtualTick = 24,
                    Volume = 100
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(first);
                model.Tracks.GetTrackAtIndex(0).AddEvent(second);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { first, second });

                AssertTrue(context.Edits.SetSelectionVolume(127),
                    "batch volume edit on volume events should commit");
                AssertTrue(first.Volume == 127 && second.Volume == 127,
                    "batch volume edit did not update every selected volume event");

                undo.Undo();
                AssertTrue(first.Volume == 80 && second.Volume == 100,
                    "one undo should restore each event's original volume");
            });

            Test("ChartEdit_NoOpBatchPropertyMutationsDoNotCreateUndoEntries", () =>
            {
                var model = EditingModel(1);
                var item = AddEditingEvent(model, 0, 12);
                item.Vel = 90;
                item.Pan = 70;
                item.Volume = 100;
                item.Duration = 8;
                var sound = new InstrumentData { InsNum = 1, Name = "sound" };
                item.Instrument = sound;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { item });

                AssertTrue(!context.Edits.SetSelectionVel(90),
                    "assigning the current vel should report no mutation");
                AssertTrue(!context.Edits.SetSelectionPan(70),
                    "assigning the current pan should report no mutation");
                AssertTrue(!context.Edits.SetSelectionVolume(100),
                    "assigning the current volume should report no mutation");
                AssertTrue(!context.Edits.SetSelectionDuration(8),
                    "assigning the current duration should report no mutation");
                AssertTrue(!context.Edits.SetSelectionInstrument(sound),
                    "assigning the current instrument should report no mutation");
                AssertTrue(!context.Edits.SetSelectionInstrument(null),
                    "a null instrument should be rejected");
                AssertTrue(!undo.CanUndo,
                    "no-op batch mutations created an undo entry");
            });

            Test("ChartEdit_SingleNoteLayerPropertyEditsAreUndoable", () =>
            {
                var model = EditingModel(1);
                var note = AddEditingEvent(model, 0, 12);
                note.Vel = 90;
                note.Pan = 70;
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { note });

                var layer = new NoteEventPropertiesLayer(note, context.Edits);
                layer.Vel = 120;
                layer.Pan = 30;
                layer.Duration = 8;

                AssertTrue(note.Vel == 120 && note.Pan == 30 && note.Duration == 8,
                    "single-note layer setters should apply through the edit controller");
                AssertTrue(undo.CanUndo, "single-note property edit must create an undo entry");

                undo.Undo();
                undo.Undo();
                undo.Undo();
                AssertTrue(note.Vel == 90 && note.Pan == 70 && note.Duration == 1,
                    "undo should restore every property changed through the single-note layer");
                AssertTrue(!undo.CanUndo, "every single-note edit should be undone");
            });

            Test("ChartEdit_SingleNoteLayerPositionMoveIsUndoable", () =>
            {
                var model = EditingModel(1);
                var note = AddEditingEvent(model, 0, 12);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { note });

                var layer = new NoteEventPropertiesLayer(note, context.Edits);
                layer.Position = 10;

                AssertTrue(note.VirtualTick == 60, "position edit should move the note");
                AssertTrue(undo.CanUndo, "position edit must create an undo entry");

                undo.Undo();
                AssertTrue(note.VirtualTick == 12, "undo should restore the original position");
            });

            Test("ChartEdit_SingleTempoLayerEditIsUndoable", () =>
            {
                var model = EditingModel(1);
                var tempo = new EventData
                {
                    EventType = EventType.Tempo,
                    VirtualTick = 12,
                    Tempo = 140f
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(tempo);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { tempo });

                var layer = new TempoEventPropertiesLayer(tempo, context.Edits);
                layer.Tempo = 175.5f;

                AssertTrue(tempo.Tempo == 175.5f, "tempo layer setter should apply");
                AssertTrue(undo.CanUndo, "tempo edit must create an undo entry");

                undo.Undo();
                AssertTrue(tempo.Tempo == 140f, "undo should restore the original tempo");
            });

            Test("ChartEdit_SingleVolumeLayerEditIsUndoable", () =>
            {
                var model = EditingModel(1);
                var volume = new EventData
                {
                    EventType = EventType.Volume,
                    VirtualTick = 12,
                    Volume = 80
                };
                model.Tracks.GetTrackAtIndex(0).AddEvent(volume);
                var undo = new UndoManager();
                var context = new EditorDocumentContext(model, "editing.pt", undo);
                context.Selection.Replace(new[] { volume });

                var layer = new VolumeEventPropertiesLayer(volume, context.Edits);
                layer.Volume = 127;

                AssertTrue(volume.Volume == 127, "volume layer setter should apply");
                AssertTrue(undo.CanUndo, "volume edit must create an undo entry");

                undo.Undo();
                AssertTrue(volume.Volume == 80, "undo should restore the original volume");
            });
        }

        private static PlayerData EditingModel(int trackCount)
        {
            var model = new PlayerData();
            for (uint i = 0; i < trackCount; i++)
            {
                model.Tracks.AddTrack(new TrackData(i));
            }
            return model;
        }

        private static EventData AddEditingEvent(PlayerData model, uint track, int virtualTick)
        {
            var item = new EventData
            {
                EventType = EventType.Note,
                VirtualTick = virtualTick,
                VirtualDuration = 6
            };
            model.Tracks.GetTrackAtIndex(track).AddEvent(item);
            return item;
        }
    }
}
