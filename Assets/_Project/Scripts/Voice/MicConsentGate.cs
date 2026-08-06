namespace FalsePositive.Voice
{
    /// <summary>
    /// Guardrail #1 (docs/GAME_COMPLETION_PLAN.md §8): no capture starts before
    /// the consent card is accepted. Deliberately tiny and deliberately static
    /// so the guardrail is auditable in one file — MicrophoneService.TryBeginCapture
    /// is the only caller of Microphone.Start in the project, and it refuses to
    /// run unless Granted is true. Session-scoped; never persisted to disk or
    /// PlayerPrefs, so consent is asked again on every launch.
    /// </summary>
    public static class MicConsentGate
    {
        public static bool Granted { get; private set; }

        public static void Grant() => Granted = true;

        public static void Revoke() => Granted = false;
    }
}
