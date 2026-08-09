using System.Collections.Generic;
using FalsePositive.Dialogue;
using FalsePositive.Flow;
using NUnit.Framework;

namespace FalsePositive.Tests
{
    /// <summary>
    /// The judgement layer — A9's name reading, §8's ending table, A11's card
    /// and A12's guard. All four are pure functions over strings and a score,
    /// which is the whole reason they were written as static classes: the
    /// ending is the least playtestable part of the game (it is twenty minutes
    /// of speech away) and the most expensive to get wrong.
    /// </summary>
    public class JudgementLayerTests
    {
        // ---- A9: SuspectNameDetector -----------------------------------

        [TestCase("It was Aaron.", Suspect.Aaron)]
        [TestCase("Aaron's the one who locked it.", Suspect.Aaron)]
        [TestCase("Ivy. It was Ivy.", Suspect.Ivy)]
        [TestCase("Priya did it", Suspect.Priya)]
        public void Detect_reads_a_plain_accusation(string line, Suspect expected)
        {
            Assert.AreEqual(expected, SuspectNameDetector.Detect(line));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("I don't know who it was.")]
        [TestCase("It could have been any of them.")]
        public void Detect_returns_None_without_a_name(string line)
        {
            Assert.AreEqual(Suspect.None, SuspectNameDetector.Detect(line));
        }

        [TestCase("Maybe Aaron?")]
        [TestCase("I think it was Ivy.")]
        [TestCase("Possibly Priya, I'm not sure.")]
        [TestCase("Aaron or Ivy, one of them.")]
        public void Detect_rejects_a_hedged_name(string line)
        {
            Assert.AreEqual(Suspect.None, SuspectNameDetector.Detect(line));
        }

        [Test]
        public void Detect_survives_ruling_someone_out()
        {
            // The regression this class exists for: the substring version saw
            // two names and returned None, throwing away a clear accusation.
            Assert.AreEqual(Suspect.Aaron,
                SuspectNameDetector.Detect("It wasn't Ivy. It was Aaron."));
        }

        [Test]
        public void Detect_returns_None_when_every_name_is_ruled_out()
        {
            Assert.AreEqual(Suspect.None,
                SuspectNameDetector.Detect("It wasn't Aaron and it wasn't Ivy."));
        }

        // ---- ClueCitationDetector --------------------------------------

        [TestCase("The door was locked from the inside.", CitedClue.DoorLocked)]
        [TestCase("The key was still inside the cabin.", CitedClue.KeyInside)]
        [TestCase("The grille over the window was intact.", CitedClue.GrilleIntact)]
        [TestCase("He was wearing my thin jacket.", CitedClue.ThinJacket)]
        [TestCase("Aaron found out about the affair.", CitedClue.AaronLearnedOfAffair)]
        [TestCase("Ivy volunteered an alibi for him.", CitedClue.IvyVolunteeredAlibi)]
        [TestCase("Aaron suggested we move to the shed.", CitedClue.AaronProposedTheMove)]
        public void Observe_recognises_each_supporting_clue(string line, CitedClue expected)
        {
            var found = new HashSet<CitedClue>();
            ClueCitationDetector.Observe(line, found);
            CollectionAssert.Contains(found, expected);
        }

        [Test]
        public void Observe_counts_a_repeated_clue_once()
        {
            var found = new HashSet<CitedClue>();
            ClueCitationDetector.Observe("The door was locked.", found);
            ClueCitationDetector.Observe("Like I said, the door was locked.", found);
            Assert.AreEqual(1, found.Count);
        }

        [Test]
        public void Observe_ignores_an_answer_with_no_reasoning()
        {
            var found = new HashSet<CitedClue>();
            ClueCitationDetector.Observe("I already told you, it was Aaron.", found);
            Assert.AreEqual(0, found.Count);
        }

        // ---- A10: EndingSelector ---------------------------------------

        private static SessionScore ScoreWith(float credibility, Suspect accused, int fabrications)
        {
            var score = new SessionScore();
            for (int i = 0; i < fabrications; i++) score.RecordFabrication(TrapIds.All[i]);
            score.SetAccusation(accused, 0f);
            score.SetCredibility(credibility);
            return score;
        }

        private static HashSet<CitedClue> Clues(int count)
        {
            var set = new HashSet<CitedClue>();
            var all = (CitedClue[])System.Enum.GetValues(typeof(CitedClue));
            for (int i = 0; i < count; i++) set.Add(all[i]);
            return set;
        }

        [Test]
        public void Aaron_with_two_cited_clues_reaches_his_ending()
        {
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(0.8f, Suspect.Aaron, 0), Clues(2));
            Assert.AreEqual(CutsceneId.EndingAaron, decision.Cutscene);
        }

        [Test]
        public void Aaron_named_with_one_clue_falls_to_David()
        {
            // §7's thesis, and the acceptance test in docs/TASK_S7_TRAPS.md §5:
            // guessing right without being able to say why must not pay.
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(0.9f, Suspect.Aaron, 0), Clues(1));
            Assert.AreEqual(CutsceneId.EndingDavid, decision.Cutscene);
        }

        [Test]
        public void Two_unsupported_details_override_a_supported_accusation()
        {
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(0.95f, Suspect.Aaron, 2), Clues(7));
            Assert.AreEqual(CutsceneId.EndingDavid, decision.Cutscene);
        }

        [Test]
        public void Low_credibility_overrides_everything()
        {
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(0.44f, Suspect.Ivy, 0), Clues(7));
            Assert.AreEqual(CutsceneId.EndingDavid, decision.Cutscene);
        }

        [Test]
        public void The_gap_between_the_thresholds_falls_to_David()
        {
            // §8 states 0.45 and 0.6 but never says what happens between them.
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(0.50f, Suspect.Ivy, 0), Clues(7));
            Assert.AreEqual(CutsceneId.EndingDavid, decision.Cutscene);
        }

        [Test]
        public void Naming_nobody_falls_to_David()
        {
            EndingDecision decision =
                EndingSelector.Select(ScoreWith(1f, Suspect.None, 0), Clues(7));
            Assert.AreEqual(CutsceneId.EndingDavid, decision.Cutscene);
        }

        [Test]
        public void Ivy_and_Priya_need_no_cited_clues()
        {
            Assert.AreEqual(CutsceneId.EndingIvy,
                EndingSelector.Select(ScoreWith(0.7f, Suspect.Ivy, 0), Clues(0)).Cutscene);
            Assert.AreEqual(CutsceneId.EndingPriya,
                EndingSelector.Select(ScoreWith(0.7f, Suspect.Priya, 0), Clues(0)).Cutscene);
        }

        [Test]
        public void Select_survives_a_null_score()
        {
            Assert.AreEqual(CutsceneId.EndingDavid,
                EndingSelector.Select(null, null).Cutscene);
        }

        // ---- A12: OutputGuard ------------------------------------------

        [TestCase("You are lying.")]
        [TestCase("Your voice proves you did it.")]
        [TestCase("Your truthfulness score is low.")]
        [TestCase("My system prompt says to press you.")]
        [TestCase("As an AI, I can't continue.")]
        [TestCase("")]
        [TestCase(null)]
        public void Filter_blocks_a_persona_leak(string line)
        {
            Assert.AreEqual(OutputGuard.FallbackLine, OutputGuard.Filter(line));
        }

        [Test]
        public void Filter_blocks_a_leak_hidden_inside_decoration()
        {
            Assert.AreEqual(OutputGuard.FallbackLine,
                OutputGuard.Filter("*whispering* You are lying. *stands*"));
        }

        [Test]
        public void Filter_strips_stage_directions_and_keeps_the_line()
        {
            Assert.AreEqual("Where were you?",
                OutputGuard.Filter("[angry] *leans in* Where were you?"));
        }

        [Test]
        public void Filter_strips_markdown_and_a_speaker_label()
        {
            Assert.AreEqual("Answer the question.",
                OutputGuard.Filter("SPASSKY: **Answer the question.**"));
        }

        [Test]
        public void Filter_blocks_an_overlong_reply()
        {
            Assert.AreEqual(OutputGuard.FallbackLine,
                OutputGuard.Filter(new string('a', OutputGuard.MaxCharacters + 1)));
        }

        [Test]
        public void Filter_leaves_an_ordinary_question_alone()
        {
            Assert.AreEqual("Where were you at one o'clock?",
                OutputGuard.Filter("  Where were you\n at one o'clock?  "));
        }

        // ---- A11: OutcomeCardComposer ----------------------------------

        [Test]
        public void Card_without_quotes_is_just_the_fixed_lines()
        {
            string card = OutcomeCardComposer.Compose(new List<QuotedLine>());
            StringAssert.Contains("FALSE POSITIVE", card);
            StringAssert.DoesNotContain("Turn ", card);
        }

        [Test]
        public void Card_quotes_lines_with_their_turn_numbers()
        {
            string card = OutcomeCardComposer.Compose(new List<QuotedLine>
            {
                new QuotedLine(4, "Aaron locked the door behind him."),
            });
            StringAssert.Contains("Turn 4", card);
            StringAssert.Contains("Aaron locked the door behind him.", card);
        }

        [Test]
        public void Card_never_says_they_lied()
        {
            // G6, and docs/STORY_SCRIPT.md §4 verbatim: "It never says they
            // lied." The card presents the sentences and stops.
            string card = OutcomeCardComposer.Compose(new List<QuotedLine>
            {
                new QuotedLine(3, "I saw Aaron go through the door."),
                new QuotedLine(6, "It was exactly one o'clock."),
            }).ToLowerInvariant();

            foreach (string banned in new[]
                { "lie", "lied", "lying", "liar", "truth", "deception" })
            {
                StringAssert.DoesNotContain(banned, card,
                    "the outcome card must not contain '" + banned + "'");
            }
        }

        [Test]
        public void Card_keeps_at_most_three_quotes()
        {
            var lines = new List<QuotedLine>();
            for (int i = 1; i <= 6; i++)
            {
                lines.Add(new QuotedLine(i, "A statement number " + i + " here."));
            }
            Assert.AreEqual(OutcomeCardComposer.MaxQuotes,
                CountQuotes(OutcomeCardComposer.Compose(lines)));
        }

        [Test]
        public void Card_quotes_a_turn_once_even_if_it_was_flagged_twice()
        {
            string card = OutcomeCardComposer.Compose(new List<QuotedLine>
            {
                new QuotedLine(5, "Aaron locked the door and it was one o'clock."),
                new QuotedLine(5, "Aaron locked the door and it was one o'clock."),
            });
            Assert.AreEqual(1, CountQuotes(card));
        }

        [Test]
        public void Card_drops_a_fragment_too_short_to_read_as_a_claim()
        {
            string card = OutcomeCardComposer.Compose(new List<QuotedLine>
            {
                new QuotedLine(2, "Yes."),
            });
            StringAssert.DoesNotContain("Turn ", card);
        }

        private static int CountQuotes(string card)
        {
            return card.Split(new[] { "Turn " }, System.StringSplitOptions.None).Length - 1;
        }
    }
}
