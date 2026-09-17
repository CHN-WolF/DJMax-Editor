using System;
using System.Linq;
using System.Text;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.FormatDetection;
using DJMaxEditor.Files.Tech;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private const string TechFixture =
            "{\n" +
            "  \"version\": \"3\",\n" +
            "  \"trackMetadata\": {\n" +
            "    \"guid\": \"11111111-2222-3333-4444-555555555555\",\n" +
            "    \"title\": \"Synthetic Tech\",\n" +
            "    \"artist\": \"Tester\",\n" +
            "    \"genre\": null,\n" +
            "    \"additionalCredits\": null,\n" +
            "    \"eyecatchImage\": null,\n" +
            "    \"previewTrack\": null,\n" +
            "    \"previewStartTime\": 0.0,\n" +
            "    \"previewEndTime\": 0.0,\n" +
            "    \"previewBga\": null,\n" +
            "    \"autoOrderPatterns\": true\n" +
            "  },\n" +
            "  \"patterns\": [\n" +
            "    {\n" +
            "      \"patternMetadata\": {\n" +
            "        \"guid\": \"aaaaaaaa-0000-0000-0000-000000000000\",\n" +
            "        \"patternName\": \"NM\",\n" +
            "        \"level\": 3,\n" +
            "        \"controlScheme\": 0,\n" +
            "        \"playableLanes\": 4,\n" +
            "        \"initBpm\": 150.0,\n" +
            "        \"bps\": 4\n" +
            "      },\n" +
            "      \"packedNotes\": [\n" +
            "        \"Basic|0|0|\",\n" +
            "        \"Basic|240|1|\",\n" +
            "        \"E|RepeatHead|480|2|110|-10|0|hit.ogg\"\n" +
            "      ],\n" +
            "      \"packedHoldNotes\": [\n" +
            "        \"Hold|720|3|240|\"\n" +
            "      ],\n" +
            "      \"packedDragNotes\": [\n" +
            "        { \"packedNote\": \"Drag|1440|0|\", \"packedNodes\": [ \"0|0|0|0|0|0\", \"240|1|0|0|0|0\" ] }\n" +
            "      ],\n" +
            "      \"bpmEvents\": [\n" +
            "        { \"pulse\": 960, \"bpm\": 180.0 }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";

        private const string TechTwoPatternFixture =
            "{\n" +
            "  \"version\": \"3\",\n" +
            "  \"trackMetadata\": { \"guid\": \"g\", \"title\": \"Two\" },\n" +
            "  \"patterns\": [\n" +
            "    {\n" +
            "      \"patternMetadata\": { \"patternName\": \"NM\", \"level\": 1, \"playableLanes\": 4, \"initBpm\": 120.0 },\n" +
            "      \"packedNotes\": [ \"Basic|0|0|\" ]\n" +
            "    },\n" +
            "    {\n" +
            "      \"patternMetadata\": { \"patternName\": \"MX\", \"level\": 9, \"playableLanes\": 4, \"initBpm\": 120.0 },\n" +
            "      \"packedNotes\": [ \"Basic|240|3|\" ]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";

        private static void RunTechFormatTests()
        {
            Test("Tech_DetectsTrackFromContent", () =>
            {
                var detected = ChartFormatDetector.Detect(
                    Encoding.UTF8.GetBytes(TechFixture), ".bin");
                AssertFormat(detected, ChartFormat.TechmaniaTrack);
                AssertTrue(detected.IsOpenable, "track.tech should be openable");
            });

            Test("Tech_ImportsNotesTempoAndMetadata", () =>
            {
                PlayerData chart = TechmaniaChartSerializer.Parse(
                    Encoding.UTF8.GetBytes(TechFixture));

                AssertTrue(chart.SourceFormat == ChartFormat.TechmaniaTrack,
                    "source format was not retained");
                AssertTrue(chart.TechMetadata != null, "TECH metadata was not retained");
                AssertTrue(chart.TechMetadata.Title == "Synthetic Tech",
                    "track title was not retained");
                AssertTrue(chart.TechMetadata.PatternName == "NM" &&
                    chart.TechMetadata.Level == 3,
                    "pattern identity was not retained");
                AssertTrue(Math.Abs(chart.Tempo - 150f) < 0.001f,
                    "initBpm was not imported");

                var notes = chart.Tracks.SelectMany(t => t.Events)
                    .Where(e => e.EventType == EventType.Note).ToArray();
                // Pulse grid (240/beat) -> virtual ticks (288/beat): pulse * 6 / 5.
                AssertTrue(notes.Any(e => e.Attribute == (byte)EventAttribute.BasicNote &&
                    e.TrackId == 0 && e.Tick == 0),
                    "Basic note on lane 0 was not imported");
                AssertTrue(notes.Any(e => e.Attribute == (byte)EventAttribute.BasicNote &&
                    e.TrackId == 1 && e.Tick == 48),
                    "Basic note pulse->tick conversion failed (240 pulse -> 48 ticks)");
                AssertTrue(notes.Any(e => e.Attribute == (byte)EventAttribute.RepeatNote &&
                    e.TrackId == 2),
                    "extended RepeatHead was not imported");
                AssertTrue(notes.Any(e => e.Attribute == (byte)EventAttribute.LongHoldNote &&
                    e.TrackId == 3 && e.Duration == 48),
                    "Hold with 240-pulse duration did not become a 48-tick hold");
                AssertTrue(notes.Any(e => e.Attribute == (byte)EventAttribute.BasicNote &&
                    e.TrackId == 0 && e.Tick == 288 && e.Duration > 0),
                    "Drag did not become a long Basic note (1440 pulses -> tick 288)");

                var tempo = chart.Tracks.SelectMany(t => t.Events)
                    .SingleOrDefault(e => e.EventType == EventType.Tempo && e.VirtualTick > 0);
                AssertTrue(tempo != null && tempo.Tick == 192 &&
                    Math.Abs(tempo.Tempo - 180f) < 0.001f,
                    "bpmEvent was not imported (960 pulses -> tick 192)");

                // The player only applies tempo when it crosses a TEMPO event, so the
                // importer must synthesize one at tick 0 on track 0 holding initBpm.
                var initial = chart.Tracks.SelectMany(t => t.Events)
                    .SingleOrDefault(e => e.EventType == EventType.Tempo && e.VirtualTick == 0);
                AssertTrue(initial != null && initial.TrackId == 0 &&
                    Math.Abs(initial.Tempo - 150f) < 0.001f,
                    "initBpm was not synthesized as a tick-0 TEMPO event on track 0");
            });

            Test("Tech_CompactsKeysoundLanesOntoTracks16And20Plus", () =>
            {
                const string fixture =
                    "{ \"version\": \"3\"," +
                    "  \"trackMetadata\": { \"guid\": \"g\", \"title\": \"Lanes\" }," +
                    "  \"patterns\": [ {" +
                    "    \"patternMetadata\": { \"patternName\": \"NM\", \"level\": 1, \"playableLanes\": 4, \"initBpm\": 120.0 }," +
                    "    \"packedNotes\": [ \"Basic|0|0|\", \"Basic|0|1|\", \"Basic|0|2|\", \"Basic|0|3|\"," +
                    "                     \"Basic|240|4|\", \"Basic|360|5|\", \"Basic|480|6|\" ]," +
                    "    \"bpmEvents\": [] } ] }";

                PlayerData chart = TechmaniaChartSerializer.Parse(
                    Encoding.UTF8.GetBytes(fixture));

                TrackData TrackWith(uint idx) => chart.Tracks.FirstOrDefault(t => t.Idx == idx);
                AssertTrue(TrackWith(16) != null &&
                    TrackWith(16).Events.Any(e => e.EventType == EventType.Note),
                    "first keysound lane did not land on track 16");
                AssertTrue(TrackWith(20) != null &&
                    TrackWith(20).Events.Any(e => e.EventType == EventType.Note),
                    "second keysound lane did not land on track 20");
                AssertTrue(TrackWith(21) != null &&
                    TrackWith(21).Events.Any(e => e.EventType == EventType.Note),
                    "third keysound lane did not land on track 21");
                AssertTrue(TrackWith(9) != null && !TrackWith(9).Events.Any() &&
                    TrackWith(17) != null && !TrackWith(17).Events.Any() &&
                    TrackWith(19) != null && !TrackWith(19).Events.Any(),
                    "reserved tracks 9/17/19 must exist as empty placeholders (position == Idx)");
                AssertTrue(chart.TechMetadata.FormatLaneForTrack(16) == 4 &&
                    chart.TechMetadata.FormatLaneForTrack(20) == 5 &&
                    chart.TechMetadata.FormatLaneForTrack(21) == 6,
                    "overflow track-to-lane mapping was not retained");

                string saved = TechmaniaChartSerializer.Serialize(chart);
                PlayerData reparsed = TechmaniaChartSerializer.Parse(
                    Encoding.UTF8.GetBytes(saved));
                var laneTicks = reparsed.Tracks
                    .Where(t => t.Idx == 16 || t.Idx == 20 || t.Idx == 21)
                    .SelectMany(t => t.Events)
                    .Where(e => e.EventType == EventType.Note)
                    .Select(e => e.Tick).OrderBy(x => x).ToArray();
                AssertTrue(laneTicks.SequenceEqual(new[] { 48, 72, 96 }),
                    "round trip did not restore the keysound lanes (240/360/480 pulses -> ticks 48/72/96)");
            });

            Test("Tech_MultiPatternListsAndOpensRequestedSlot", () =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(TechTwoPatternFixture);
                var patterns = TechmaniaChartSerializer.ListPatterns(bytes);
                AssertTrue(patterns.Count == 2, "container should list two patterns");
                AssertTrue(patterns[0].Name == "NM" && patterns[1].Name == "MX" &&
                    patterns[1].Level == 9,
                    "pattern summaries are wrong");

                PlayerData second = TechmaniaChartSerializer.Parse(bytes, 1);
                AssertTrue(second.TechMetadata.ActivePatternIndex == 1,
                    "requested pattern slot was not opened");
                AssertTrue(second.TechMetadata.PatternName == "MX",
                    "wrong pattern imported");
                AssertTrue(second.TechMetadata.SiblingPatterns.Count == 1 &&
                    second.TechMetadata.SiblingPatterns[0].Index == 0,
                    "sibling pattern was not retained verbatim for save-back");

                var note = second.Tracks.SelectMany(t => t.Events)
                    .Single(e => e.EventType == EventType.Note);
                AssertTrue(note.TrackId == 3 && note.Tick == 48,
                    "second pattern's note did not land on its lane");
            });

            Test("Tech_RoundTripPreservesNotesAndSiblings", () =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(TechTwoPatternFixture);
                PlayerData chart = TechmaniaChartSerializer.Parse(bytes, 1);

                string saved = TechmaniaChartSerializer.Serialize(chart);
                var detected = ChartFormatDetector.Detect(Encoding.UTF8.GetBytes(saved), ".tech");
                AssertFormat(detected, ChartFormat.TechmaniaTrack);
                AssertTrue(!saved.Contains("\"pulse\": 0"),
                    "synthesized tick-0 tempo must not round-trip into bpmEvents");

                PlayerData reparsed = TechmaniaChartSerializer.Parse(
                    Encoding.UTF8.GetBytes(saved), 1);
                AssertTrue(reparsed.TechMetadata.PatternName == "MX",
                    "round trip lost the pattern identity");
                AssertTrue(reparsed.TechMetadata.SiblingPatterns.Count == 1,
                    "round trip dropped the sibling difficulty");

                var note = reparsed.Tracks.SelectMany(t => t.Events)
                    .SingleOrDefault(e => e.EventType == EventType.Note);
                AssertTrue(note != null && note.TrackId == 3 && note.Tick == 48,
                    "round trip moved the note");
            });
        }
    }
}
