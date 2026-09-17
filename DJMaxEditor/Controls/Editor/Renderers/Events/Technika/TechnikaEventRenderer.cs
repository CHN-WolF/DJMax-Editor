using DJMaxEditor.DJMax;
using System.Collections.Generic;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Events
{
    internal class TechnikaThemeRenderer : EventRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "P1 Visible", 0, 3);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Red, "P1 notes special", 4, 7);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player2Visible, "P2 Visible", 8, 11);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Azure, "P2 notes special", 12, 15);
        }

        public override string GetName()
        {
            return "Technika";
        }

        public override IEnumerable<KeyValuePair<string, EventData>> GetTemplates()
        {
            var list = new List<KeyValuePair<string, EventData>>();

            list.Add(this.CreateNoteFromEventData(
                "Basic note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.BasicNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Basic note long",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.BasicNote,
                    Duration = 9,
                }
            ));

            /*
            list.Add(this.CreateNoteFromEventData(
                "Basic note long up flag",
                new EventData()
                {
                    EventType = PlayerData.EventType.Note,
                    Attribute = (byte)90,
                }
            ));
            */

            list.Add(this.CreateNoteFromEventData(
                "Repeat note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.RepeatNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Repeat note end",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.RepeatNoteEnd,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Long hold note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.LongHoldNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Press note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.PressNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Press note end",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.PressNoteEnd,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Tempo",
                new EventData()
                {
                    EventType = EventType.Tempo,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Volume",
                new EventData()
                {
                    EventType = EventType.Volume,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Beat",
                new EventData()
                {
                    EventType = EventType.Beat,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Video start",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.VideoStart,
                }
            ));

            return list;
        }

        public override void RenderEventData(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {
            //g.DrawRectangle(Pens.Red, eventRectangle);

            switch (eventData.EventType)
            {
                case EventType.Note:
                    {
                        RenderNote(g, eventData, eventRectangle, centerX, centerY);
                    }
                    break;
                case EventType.Tempo:
                    {
                        DrawImage(g, DJMRessources.metronome, centerX - (90 / 2), centerY - (90 / 2), 90, 90);
                    }
                    break;
                case EventType.Volume:
                    {
                        DrawImage(g, DJMRessources.volume, centerX - (64 / 2), centerY - (64 / 2), 64, 64);
                    }
                    break;
                case EventType.Beat:
                    {
                        DrawImage(g, DJMRessources.beat, centerX - (90 / 2), centerY - (90 / 2), 90, 90);
                    }
                    break;
                default:
                    {
                        g.DrawImage(DJMRessources.unknowNote, eventRectangle, 0, 0, 90, 90, GraphicsUnit.Pixel, null);
                    }
                    break;
            }
        }

        public override void RenderNote(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {
            int x = centerX;
            int y = centerY;

            if (eventData.Attribute == (byte)EventAttribute.BasicNote)
            { // normal note

                // if the note have a more than 6 as duration, it could be a hold note
                if (eventData.Duration > 6)
                {
                    DrawDragBody(g, TechnikaHdAssets.LongBody, x, y, eventData.VirtualDuration, 76);
                    DrawImageHd(g, TechnikaHdAssets.LongHead, x - (116 / 2), y - (116 / 2), 116, 116);
                }
                else
                {
                    DrawImageHd(g, TechnikaHdAssets.Basic, x - (90 / 2), y - (90 / 2), 90, 90);
                }
            }

            else if (eventData.Attribute == (byte)EventAttribute.PressNote)
            { // note press start
                DrawImageHd(g, TechnikaHdAssets.LongHead, x - (116 / 2), y - (116 / 2), 116, 116);
            }
            else if (eventData.Attribute == (byte)EventAttribute.PressNoteEnd)
            { // note press start end
                DrawImageHd(g, TechnikaHdAssets.PressEnd, x - (76 / 2), y - (76 / 2), 76, 76);
            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNote)
            { // note repeat

                if (eventData.Duration > 6)
                {
                    DrawSolidTrail(g, RepeatTrailBrush, x, y, eventData.VirtualDuration, 90);
                }

                DrawImageHd(g, TechnikaHdAssets.RepeatHead, x - (90 / 2), y - (90 / 2), 90, 90);

            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNoteEnd)
            { // note repeat end
                DrawImageHd(g, TechnikaHdAssets.RepeatTail, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.LongHoldNote)
            { // long hold note
                DrawSolidTrail(g, HoldTrailBrush, x, y, eventData.VirtualDuration, 90);
                DrawImageHd(g, TechnikaHdAssets.HoldHead, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.VideoStart)
            { // Video Start
                DrawImage(g, DJMRessources.videoStart, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else
            {
                DrawImage(g, DJMRessources.unknowNote, x - (90 / 2), y - (90 / 2), 90, 90);
            }
        }

        // Draws an image with its full source rectangle mapped to the destination,
        // so every pixel of the HD art feeds the zoom transform.
        private static void DrawImageHd(GraphicsWrapper g, Image image, int x, int y, int width, int height)
        {
            if (image == null)
            {
                return;
            }
            g.DrawImage(
                image,
                new Rectangle(x, y, width, height),
                0,
                0,
                image.Width,
                image.Height,
                GraphicsUnit.Pixel,
                null);
        }

        // Drag-note body: flat left slice stretched across the hold, then the frame
        // again so the rounded tip lands just past the hold end.
        private static void DrawDragBody(GraphicsWrapper g, Image body, int x, int y, int duration, int height)
        {
            if (body == null || duration <= 0)
            {
                return;
            }
            int half = height / 2;
            g.DrawImage(
                body,
                new Rectangle(x, y - half, duration, height),
                0,
                0,
                2,
                body.Height,
                GraphicsUnit.Pixel,
                null);
            g.DrawImage(
                body,
                new Rectangle(x + duration + 25 - height, y - half, height, height),
                0,
                0,
                body.Width,
                body.Height,
                GraphicsUnit.Pixel,
                null);
        }

        // Hold/repeat trails are single-color solid bars in Technika, drawn
        // semi-transparent so the lane grid stays readable underneath.
        private static readonly Brush HoldTrailBrush =
            new SolidBrush(Color.FromArgb(120, 45, 100, 235));

        private static readonly Brush RepeatTrailBrush =
            new SolidBrush(Color.FromArgb(120, 155, 65, 225));

        private static void DrawSolidTrail(GraphicsWrapper g, Brush brush, int x, int y, int duration, int height)
        {
            if (duration <= 0)
            {
                return;
            }
            int lineHeight = height / 3;
            g.FillRectangle(brush, x, y - lineHeight / 2, duration, lineHeight);
        }
    }
}
