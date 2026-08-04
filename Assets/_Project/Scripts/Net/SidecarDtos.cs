using System;

namespace FalsePositive.Net
{
    /// <summary>
    /// Mirrors the sidecar's additive JSON response from POST /turn (see
    /// Sidecar/app.py). Field names are snake_case to match JsonUtility's
    /// exact-name mapping — it has no attribute-based renaming the way
    /// Newtonsoft does, so don't "fix" these to C# convention; that would
    /// silently break deserialization (fields would just stay at their
    /// default values with no error).
    /// </summary>
    [Serializable]
    public sealed class SidecarTurnResponse
    {
        public bool ok;
        public string error;
        public string transcript;
        public string emotion;
        public float emotion_confidence;
        public SidecarProsodySignal prosody;
        public string reply_text;
        public string audio_b64;
        public int audio_sample_rate;
        public int audio_channels;
        public int stt_ms;
        public int ser_ms;
        public int llm_ms;
        public int tts_ms;
        public int total_ms;
    }

    [Serializable]
    public sealed class SidecarProsodySignal
    {
        public string version;
        public bool available;
        public bool reliable;
        public string reliability_reason;
        public string calibration_state;
        public int reference_turns;
        public bool reference_comparison_available;
        public float duration_seconds;
        public int onset_delay_ms;
        public float speech_ratio;
        public int long_pause_count;
        public float speech_rate_delta;
        public float pitch_variability;
        public float energy_variability;
        public float hubert_instability;
        public float hubert_baseline_distance;
        public float hubert_reference_change;
        public float arousal;
        public float tension;
        public float confidence_in_signal;
        public string trend;
        public SidecarClassProbabilities class_probabilities;
        public string[] flags;
    }

    [Serializable]
    public sealed class SidecarClassProbabilities
    {
        public float neutral;
        public float happy;
        public float angry;
        public float sad;
    }

    [Serializable]
    public sealed class SidecarHealthResponse
    {
        public string status;
        public bool models_loaded;
        public string version;
        public SidecarProsodyHealth prosody;
    }

    [Serializable]
    public sealed class SidecarProsodyHealth
    {
        public bool enabled;
        public bool available;
        public string model_id;
        public string device;
        public string orchestration_version;
        public string error;
    }
}
