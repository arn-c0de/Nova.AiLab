using System.Collections.Generic;
using Nova.AI.Data;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The goal recording: what the AI intended, written down where it was
    /// decided.
    /// <para>
    /// The first test is the same hard condition every observer in this lab
    /// lives under, and it matters more here than for the view window: this one
    /// is handed INTO the simulation rather than reading it from outside. If
    /// attaching it could move a single decision, every artifact of every
    /// recorded run would be describing a match that only exists while somebody
    /// is watching.
    /// </para>
    /// <para>
    /// The rest guard the property that makes the file worth reading at all:
    /// the goal in the row is the goal that produced the orders in the same row.
    /// A panel drawing a goal a unit is not under is worse than a panel drawing
    /// nothing, because it looks like an answer.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class GoalRecorderTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const int ShortBudget = 2500;

        private static MatchSpec ShortSpec() => new MatchSpec { Seed = Seed, TickBudget = ShortBudget };

        /// <summary>The spec that switches the goal recording on — it rides along with the view window.</summary>
        private static MatchSpec WatchedSpec()
        {
            MatchSpec spec = ShortSpec();
            spec.HashIntervalTicks = 100;
            spec.ViewIntervalTicks = 25;
            spec.TrackIntervalTicks = 1;
            return spec;
        }

        // ================================================================
        // (a) THE HARD CONDITION: watching is not playing
        // ================================================================

        [Test]
        public void RecordingTheGoals_DoesNotChangeTheHashChain()
        {
            MatchSpec quiet = ShortSpec();
            quiet.HashIntervalTicks = 100;

            MatchRunResult withoutGoals = MatchRun.Execute(quiet);
            MatchRunResult withGoals = MatchRun.Execute(WatchedSpec());

            Assert.That(withGoals.Goals, Is.Not.Empty, "the watched run recorded no goal at all");
            Assert.That(SweepRunner.Compare(withoutGoals, withGoals), Is.Null,
                "the goal observer is handed into the AI and must still be unable to move it: " +
                "same hash chain, same end state, same deciding tick");
        }

        // ================================================================
        // (b) The rows describe the run they came from
        // ================================================================

        /// <summary>
        /// Every judged unit carries a name — the recorder must never write
        /// <see cref="GoalKind.None"/>, which would mean the catalogue does not
        /// cover a decision the AI took.
        /// </summary>
        [Test]
        public void EveryRecordedUnitCarriesANamedGoal()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());

            int judged = 0;
            foreach (GoalFrame frame in run.Goals)
            {
                foreach (AiUnitGoal unit in frame.Units)
                {
                    judged++;
                    Assert.That(unit.Goal, Is.Not.EqualTo(GoalKind.None),
                        $"tick {frame.Tick}, slot {frame.Slot}, unit {unit.EntityRaw} came out unnamed");
                }
            }
            Assert.That(judged, Is.GreaterThan(0), "no unit was judged in the whole run");
        }

        /// <summary>
        /// A unit is judged at most once per seat and cadence, and it is judged
        /// by the seat that owns it.
        /// <para>
        /// Both AI seats decide inside the same tick, one after the other in
        /// ascending slot order. A unit landing in the neighbour's frame would
        /// leave a file that still parses and a panel that still draws — with
        /// the goals on the wrong side of the map, which is the sort of display
        /// error nobody recognises as one.
        /// </para>
        /// </summary>
        [Test]
        public void AUnitIsJudgedOncePerSeatAndCadence_ByTheSeatThatOwnsIt()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());

            // Which seat a unit belongs to, taken from the view frames rather
            // than from the goal file itself: asking the recording to confirm
            // its own claim would prove nothing.
            var owner = new Dictionary<uint, int>();
            foreach (ViewFrame frame in run.View)
            {
                foreach (ViewEntity entity in frame.Entities) owner[entity.Id] = entity.Slot;
            }

            var seen = new HashSet<uint>();
            foreach (GoalFrame frame in run.Goals)
            {
                seen.Clear();
                foreach (AiUnitGoal unit in frame.Units)
                {
                    Assert.That(seen.Add(unit.EntityRaw), Is.True,
                        $"unit {unit.EntityRaw} was judged twice at tick {frame.Tick} by slot {frame.Slot}");
                    if (!owner.TryGetValue(unit.EntityRaw, out int slot)) continue;
                    Assert.That(slot, Is.EqualTo(frame.Slot),
                        $"slot {frame.Slot} handed a goal to unit {unit.EntityRaw}, which belongs to slot {slot}");
                }
            }
        }

        /// <summary>
        /// The orders in a row are the ones its goal produces. Checked against
        /// the ARMY row of the same frame, not against a second copy of the
        /// effect table — a copy would agree with itself forever.
        /// </summary>
        [Test]
        public void TheRecordedOrdersAreTheOnesTheRecordedGoalProduces()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());

            foreach (GoalFrame frame in run.Goals)
            {
                AiArmyGoal army = frame.Army;
                foreach (AiUnitGoal unit in frame.Units)
                {
                    string where = $"tick {frame.Tick}, slot {frame.Slot}, unit {unit.EntityRaw} under {unit.Goal}";
                    switch (unit.Goal)
                    {
                        case GoalKind.Attack:
                            Assert.That(unit.MoveCellX, Is.EqualTo(army.MoveCellX), where);
                            Assert.That(unit.MoveCellY, Is.EqualTo(army.MoveCellY), where);
                            Assert.That(unit.AttackTargetRaw, Is.EqualTo(army.TargetRaw), where);
                            break;
                        case GoalKind.Hold:
                            Assert.That(unit.MoveCellX, Is.EqualTo(-1), where);
                            Assert.That(unit.MoveCellY, Is.EqualTo(-1), where);
                            break;
                        default:
                            Assert.That(unit.MoveCellX, Is.EqualTo(army.StagingCellX), where);
                            Assert.That(unit.MoveCellY, Is.EqualTo(army.StagingCellY), where);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// A seat that does not act judges nobody — and still reports, because
        /// "the army is below its squad threshold" is an answer one looks for at
        /// least as often as the other one.
        /// </summary>
        [Test]
        public void ASeatThatDoesNotActReportsThat_AndJudgesNobody()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());

            bool idleSeen = false;
            foreach (GoalFrame frame in run.Goals)
            {
                if (frame.Army.Engages) continue;
                idleSeen = true;
                Assert.That(frame.Units, Is.Empty,
                    $"tick {frame.Tick}, slot {frame.Slot}: the army does not act and still handed out goals");
            }
            Assert.That(idleSeen, Is.True,
                "no idle decision was recorded — the opening ticks are below the squad threshold and must be");
        }

        /// <summary>
        /// AN UNWEIGHED FRAME CARRIES ZEROS, AND THEY ARE NOT COUNTS.
        /// <para>
        /// <c>ResolveArmyPosture</c> returns before it counts anything when the
        /// seat is below its squad threshold, and again when waves are switched
        /// off — so the row leaves <c>WaveMode</c> at <c>Off</c> and the four
        /// numbers beside it at zero. That is not "no units in the ring" and not
        /// "this profile runs without waves"; it is "nobody counted".
        /// </para>
        /// <para>
        /// This test pins the difference because a reader cannot see it in the
        /// file. The player read those zeros as data and told a seat with five
        /// living units that it had no army, and a seat with a 1.200-point gate
        /// that its waves were off — for 44 decisions of the canonical run. The
        /// rule a reader has to hold to is here: <c>WaveMode == Off</c> means
        /// the numbers say nothing, whatever they contain.
        /// </para>
        /// </summary>
        [Test]
        public void AFrameThatWeighedNoWaveCarriesNoNumbers()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());

            bool unweighedSeen = false;
            foreach (GoalFrame frame in run.Goals)
            {
                AiArmyGoal army = frame.Army;
                if (army.Engages) continue;

                unweighedSeen = true;
                string where = $"tick {frame.Tick}, slot {frame.Slot}";
                Assert.That(army.WaveMode, Is.EqualTo(WaveGateMode.Off),
                    $"{where}: the army step never ran and the row still names a wave rule");
                Assert.That(army.Gathered, Is.Zero, where);
                Assert.That(army.Committed, Is.Zero, where);
                Assert.That(army.GatheredStrength, Is.Zero, where);
                Assert.That(army.WaveThreshold, Is.Zero, where);
            }
            Assert.That(unweighedSeen, Is.True, "no unweighed decision in the run — the opening ticks are");
        }

        // ================================================================
        // (c) The file
        // ================================================================

        /// <summary>
        /// Every column of a row is an integer, and the row carries the number
        /// of columns the readers expect.
        /// <para>
        /// The whole artifact set holds to "no float ever leaves the
        /// simulation", because comparing two runs is then arithmetic instead of
        /// luck. This file writes booleans as 0/1 and enums as their number for
        /// the same reason, and the check has to be on the TEXT — a check on the
        /// object would pass whatever the writer did with it.
        /// </para>
        /// </summary>
        [Test]
        public void EveryColumnOfARowIsAnInteger()
        {
            MatchRunResult run = MatchRun.Execute(WatchedSpec());
            Assert.That(run.Goals, Is.Not.Empty);

            const int armyColumns = 13;
            const int unitColumns = 10;

            foreach (GoalFrame frame in run.Goals)
            {
                string line = frame.ToJsonLine();
                Assert.That(line, Does.Not.Contain("."), $"a decimal point in a goal row: {line}");
                Assert.That(line, Does.Not.Contain("E+"), $"an exponent in a goal row: {line}");

                Assert.That(CountColumns(line, "\"a\":["), Is.EqualTo(armyColumns), line);
                if (frame.Units.Count == 0) continue;
                Assert.That(CountColumns(line, "\"u\":[["), Is.EqualTo(unitColumns), line);
            }
        }

        /// <summary>Columns of the first array that starts at <paramref name="marker"/>.</summary>
        private static int CountColumns(string line, string marker)
        {
            int start = line.IndexOf(marker, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"'{marker}' is missing from {line}");
            start += marker.Length;
            int end = line.IndexOf(']', start);
            Assert.That(end, Is.GreaterThan(start), line);

            int columns = 1;
            for (int i = start; i < end; i++)
            {
                if (line[i] == ',') columns++;
            }
            return columns;
        }
    }
}
