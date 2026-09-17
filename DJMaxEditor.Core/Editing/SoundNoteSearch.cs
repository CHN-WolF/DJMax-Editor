using System;
using System.Collections.Generic;
using System.Linq;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Editor
{
    /// <summary>
    /// Finds note events that reference a given sound (instrument) file name.
    /// Pure domain logic shared by the Find dialog and unit tests.
    /// </summary>
    public static class SoundNoteSearch
    {
        /// <summary>
        /// Returns every note whose instrument name contains <paramref name="query"/>
        /// (case-insensitive), ordered by virtual tick. An empty/whitespace query or a
        /// null model yields an empty list.
        /// </summary>
        public static List<EventData> FindNotesByInstrumentName(PlayerData data, string query)
        {
            if (data == null || string.IsNullOrWhiteSpace(query))
            {
                return new List<EventData>();
            }

            return data.Tracks.Events
                .Where(ev => ev.EventType == EventType.Note
                    && ev.Instrument != null
                    && !string.IsNullOrEmpty(ev.Instrument.Name)
                    && ev.Instrument.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(ev => ev.VirtualTick)
                .ThenBy(ev => ev.TrackId)
                .ToList();
        }

        /// <summary>
        /// Index of the first match at or after <paramref name="fromVirtualTick"/>, wrapping
        /// to the first match when the playhead sits past every match. -1 when empty.
        /// </summary>
        public static int FindNextIndex(IReadOnlyList<EventData> matches, int fromVirtualTick)
        {
            if (matches == null || matches.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].VirtualTick >= fromVirtualTick)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>
        /// Index of the last match strictly before <paramref name="fromVirtualTick"/>,
        /// wrapping to the last match when the playhead sits at/before every match.
        /// -1 when empty.
        /// </summary>
        public static int FindPreviousIndex(IReadOnlyList<EventData> matches, int fromVirtualTick)
        {
            if (matches == null || matches.Count == 0)
            {
                return -1;
            }

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (matches[i].VirtualTick < fromVirtualTick)
                {
                    return i;
                }
            }
            return matches.Count - 1;
        }
    }
}
