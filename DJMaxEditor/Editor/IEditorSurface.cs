using System.Windows.Forms;

namespace DJMaxEditor.Editor
{
    public interface IEditorSurface
    {
        Control View { get; }

        bool SupportsEditing { get; }

        void Bind(EditorDocumentContext document);

        void InvalidateView();

        bool TrySetTimeZoom(float zoom);

        EditorViewState CaptureViewState();

        void RestoreViewState(EditorViewState state);

        int PlayheadVirtualTick { get; set; }

        /// <summary>
        /// Scrolls the surface so the given virtual tick (and, when the surface
        /// has per-track lanes, the given track lane) becomes visible.
        /// </summary>
        void RevealPosition(int virtualTick, int trackIndex);
    }
}
