using System.Collections.Generic;

namespace FalsePositive.UI
{
    /// <summary>
    /// Static (not a MonoBehaviour, so it survives scene loads/unloads)
    /// registry of currently-open MenuWindows, in open order. One Escape
    /// handler (MainMenuController.Update) calls CloseTop() rather than every
    /// MenuWindow polling Escape itself and racing to close two at once — the
    /// same direct-polling-and-dispatch idiom as DebugOverlayUI.cs and
    /// GameFlowDirector.cs's F2 check.
    /// </summary>
    public static class MenuWindowStack
    {
        private static readonly List<MenuWindow> OpenWindows = new List<MenuWindow>();

        public static bool AnyOpen => OpenWindows.Count > 0;

        public static MenuWindow Top => OpenWindows.Count > 0 ? OpenWindows[OpenWindows.Count - 1] : null;

        public static void Push(MenuWindow window)
        {
            OpenWindows.Remove(window);
            OpenWindows.Add(window);
        }

        public static void Pop(MenuWindow window)
        {
            OpenWindows.Remove(window);
        }

        public static void CloseTop()
        {
            Top?.Close();
        }
    }
}
