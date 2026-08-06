using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// Starts (or resumes) an AudioSource's playback every time this
    /// GameObject becomes active — not just once via AudioSource.playOnAwake,
    /// which only fires on the object's first-ever Awake(). SceneRouter's
    /// additive-load-then-SetActive model means Memory_* scene props are
    /// often saved inactive and only activated later when their scene
    /// becomes current, and Awake() does not re-fire on repeat activation —
    /// only OnEnable() does. Caught via a Play-mode check: playOnAwake
    /// sources on the clock/fireplace/wind-bed props showed isPlaying=false
    /// even though a manual .Play() call worked fine, confirming the audio
    /// pipeline itself was never the problem.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class LoopOnEnable : MonoBehaviour
    {
        private void OnEnable()
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source != null && !source.isPlaying) source.Play();
        }
    }
}
