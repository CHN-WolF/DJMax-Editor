using System.Drawing;
using System.IO;
using System.Reflection;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    /// <summary>
    /// HD TECHNIKA 3 note art baked from the TECHMANIA skin sheets. Replaces the
    /// original low-resolution strip bitmaps so notes stay sharp at every zoom.
    /// </summary>
    internal static class TechnikaHdAssets
    {
        private const string ResourcePrefix = "DJMaxEditor.TechnikaHD.";

        public static readonly Bitmap Basic = Load("Basic.png");
        public static readonly Bitmap LongHead = Load("LongHead.png");
        public static readonly Bitmap HoldHead = Load("HoldHead.png");
        public static readonly Bitmap RepeatHead = Load("RepeatHead.png");
        public static readonly Bitmap PressEnd = Load("PressEnd.png");
        public static readonly Bitmap RepeatTail = Load("RepeatTail.png");
        public static readonly Bitmap LongBody = Load("LongBody.png");

        private static Bitmap Load(string fileName)
        {
            Assembly assembly = typeof(TechnikaHdAssets).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName))
            {
                return stream == null ? null : new Bitmap(stream);
            }
        }
    }
}
