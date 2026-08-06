namespace FalsePositive.Voice
{
    /// <summary>The result of one completed calibration pass — see MicCalibration.</summary>
    public readonly struct CalibrationResult
    {
        public float NoiseFloor { get; }
        public float LoudReferenceRms { get; }
        public float PeakRms { get; }
        public string DeviceName { get; }

        public CalibrationResult(float noiseFloor, float loudReferenceRms, float peakRms, string deviceName)
        {
            NoiseFloor = noiseFloor;
            LoudReferenceRms = loudReferenceRms;
            PeakRms = peakRms;
            DeviceName = deviceName;
        }
    }

    /// <summary>
    /// Runtime-only calibration results, owned by GameFlowDirector. Deliberately
    /// NOT written into InterrogationConfig: that is a ScriptableObject, and a
    /// play-mode write to one of its fields in the Editor persists to the
    /// .asset file on disk — a single noisy-room calibration would otherwise
    /// get committed as the project default. This object lives and dies with
    /// the playthrough instead. See docs/GAME_COMPLETION_PLAN.md A4.
    /// </summary>
    public sealed class VoiceCalibrationState
    {
        public bool IsCalibrated { get; private set; }
        public float NoiseFloor { get; private set; }
        public float LoudReferenceRms { get; private set; }
        public string DeviceName { get; private set; }

        public void Apply(in CalibrationResult result)
        {
            NoiseFloor = result.NoiseFloor;
            LoudReferenceRms = result.LoudReferenceRms;
            DeviceName = result.DeviceName;
            IsCalibrated = true;
        }

        public void Clear()
        {
            IsCalibrated = false;
            NoiseFloor = 0f;
            LoudReferenceRms = 0f;
            DeviceName = null;
        }
    }
}
