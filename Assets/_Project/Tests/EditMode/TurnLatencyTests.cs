using FalsePositive.Net;
using NUnit.Framework;

namespace FalsePositive.Tests
{
    /// <summary>Guards the arithmetic behind the F1 overlay's client-side line.
    ///
    /// The bug these exist to prevent is a silent one: every span here is a
    /// plausible-looking number, so a sign error or a swapped subtraction shows
    /// up as a latency report that reads fine and points at the wrong stage.
    /// </summary>
    public sealed class TurnLatencyTests
    {
        [Test]
        public void TransportMsIsWireTimeThatWasNotServerWork()
        {
            var latency = new TurnLatency { wireMs = 4966, serverMs = 3387 };

            // Upload plus download plus TLS: the part a closer region or a
            // smaller body can actually reduce.
            Assert.AreEqual(1579, latency.TransportMs);
        }

        [Test]
        public void TransportMsNeverGoesNegativeWhenTheServerOutrunsTheWire()
        {
            // The two clocks are independent — the server's and the player's —
            // so rounding, or a stopwatch started a frame late, can leave
            // serverMs a hair above wireMs. That is measurement noise, not a
            // negative network cost, and it must not surface as one.
            var latency = new TurnLatency { wireMs = 3387, serverMs = 3390 };

            Assert.AreEqual(0, latency.TransportMs);
        }

        [Test]
        public void IsCompleteOnlyOnceATotalHasBeenMeasured()
        {
            Assert.IsFalse(default(TurnLatency).IsComplete);
            Assert.IsTrue(new TurnLatency { totalMs = 1 }.IsComplete);
        }

        [Test]
        public void PublishExposesTheLastTurnWithoutASceneReference()
        {
            var latency = new TurnLatency
            {
                vadWaitMs = 700,
                wireMs = 4966,
                serverMs = 3387,
                decodeMs = 40,
                totalMs = 5666,
            };

            TurnLatency.Publish(latency);

            Assert.AreEqual(5666, TurnLatency.Last.totalMs);
            Assert.AreEqual(1579, TurnLatency.Last.TransportMs);
        }

        [Test]
        public void ToStringReportsTheTotalSeparatelyFromTheServersOwnFigure()
        {
            var latency = new TurnLatency
            {
                vadWaitMs = 700,
                wireMs = 4966,
                serverMs = 3387,
                decodeMs = 40,
                totalMs = 5666,
            };

            string text = latency.ToString();

            // The whole point of the overlay line: the server's 3387ms and the
            // player's 5666ms are both visible, so the 2.3s the server cannot
            // see is impossible to miss.
            StringAssert.Contains("server=3387ms", text);
            StringAssert.Contains("TOTAL=5666ms", text);
            StringAssert.Contains("net=1579ms", text);
        }

        [Test]
        public void ToStringReportsDownloadSizeSoGzipIsVisibleWithoutAProxy()
        {
            // 205KB is a gzipped reply, ~320KB is the same reply uncompressed.
            // The overlay has to make those two distinguishable at a glance,
            // because nothing else in the game can tell them apart.
            var compressed = new TurnLatency { totalMs = 1, wireBytes = 210_432 };

            StringAssert.Contains("down=205KB", compressed.ToString());
        }
    }
}
