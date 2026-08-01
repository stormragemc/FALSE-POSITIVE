using FalsePositive.Audio;
using FalsePositive.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Small mic icon + fill bar, visible only while seated. Reads
    /// VoiceActivityDetector.DisplayRms, which is already forced to zero
    /// while gated, so this never implies the mic is hearing the cop.
    /// </summary>
    public sealed class MicLevelMeterUI : MonoBehaviour
    {
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private PlayerStateController playerState;
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject root;
        [SerializeField] private float displayGain = 12f;

        private void OnEnable()
        {
            if (playerState == null) return;
            playerState.SeatedChanged += SetVisible;
            SetVisible(playerState.State == PlayerState.Seated);
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.SeatedChanged -= SetVisible;
        }

        private void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        private void Update()
        {
            if (fillImage == null || vad == null) return;
            fillImage.fillAmount = Mathf.Clamp01(vad.DisplayRms * displayGain);
        }
    }
}
