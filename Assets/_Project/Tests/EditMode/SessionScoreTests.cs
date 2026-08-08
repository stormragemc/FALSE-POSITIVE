using FalsePositive.Flow;
using NUnit.Framework;

namespace FalsePositive.Tests
{
    /// <summary>
    /// The §7 bookkeeping and the §8 credibility formula — the half of the trap
    /// mechanic that can be checked without a microphone. Whether the officer
    /// actually catches a fabrication is a live-model question; whether a caught
    /// one is counted once, and what it does to the ending thresholds, is not.
    /// </summary>
    public class SessionScoreTests
    {
        private const int TotalMarks = 7;

        private static SessionScore FullCoverage()
        {
            var score = new SessionScore();
            score.ObserveMarkCoverage(TotalMarks);
            return score;
        }

        // --- fabrication bookkeeping ---------------------------------------

        [Test]
        public void ARepeatedTrapIsCountedOnce()
        {
            var score = new SessionScore();

            Assert.IsTrue(score.RecordFabrication(TrapIds.Lock));
            Assert.IsFalse(score.RecordFabrication(TrapIds.Lock),
                "the officer re-baits the same trap across P2 and P3 by design");
            Assert.AreEqual(1, score.CaughtFabrications);
        }

        [Test]
        public void DistinctTrapsAccumulate()
        {
            var score = new SessionScore();
            score.RecordFabrication(TrapIds.Door);
            score.RecordFabrication(TrapIds.Time);

            Assert.AreEqual(2, score.CaughtFabrications);
        }

        [Test]
        public void UnknownIdsAreIgnored()
        {
            var score = new SessionScore();

            Assert.IsFalse(score.RecordFabrication("trap_bogus"));
            Assert.IsFalse(score.RecordFabrication(null));
            Assert.IsFalse(score.RecordFabrication(string.Empty));
            Assert.AreEqual(0, score.CaughtFabrications);
        }

        [Test]
        public void ResetClearsCaughtTrapsAndCoverage()
        {
            var score = FullCoverage();
            score.RecordFabrication(TrapIds.Window);
            score.UpdateCredibility(TotalMarks);

            score.Reset();

            Assert.AreEqual(0, score.CaughtFabrications);
            Assert.AreEqual(0, score.MarkCoverage);
            Assert.AreEqual(1f, score.Credibility);
        }

        [Test]
        public void MarkCoverageKeepsItsHighWaterMark()
        {
            // StoryMarkTracker is Reset() at the top of every live phase, so a
            // later, lower reading must not erase what P2 actually covered.
            var score = new SessionScore();
            score.ObserveMarkCoverage(6);
            score.ObserveMarkCoverage(0);

            Assert.AreEqual(6, score.MarkCoverage);
        }

        // --- credibility (docs/STORY_SCRIPT.md §8) --------------------------

        [Test]
        public void ACleanFullCoverageRunClearsTheAccusationThreshold()
        {
            var score = FullCoverage();
            score.UpdateCredibility(TotalMarks);

            Assert.GreaterOrEqual(score.Credibility, 0.6f);
        }

        [Test]
        public void OneFabricationWithComposureIntactStillAccuses()
        {
            // §8 makes TWO fabrications decisive and the ending selector
            // enforces that independently, so the formula must not double-count
            // it: a single slip by an otherwise composed witness is survivable.
            var score = FullCoverage();
            score.RecordFabrication(TrapIds.Time);
            score.UpdateCredibility(TotalMarks);

            Assert.GreaterOrEqual(score.Credibility, 0.6f);
        }

        [Test]
        public void TwoFabricationsCanNeverReachTheAccusationThreshold()
        {
            // Holds at the most generous composure there is, which is what makes
            // it an invariant rather than a tuning coincidence: the best case is
            // 0.54 against a 0.6 gate. Note it stays ABOVE §8's 0.45 floor —
            // §8's own "two-plus caught fabrications" clause is what sends this
            // run to E_DAVID, and that clause belongs to the ending selector.
            var score = FullCoverage();
            score.RecordFabrication(TrapIds.Time);
            score.RecordFabrication(TrapIds.Door);
            score.UpdateCredibility(TotalMarks);

            Assert.AreEqual(1f, score.Composure, 0.0001f, "no reliable turns yet");
            Assert.Less(score.Credibility, 0.6f);
        }

        [Test]
        public void StonewallingFallsBelowTheFloor()
        {
            var score = new SessionScore();
            score.UpdateCredibility(TotalMarks);

            Assert.Less(score.Credibility, 0.45f);
        }

        [Test]
        public void ComposureAloneCannotSinkACleanRun()
        {
            // §8: composure never decides an ending on its own. Worst possible
            // composure on a clean, fully covered run must still accuse.
            var score = FullCoverage();
            for (int turn = 0; turn < 5; turn++)
            {
                score.RecordTurn(new TurnRecord(
                    turn, GamePhase.P2_Recall, "said something", "asked something",
                    tension: 1f, arousal: 1f, signalConfidence: 1f, prosodyReliable: true));
            }
            score.UpdateCredibility(TotalMarks);

            Assert.AreEqual(0f, score.Composure, 0.0001f);
            Assert.GreaterOrEqual(score.Credibility, 0.6f);
        }

        [Test]
        public void ComposureMovesCredibilityByAtMostATenth()
        {
            var calm = FullCoverage();
            calm.UpdateCredibility(TotalMarks);

            var tense = FullCoverage();
            for (int turn = 0; turn < 5; turn++)
            {
                tense.RecordTurn(new TurnRecord(
                    turn, GamePhase.P2_Recall, "said something", "asked something",
                    tension: 1f, arousal: 1f, signalConfidence: 1f, prosodyReliable: true));
            }
            tense.UpdateCredibility(TotalMarks);

            // Calm sits at composure 1.0 (+0.1), tense at 0.0 (-0.1): the full
            // span the §8 G6 requirement allows, and no more.
            Assert.AreEqual(0.2f, calm.Credibility - tense.Credibility, 0.0001f);
        }

        [Test]
        public void CredibilityStaysWithinZeroToOne()
        {
            var score = FullCoverage();
            foreach (string trapId in TrapIds.All) score.RecordFabrication(trapId);
            score.UpdateCredibility(TotalMarks);

            Assert.GreaterOrEqual(score.Credibility, 0f);
            Assert.LessOrEqual(score.Credibility, 1f);
        }

        [Test]
        public void ATotalOfZeroMarksIsNotADivideByZero()
        {
            var score = FullCoverage();
            score.UpdateCredibility(0);

            Assert.GreaterOrEqual(score.Credibility, 0f);
            Assert.LessOrEqual(score.Credibility, 1f);
        }
    }
}
