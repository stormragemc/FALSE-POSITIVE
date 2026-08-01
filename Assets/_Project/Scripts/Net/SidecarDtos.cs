using System;

namespace FalsePositive.Net
{
    /// <summary>
    /// Mirrors the sidecar's flat JSON response from POST /turn (see
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
    public sealed class SidecarHealthResponse
    {
        public string status;
        public bool models_loaded;
        public string version;
    }
}
