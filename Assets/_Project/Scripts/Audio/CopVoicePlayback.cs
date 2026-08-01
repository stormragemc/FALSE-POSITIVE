using System;
using System.Collections;
using FalsePositive.Core;
using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// Owns the officer's AudioSource. IsSpeaking stays true for a short
    /// tail after AudioSource.isPlaying flips false — the audible
    /// reverb/decay can outlast that flag, and that tail is exactly what
    /// would re-trigger the player's own VAD. DialogueManager gates the VAD
    /// off of IsSpeaking here, never off isPlaying directly.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class CopVoicePlayback : MonoBehaviour
    {
        [SerializeField] private InterrogationConfig config;
        [Tooltip("Fallback used only if config is unassigned. Prefer config.ttsEchoGateTailSeconds — " +
                 "having both was a dead-config bug (this field was the one actually read; the config " +
                 "value was set but silently ignored).")]
        [SerializeField] private float postPlaybackTailSeconds = 0.25f;

        private float TailSeconds => config != null ? config.ttsEchoGateTailSeconds : postPlaybackTailSeconds;

        public event Action Started;
        public event Action Stopped;

        private AudioSource _source;
        private Coroutine _watchRoutine;

        public bool IsSpeaking { get; private set; }
        public AudioSource Source => _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        public void Play(AudioClip clip)
        {
            if (_source == null || clip == null) return;

            if (_watchRoutine != null)
            {
                StopCoroutine(_watchRoutine);
            }

            _source.clip = clip;
            _source.Play();
            IsSpeaking = true;
            Started?.Invoke();

            _watchRoutine = StartCoroutine(WatchPlayback());
        }

        private IEnumerator WatchPlayback()
        {
            yield return new WaitWhile(() => _source.isPlaying);
            yield return new WaitForSeconds(TailSeconds);
            IsSpeaking = false;
            _watchRoutine = null;
            Stopped?.Invoke();
        }
    }
}
