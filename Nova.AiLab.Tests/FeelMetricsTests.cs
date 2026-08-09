using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Simulation.Definitions;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

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
        // (a2) THE PAIRING ITSELF: damage and answer in ONE tick
        // ================================================================

        /// <summary>
        /// A unit that is hit AND re-ordered in the same tick produces TWO
        /// events, not one: the order closes the open pair, the hit opens the
        /// next one.
        /// <para>
        /// This was an if/else-if, so only the first of the two ever fired and
        /// the fresh hit was neither answered nor unanswered — it was dropped.
        /// The shape it dropped is the one the column exists for: a unit
        /// walking out of fire is being shot WHILE it receives new orders. Over
        /// n such ticks the tally came out at roughly n/2, and nothing in the
        /// artifact said half the events were missing.
        /// </para>
        /// <para>
        /// Driven by hand rather than through a match: the branch needs damage
        /// and a re-order on the SAME tick, every tick, and no real match
        /// arranges that on demand. The collector is a pure reader, so feeding
        /// it a hand-written state is feeding it exactly what it would read.
        /// </para>
        /// </summary>
        [Test]
        public void ADamagedAndReOrderedUnitCountsBothEventsInTheSameTick()
        {
            const int ticks = 20;
            MultiSlotAiHost host = MultiSlotAiHost.Build(new MatchSpec
            {
                Seed = Seed,
                TickBudget = ShortBudget,
                CountIntents = false,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = FactionId.Alliance, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = FactionId.Legion, Controller = SlotController.Scripted },
                },
            });

            Assert.That(SimDefinitions.TryGetUnit(FactionId.Alliance, UnitRole.BasicInfantry,
                out SimUnitDefinition def), Is.True);
            EntityId id = host.Entities.SpawnUnit(
                0, new Transform2D(SimFixed.FromInt(10), SimFixed.FromInt(10)),
                def.MoveSpeed, maxHealth: 10000, role: def.Role);

            var collector = new TraceCollector(host);

            for (uint tick = 1; tick <= ticks; tick++)
            {
                ref UnitState unit = ref host.Entities.RawUnits[id.Index];
                unit.CurrentHealth -= 10;                              // hit
                unit.TargetGridPos = new GridPos2D(20 + (int)tick, 20); // and pulled out
                collector.OnTick(tick);
            }
            collector.FinishReactions();

            ReactionTally tally = collector.Reactions[0];
            Assert.That(tally.Events + tally.Unanswered, Is.EqualTo(ticks),
                "every one of the 20 hits has to end up either answered or unanswered — the if/else-if " +
                "version scored about half of them and lost the rest without a trace");
            Assert.That(tally.Events, Is.EqualTo(ticks - 1),
                "19 hits were answered by the next tick's order; the 20th was still open when the run ended");
            Assert.That(tally.LatencySumTicks, Is.EqualTo(ticks - 1),
                "each answer came one tick after its hit — a latency of 0 stays impossible, " +
                "an intent cannot answer damage that has not happened yet");
            Assert.That(tally.Unanswered, Is.EqualTo(1),
                "damage still open when the match ends is unanswered, not forgotten");
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
