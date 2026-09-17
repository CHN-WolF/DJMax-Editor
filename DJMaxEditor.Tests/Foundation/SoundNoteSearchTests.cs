using System.Collections.Generic;
using System.Linq;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunSoundNoteSearchTests()
        {
            Test("SoundNoteSearch_MatchesSubstringCaseInsensitive", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, "KICK");
                AssertTrue(matches.Count == 2, "expected 2 kick matches, got " + matches.Count);
                AssertTrue(matches.All(ev => ev.Instrument.Name == "kick.wav"), "unexpected instrument matched");
            });

            Test("SoundNoteSearch_ExtensionlessQueryMatchesFileName", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, "snare");
                AssertTrue(matches.Count == 1, "expected 1 snare match, got " + matches.Count);
                AssertTrue(matches[0].Instrument.Name == "Snare.ogg", "case-insensitive match failed");
            });

            Test("SoundNoteSearch_SkipsNonNoteEventsAndNullInstruments", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, ".wav");
                AssertTrue(matches.Count == 2, "only note events with instruments may match");
                AssertTrue(matches.All(ev => ev.EventType == EventType.Note), "non-note event matched");
            });

            Test("SoundNoteSearch_EmptyQueryOrModelReturnsEmpty", () =>
            {
                var data = BuildSearchFixture();
                AssertTrue(SoundNoteSearch.FindNotesByInstrumentName(data, "").Count == 0, "empty query");
                AssertTrue(SoundNoteSearch.FindNotesByInstrumentName(data, "   ").Count == 0, "whitespace query");
                AssertTrue(SoundNoteSearch.FindNotesByInstrumentName(null, "kick").Count == 0, "null model");
                AssertTrue(SoundNoteSearch.FindNotesByInstrumentName(data, "no-such-sound").Count == 0, "no match");
            });

            Test("SoundNoteSearch_ResultsOrderedByVirtualTick", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, ".wav");
                AssertTrue(matches[0].VirtualTick < matches[1].VirtualTick, "matches not sorted by tick");
            });

            Test("SoundNoteSearch_FindNextWrapsAround", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, ".wav");
                int first = SoundNoteSearch.FindNextIndex(matches, 0);
                int second = SoundNoteSearch.FindNextIndex(matches, matches[0].VirtualTick + 1);
                int wrapped = SoundNoteSearch.FindNextIndex(matches, matches[1].VirtualTick + 1);
                AssertTrue(first == 0, "next from start should be 0, got " + first);
                AssertTrue(second == 1, "next past first should be 1, got " + second);
                AssertTrue(wrapped == 0, "next past end should wrap to 0, got " + wrapped);
                AssertTrue(SoundNoteSearch.FindNextIndex(new List<EventData>(), 0) == -1, "empty list must give -1");
            });

            Test("SoundNoteSearch_FindPreviousWrapsAround", () =>
            {
                var data = BuildSearchFixture();
                var matches = SoundNoteSearch.FindNotesByInstrumentName(data, ".wav");
                int beforeSecond = SoundNoteSearch.FindPreviousIndex(matches, matches[1].VirtualTick);
                int wrapped = SoundNoteSearch.FindPreviousIndex(matches, matches[0].VirtualTick);
                AssertTrue(beforeSecond == 0, "previous before second should be 0, got " + beforeSecond);
                AssertTrue(wrapped == 1, "previous at first should wrap to last, got " + wrapped);
                AssertTrue(SoundNoteSearch.FindPreviousIndex(new List<EventData>(), 0) == -1, "empty list must give -1");
            });
        }

        private static PlayerData BuildSearchFixture()
        {
            var data = new PlayerData();
            var kick = new InstrumentData { InsNum = 1, Name = "kick.wav" };
            var snare = new InstrumentData { InsNum = 2, Name = "Snare.ogg" };
            data.Instruments.Add(kick);
            data.Instruments.Add(snare);

            var track = new TrackData(0);
            track.AddEvent(new EventData
            {
                EventType = EventType.Note,
                Instrument = kick,
                VirtualTick = 960
            });
            track.AddEvent(new EventData
            {
                EventType = EventType.Note,
                Instrument = kick,
                VirtualTick = 480
            });
            track.AddEvent(new EventData
            {
                EventType = EventType.Note,
                Instrument = snare,
                VirtualTick = 720
            });
            track.AddEvent(new EventData
            {
                EventType = EventType.Volume,
                Instrument = kick,
                VirtualTick = 240
            });
            track.AddEvent(new EventData
            {
                EventType = EventType.Note,
                Instrument = null,
                VirtualTick = 600
            });
            data.Tracks.AddTrack(track);
            return data;
        }
    }
}
