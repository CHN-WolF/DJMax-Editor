using System;
using System.IO;
using System.Linq;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.bytes;
using DJMaxEditor.Files.FormatDetection;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunTrailerSaveRoundTripTests()
        {
            Test("TrailerSave_RoundTrip_ChartWithBeatEventsReopens", () =>
            {
                var model = new PlayerData();
                model.TickPerMinute = 960;
                model.Tempo = 140f;
                model.Instruments.Add(new InstrumentData { InsNum = 0, Name = "none" });
                model.Instruments.Add(new InstrumentData { InsNum = 1, Name = "kick.ogg" });

                var track = new TrackData(0);
                model.Tracks.AddTrack(track);
                track.AddEvent(new EventData { EventType = EventType.Beat, Tick = 0, Beat = 4 });
                track.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 4,
                    Instrument = model.Instruments[1],
                    Vel = 100,
                    Pan = 64,
                    Duration = 6
                });
                track.AddEvent(new EventData { EventType = EventType.Volume, Tick = 8, Volume = 90 });
                track.AddEvent(new EventData { EventType = EventType.Tempo, Tick = 12, Tempo = 150f });

                string path = Path.Combine(Path.GetTempPath(),
                    "tq_roundtrip_" + Guid.NewGuid().ToString("N") + ".bytes");
                try
                {
                    AssertTrue(new TQSaveFile().Save(path, model), "trailer save should succeed");

                    byte[] written = File.ReadAllBytes(path);
                    var detection = ChartFormatDetector.Detect(written, ".bytes");
                    AssertTrue(detection.Format == ChartFormat.TrailerRespectV,
                        $"saved file should be detected as TrailerRespectV, got {detection.Format}");

                    TrailerReadResult stats;
                    var loaded = TrailerChartReader.Read(written, false, out stats);
                    var events = loaded.Tracks.GetTrackAtIndex(0).Events.ToList();

                    AssertTrue(events.Count == 3,
                        $"beat events have no trailer representation and must be dropped, got {events.Count} events");
                    AssertTrue(events.Any(e => e.EventType == EventType.Note &&
                        e.Tick == 4 && e.Vel == 100 && e.Pan == 64 && e.Duration == 6 &&
                        e.Instrument != null && e.Instrument.InsNum == 1),
                        "note event did not round-trip");
                    AssertTrue(events.Any(e => e.EventType == EventType.Volume &&
                        e.Tick == 8 && e.Volume == 90),
                        "volume event did not round-trip");
                    AssertTrue(events.Any(e => e.EventType == EventType.Tempo &&
                        e.Tick == 12 && e.Tempo == 150f),
                        "tempo event did not round-trip");
                }
                finally
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            });

            Test("TrailerSave_RoundTrip_MultiTrackEventCountsStayAligned", () =>
            {
                var model = new PlayerData();
                model.TickPerMinute = 960;
                model.Tempo = 140f;
                model.Instruments.Add(new InstrumentData { InsNum = 0, Name = "none" });
                model.Instruments.Add(new InstrumentData { InsNum = 1, Name = "snare.ogg" });

                var first = new TrackData(0);
                var second = new TrackData(1);
                model.Tracks.AddTrack(first);
                model.Tracks.AddTrack(second);
                first.AddEvent(new EventData { EventType = EventType.Beat, Tick = 0, Beat = 4 });
                first.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 2,
                    Instrument = model.Instruments[1]
                });
                second.AddEvent(new EventData { EventType = EventType.Beat, Tick = 0, Beat = 3 });
                second.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 6,
                    Instrument = model.Instruments[1],
                    Vel = 80
                });

                string path = Path.Combine(Path.GetTempPath(),
                    "tq_roundtrip_" + Guid.NewGuid().ToString("N") + ".bytes");
                try
                {
                    AssertTrue(new TQSaveFile().Save(path, model), "trailer save should succeed");

                    TrailerReadResult stats;
                    var loaded = TrailerChartReader.Read(File.ReadAllBytes(path), false, out stats);

                    var firstEvents = loaded.Tracks.GetTrackAtIndex(0).Events.ToList();
                    var secondEvents = loaded.Tracks.GetTrackAtIndex(1).Events.ToList();
                    AssertTrue(firstEvents.Count == 1 && firstEvents[0].Tick == 2,
                        "first track should contain exactly its note event");
                    AssertTrue(secondEvents.Count == 1 && secondEvents[0].Tick == 6 &&
                        secondEvents[0].Vel == 80,
                        "second track events must not be corrupted by the first track's beat events");
                }
                finally
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            });
        }
    }
}
