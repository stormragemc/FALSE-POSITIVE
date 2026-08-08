using FalsePositive.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.Menu
{
    /// <summary>
    /// The only new *behavioural* window — Credits and How-to-play are pure
    /// content and need nothing beyond a bare MenuWindow. Owns the
    /// #if UNITY_EDITOR / EditorApplication.isPlaying = false quit logic that
    /// used to live directly in MainMenuController.HandleQuit(), now gated
    /// behind an explicit confirmation instead of firing on the first click.
    /// </summary>
    public sealed class QuitConfirmWindow : MonoBehaviour
    {
        [SerializeField] private MenuWindow window;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            if (window != null) window.Open();
        }

        public void Close()
        {
            if (window != null) window.Close();
        }

        private static void HandleConfirm()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
