using Nova.AI.Data;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The live session and its interventions.
    /// <para>
    /// ONE TEST HERE IS THE CONDITION UNDER WHICH THE PANEL MAY EXIST AT ALL,
    /// and it is named after that: a session plus its <c>overrides.ndjson</c>,
    /// run again, has to come out bit for bit the same. If it does not, the
    /// intervention was not an INPUT to the run but hidden state inside it — and
    /// then every session anybody ever recorded describes a match that cannot be
    /// checked, which is worse than having no panel.
    /// </para>
    /// <para>
    /// The other three exist so that the first one cannot pass for the wrong
    /// reason: a session nobody touched must be the ordinary match, an
    /// intervention must actually change something, and the protocol must
    /// survive being written down and read back.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class LiveOverrideTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const int Budget = 2500;

        private static MatchSpec Spec() => new MatchSpec { Seed = Seed, TickBudget = Budget };

        // ================================================================
        // (a) The condition
        // ================================================================

        [Test]
        public void ASessionAndItsProtocolReplayBitForBit()
        {
            var session = new LiveMatch(Spec());
            session.Start();

            // A session shaped like a real one: watch a while, hold one seat,
            // watch some more, let it go again, run on.
            session.Step(900);
            session.Force(slot: 1, entityRaw: 0, goal: GoalKind.Hold);
            session.Step(400);
            session.Force(slot: 1, entityRaw: 0, goal: GoalKind.None);
            session.Step(500);

            ulong live = session.Host.Kernel.CalculateStateHash();
            uint tick = session.Tick;
            string protocol = session.Overrides.ToNdjson();
            Assert.That(protocol, Is.Not.Empty, "nothing was written down, so nothing can be replayed");

            var replay = new LiveMatch(Spec(), GoalOverrideLog.Parse(protocol));
            replay.Start();
            replay.Step((int)tick);

            Assert.Multiple(() =>
            {
                Assert.That(replay.Tick, Is.EqualTo(tick), "the replay did not reach the same tick");
                Assert.That(replay.Host.Kernel.CalculateStateHash(), Is.EqualTo(live),
                    "the session and its protocol disagree — then an intervention is hidden state, " +
                    "not an input, and no recorded session can be checked by anybody");
            });
        }

        // ================================================================
        // (b) …and it cannot pass for the wrong reason
        // ================================================================

        /// <summary>
        /// A session nobody touched is the ordinary match. Without this the
        /// replay test would still pass if interventions did nothing whatsoever.
        /// </summary>
        [Test]
        public void ASessionNobodyTouchedIsTheOrdinaryMatch()
        {
            MatchRunResult plain = MatchRun.Execute(Spec());

            var session = new LiveMatch(Spec());
            session.Start();
            session.Step(Budget);

            Assert.Multiple(() =>
            {
                Assert.That(session.Overrides.Intervened, Is.False, "nobody intervened, and it says otherwise");
                Assert.That(session.Host.Kernel.CalculateStateHash(), Is.EqualTo(plain.FinalStateHash),
                    "holding a match open changed it, which makes every live observation worthless");
                Assert.That(session.Tick, Is.EqualTo(plain.FinalTick));
            });
        }

        /// <summary>
        /// Forcing a goal changes the match. This is the negative control for
        /// the replay test: two runs that were always going to be identical
        /// prove nothing about reproducing an intervention.
        /// </summary>
        [Test]
        public void ForcingAGoalChangesTheMatch()
        {
            var untouched = new LiveMatch(Spec());
            untouched.Start();
            untouched.Step(1800);

            var held = new LiveMatch(Spec());
            held.Start();
            held.Step(900);
            held.Force(slot: 1, entityRaw: 0, goal: GoalKind.Hold);
            held.Step(900);

            Assert.That(held.Overrides.Intervened, Is.True);
            Assert.That(held.Host.Kernel.CalculateStateHash(),
                Is.Not.EqualTo(untouched.Host.Kernel.CalculateStateHash()),
                "holding a whole seat for 900 ticks left the match exactly as it was — " +
                "then the mask is not reaching the AI and the replay test is vacuous");
        }

        /// <summary>
        /// The protocol survives being written and read. It is parsed by hand
        /// rather than by a serializer, for the same reason every artifact of
        /// this lab is written by hand: nothing may quietly turn into a float.
        /// </summary>
        [Test]
        public void TheProtocolSurvivesBeingWrittenAndReadBack()
        {
            var log = new GoalOverrideLog();
            log.Record(120, 1, 0, GoalKind.Hold);
            log.Record(340, GoalOverrideEntry.AllSlots, 1044, GoalKind.Retreat);
            log.Record(500, 0, 1044, GoalKind.None);

            GoalOverrideLog read = GoalOverrideLog.Parse(log.ToNdjson());

            Assert.That(read.Entries.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(read.Entries[i].FromTick, Is.EqualTo(log.Entries[i].FromTick), $"row {i}");
                Assert.That(read.Entries[i].Slot, Is.EqualTo(log.Entries[i].Slot), $"row {i}");
                Assert.That(read.Entries[i].EntityRaw, Is.EqualTo(log.Entries[i].EntityRaw), $"row {i}");
                Assert.That(read.Entries[i].Goal, Is.EqualTo(log.Entries[i].Goal), $"row {i}");
            }
        }

        /// <summary>
        /// An entry is in force from a TICK, and a release gives the unit back
        /// to the AI. Checked on the mask itself, because this is the one place
        /// where "when does it start" is decided.
        /// </summary>
        [Test]
        public void AnEntryIsInForceFromItsTick_AndAReleaseGivesTheUnitBack()
        {
            var log = new GoalOverrideLog();
            log.Record(100, GoalOverrideEntry.AllSlots, 1044, GoalKind.Hold);
            log.Record(200, GoalOverrideEntry.AllSlots, 1044, GoalKind.None);

            log.AdvanceTo(99);
            Assert.That(log.ResolveGoal(1044), Is.EqualTo(GoalKind.None), "in force one tick early");

            log.AdvanceTo(100);
            Assert.That(log.ResolveGoal(1044), Is.EqualTo(GoalKind.Hold), "not in force on its own tick");

            log.AdvanceTo(199);
            Assert.That(log.ResolveGoal(1044), Is.EqualTo(GoalKind.Hold), "released one tick early");

            log.AdvanceTo(200);
            Assert.That(log.ResolveGoal(1044), Is.EqualTo(GoalKind.None), "the release did not take");

            log.AdvanceTo(200);
            Assert.That(log.ResolveGoal(1045), Is.EqualTo(GoalKind.None), "an entry named one unit and reached another");
        }
    }
}
