using System.Collections.Generic;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The four game-feel columns (NEXT-STEPS.md section 7).
    /// <para>
    /// Two properties carry this file, and both are about honesty rather than
    /// arithmetic. Measuring must still cost nothing — the reaction tracking
    /// runs EVERY tick over every entity, which is by far the most invasive
    /// thing the collector does, so the pure-observer condition is asserted
    /// again here and not merely inherited. And "not measurable" must stay
    /// distinguishable from "measured as zero": an exchange ratio of 0 means
    /// the candidate killed nothing, while <c>-1</c> means it lost nothing and
    /// there is no ratio at all. Collapsing the two would invert the reading.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class FeelMetricsTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const int ShortBudget = 3000;

        private static MatchSpec TracedSpec() => new MatchSpec
        {
            Seed = Seed,
            TickBudget = ShortBudget,
            TraceIntervalTicks = 50,
            HashIntervalTicks = 100,
        };

        // ================================================================
        // (a) THE PER-TICK PASS MUST NOT MOVE A SINGLE HASH
        // ================================================================

        [Test]
        public void TrackingReactions_DoesNotChangeTheHashChain()
        {
            var quiet = new MatchSpec { Seed = Seed, TickBudget = ShortBudget, HashIntervalTicks = 100 };
            MatchSpec traced = TracedSpec();

            MatchRunResult withoutTrace = MatchRun.Execute(quiet);
            MatchRunResult withTrace = MatchRun.Execute(traced);

            Assert.That(withTrace.Feel.Count, Is.GreaterThan(0), "the traced run must actually have produced feel metrics");
            Assert.That(SweepRunner.Compare(withoutTrace, withTrace), Is.Null,
                "the per-tick reaction pass reads health and move targets of every entity every tick — " +
                "if that ever writes back, every feel number was measured on a different match than the game plays");
        }

        [Test]
        public void WithoutATrace_ThereAreNoFeelMetricsRatherThanZeros()
        {
            MatchRunResult untraced = MatchRun.Execute(
                new MatchSpec { Seed = Seed, TickBudget = ShortBudget });

            Assert.That(untraced.Feel, Is.Empty,
                "three of the four columns are per-interval or per-tick derivations; a run without a " +
                "trace has to report nothing instead of zeros that read as measurements");
        }

        // ================================================================
        // (b) -1 IS "NOT MEASURABLE", NEVER "ZERO"
        // ================================================================

        [Test]
        public void ASlotThatLostNothingHasNoExchangeRatio()
        {
            List<FeelMetrics> feel = FeelMetrics.Compute(
                new[] { Sample(0, ownLost: 0, otherLost: 7, intents: 10) },
                reactions: null, finalTick: 600, slotCount: 2);

            Assert.That(feel[0].ExchangeRatioPercent, Is.EqualTo(-1),
                "no own losses means there is no ratio — 0 would claim it killed nothing");
        }

        [Test]
        public void ASlotThatKilledNothingHasARatioOfZero()
        {
            List<FeelMetrics> feel = FeelMetrics.Compute(
                new[] { Sample(0, ownLost: 5, otherLost: 0, intents: 10) },
                reactions: null, finalTick: 600, slotCount: 2);

            Assert.That(feel[0].ExchangeRatioPercent, Is.EqualTo(0),
                "five own losses and no enemy losses IS a measurement, and it is zero");
        }

        [Test]
        public void ASlotThatNeverReactedReportsMinusOne()
        {
            var tallies = new[] { new ReactionTally { Unanswered = 12 }, new ReactionTally() };

            List<FeelMetrics> feel = FeelMetrics.Compute(
                new[] { Sample(0, ownLost: 3, otherLost: 3, intents: 10) },
                tallies, finalTick: 600, slotCount: 2);

            Assert.That(feel[0].MeanReactionLatencyTicks, Is.EqualTo(-1),
                "a mean over zero events is not zero ticks — it is no measurement");
            Assert.That(feel[0].UnansweredDamageEvents, Is.EqualTo(12),
                "and the damage that got no answer is the finding, so it has to survive into the column");
        }

        // ================================================================
        // (c) DENSITY IS THE SHAPE OF THE CURVE, NOT ITS HEIGHT
        // ================================================================

        /// <summary>
        /// The same total losses once as a trickle and once as two battles.
        /// This is the whole reason the column exists: the deciding tick and
        /// the loss total cannot tell these two matches apart, and a player
        /// tells them apart in seconds.
        /// </summary>
        [Test]
        public void TheTrickleAndTheBattleHaveTheSameLossesAndADifferentShape()
        {
            var trickle = new List<MetricSample>();
            for (int i = 0; i <= 6; i++) trickle.Add(Sample((uint)(i * 50), ownLost: i, otherLost: 0, intents: i));

            var battle = new List<MetricSample>
            {
                Sample(0, 0, 0, 0), Sample(50, 0, 0, 1), Sample(100, 3, 0, 2),
                Sample(150, 3, 0, 3), Sample(200, 3, 0, 4), Sample(250, 6, 0, 5), Sample(300, 6, 0, 6),
            };

            FeelMetrics trickleFeel = FeelMetrics.Compute(trickle, null, 300, 2)[0];
            FeelMetrics battleFeel = FeelMetrics.Compute(battle, null, 300, 2)[0];

            Assert.Multiple(() =>
            {
                Assert.That(trickle[trickle.Count - 1].Slots[0].UnitsLost,
                    Is.EqualTo(battle[battle.Count - 1].Slots[0].UnitsLost),
                    "the two matches have to lose the same number of entities, or this proves nothing");
                Assert.That(trickleFeel.CombatIntervals, Is.EqualTo(6));
                Assert.That(trickleFeel.LargestLossJump, Is.EqualTo(1));
                Assert.That(battleFeel.CombatIntervals, Is.EqualTo(2));
                Assert.That(battleFeel.LargestLossJump, Is.EqualTo(3));
            });
        }

        // ================================================================
        // (d) APM IS THE INTENT COLUMN READ AS A RATE
        // ================================================================

        [Test]
        public void ActionsPerMinuteIsIntentsOverTicksOnTheTenHertzClock()
        {
            List<FeelMetrics> feel = FeelMetrics.Compute(
                new[] { Sample(6000, ownLost: 1, otherLost: 1, intents: 240) },
                reactions: null, finalTick: 6000, slotCount: 2);

            // 240 intents over 6000 ticks = 600 seconds of simulated time at
            // 10 Hz = 10 minutes, so 24 actions per minute.
            Assert.That(feel[0].ActionsPerMinute, Is.EqualTo(24));
        }

        // ----------------------------------------------------------------

        /// <summary>One metric sample with the three fields the feel columns read.</summary>
        private static MetricSample Sample(uint tick, int ownLost, int otherLost, int intents) => new MetricSample
        {
            Tick = tick,
            Slots = new[]
            {
                new SlotMetrics { Slot = 0, UnitsLost = ownLost, IntentsSubmitted = intents },
                new SlotMetrics { Slot = 1, UnitsLost = otherLost },
            },
        };
    }
}
