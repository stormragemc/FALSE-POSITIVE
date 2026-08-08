using System.Collections;
using UnityEngine;

namespace FalsePositive.Rendering
{
    /// <summary>
    /// The only thing gameplay code should touch to drive the drunk post-process
    /// effect (Cutscene/DrunkCutsceneBinder.cs is the one caller today, ramping it
    /// across CutsceneId.Wake). Lives in _Persistent, one instance for the whole
    /// game.
    ///
    /// DrunkColorPulseFeature's render pass is a ScriptableRendererFeature -- an
    /// asset, not a scene object -- so it cannot hold a SerializeField reference to
    /// a MonoBehaviour in the scene. Settings flow the other way instead: this
    /// class owns CurrentSettings (a static struct) and the feature's pass reads
    /// it fresh every frame in RecordRenderGraph. There is deliberately no event
    /// or callback in between -- one frame of staleness on a value that is
    /// normally being smoothly ramped is not observable.
    /// </summary>
    public sealed class DrunkEffectController : MonoBehaviour
    {
        [SerializeField] private Color overlayColor = new Color(0.55f, 0.15f, 0.65f, 1f);
        [SerializeField, Range(0.01f, 100f)] private float pulseSpeed = 3f;
        [SerializeField] private bool pulseEnabled = true;
        [SerializeField, Range(0f, 0.99f)] private float trailBlurStrength = 0.6f;

        /// <summary>What DrunkColorPulseFeature's pass actually reads. Struct, not
        /// class, so a reader never sees a half-written ramp step from another
        /// thread -- render graph recording happens off the main thread's
        /// coroutine tick, and this way it only ever sees whole assignments.</summary>
        public readonly struct Settings
        {
            public readonly Color overlayColor;
            public readonly float pulseSpeed;
            public readonly bool pulseEnabled;
            public readonly float trailBlurStrength;
            public readonly float overlayIntensity;

            public Settings(Color overlayColor, float pulseSpeed, bool pulseEnabled,
                float trailBlurStrength, float overlayIntensity)
            {
                this.overlayColor = overlayColor;
                this.pulseSpeed = pulseSpeed;
                this.pulseEnabled = pulseEnabled;
                this.trailBlurStrength = trailBlurStrength;
                this.overlayIntensity = overlayIntensity;
            }

            /// <summary>Feature skips the whole pass when this is false, so an
            /// idle game (the overwhelming majority of play time) pays nothing.</summary>
            public bool IsActive => overlayIntensity > 0.0001f || trailBlurStrength > 0.0001f;
        }

        public static Settings CurrentSettings { get; private set; }

        private float _currentIntensity;
        private Coroutine _rampRoutine;

        private void Awake()
        {
            PublishStopped();
        }

        /// <summary>Snaps to a given 0..1 intensity with no ramp -- for debug/test
        /// use (Unity_RunCommand verification) rather than gameplay, which should
        /// prefer RampTo for a readable in/out.</summary>
        public void SetImmediate(float intensity)
        {
            StopRamp();
            _currentIntensity = Mathf.Clamp01(intensity);
            Publish();
        }

        /// <summary>Eases the overlay intensity to the target over seconds. A
        /// second call supersedes whatever ramp is already running (same
        /// generation-guard shape as ScreenFader.Fade) rather than the two
        /// stacking.</summary>
        public Coroutine RampTo(float targetIntensity, float seconds)
        {
            StopRamp();
            _rampRoutine = StartCoroutine(RampRoutine(Mathf.Clamp01(targetIntensity), Mathf.Max(0f, seconds)));
            return _rampRoutine;
        }

        public void Clear() => SetImmediate(0f);

        private IEnumerator RampRoutine(float target, float seconds)
        {
            float start = _currentIntensity;
            if (seconds <= 0f)
            {
                _currentIntensity = target;
                Publish();
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                _currentIntensity = Mathf.Lerp(start, target, t / seconds);
                Publish();
                yield return null;
            }
            _currentIntensity = target;
            Publish();
        }

        private void StopRamp()
        {
            if (_rampRoutine != null) StopCoroutine(_rampRoutine);
            _rampRoutine = null;
        }

        private void Publish()
        {
            CurrentSettings = new Settings(overlayColor, pulseSpeed, pulseEnabled,
                trailBlurStrength * _currentIntensity, _currentIntensity);
        }

        private void PublishStopped()
        {
            _currentIntensity = 0f;
            Publish();
        }

        private void OnDestroy()
        {
            // Guards against a lingering pass reading a stale "on" struct if this
            // component is ever destroyed mid-ramp (domain reload, scene teardown).
            if (CurrentSettings.overlayIntensity > 0f) PublishStopped();
        }
    }
}
