using System;
using UnityEngine;

namespace FalsePositive.Net
{
    /// <summary>One turn's latency, split into the spans a fix can actually move.
    ///
    /// This exists because the number the debug overlay used to show —
    /// <c>total_ms</c> from the sidecar — is a server-only stopwatch. It starts
    /// when FastAPI has the whole uploaded body in hand and stops before the
    /// response is written, so it cannot see the upload, the download, or the
    /// silence the player waits through before the upload even begins. Measured
    /// 8 Aug against us-central1, the server called it 3387ms while the player
    /// waited 5666ms. Chasing <c>total_ms</c> alone therefore optimises the
    /// smaller half of the problem; every span below is one the player feels.
    /// </summary>
    [Serializable]
    public struct TurnLatency
    {
        /// <summary>Silence the VAD held before it accepted the utterance as
        /// finished. Pure client-side cost, invisible to the backend, and the
        /// cheapest span to shorten — it is a config value, not work.</summary>
        public int vadWaitMs;

        /// <summary>SendWebRequest start to finish: upload, server work and
        /// download, as one span. The only part measured from the player's
        /// side of the wire.</summary>
        public int wireMs;

        /// <summary><c>total_ms</c> as reported by the sidecar — server work
        /// only, and a strict subset of <see cref="wireMs"/>.</summary>
        public int serverMs;

        /// <summary>Base64 decode plus PCM-to-AudioClip, on the main thread.
        /// Small, but it lands after everything else and so is felt as part of
        /// the wait rather than hidden behind it.</summary>
        public int decodeMs;

        /// <summary>The number that matches the player's experience: the moment
        /// they stopped speaking to the moment the officer's voice starts.</summary>
        public int totalMs;

        /// <summary>Bytes UnityWebRequest actually pulled off the wire.
        ///
        /// Here to settle one question the server cannot answer: the sidecar
        /// gzips replies over 1KB, but only for a client that asked. Unity does
        /// not expose whether it negotiated <c>Accept-Encoding</c>, and setting
        /// that header by hand suppresses auto-decompression on some backends,
        /// so this is measured rather than assumed. Measured 9 Aug against
        /// us-central1, one reply was 205KB compressed and 323KB not — so a
        /// number near 320KB means the compression is not reaching the player
        /// and the ~110ms it saves is still on the table.</summary>
        public int wireBytes;

        /// <summary>Everything on the wire that was not server work: TLS, the
        /// request body up, the reply body down. Large bodies pay the round trip
        /// several times over because TCP slow-start has to open the window, so
        /// this is what moving region or shrinking the payload buys back.</summary>
        public int TransportMs => Mathf.Max(0, wireMs - serverMs);

        public bool IsComplete => totalMs > 0;

        /// <summary>The most recently completed turn, for the debug overlay and
        /// for anything measuring without a reference to the scene graph. Only
        /// ever written by <see cref="DialogueManager"/> on a successful turn,
        /// so a failed turn leaves the last good measurement in place rather
        /// than a misleading zero.</summary>
        public static TurnLatency Last { get; private set; }

        public static void Publish(TurnLatency latency) => Last = latency;

        /// <summary>Deliberately ordered as the turn happens, so a regression
        /// shows up as one span growing rather than a total that moved for an
        /// unknown reason.</summary>
        public override string ToString() =>
            $"vad={vadWaitMs}ms wire={wireMs}ms (net={TransportMs}ms server={serverMs}ms) " +
            $"decode={decodeMs}ms down={wireBytes / 1024}KB TOTAL={totalMs}ms";
    }
}
