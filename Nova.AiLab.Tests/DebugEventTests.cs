using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The event stream and the per-unit columns derived from it.
    /// <para>
    /// The stream exists to answer questions a sample cannot: at which exact
    /// tick did this unit get its order, when did it stop moving without
    /// arriving, who shot it. So the tests are about EXACTNESS — a spawn
    /// before every death, a damage that matches the health difference, a
    /// stuck report that only fires after the threshold — and about the one
    /// derived field staying honest about being derived.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DebugEventTests
    {
        private const ulong Seed = 0xA17E57DE57UL;

        private static MatchRunResult PlayedMatch()
        {
            // Long enough that the armies meet: without a fight there is no
            // damage, no death and no attack to test.
            var spec = new MatchSpec { Seed = Seed, TickBudget = 6000, ViewIntervalTicks = 25 };
            return MatchRun.Execute(spec);
        }

        /// <summary>
        /// THE RECONSTRUCTION GETS A POSITION, NOT A HEALTH VALUE.
        /// <para>
        /// <c>A</c>/<c>B</c> mean a different thing per kind: a death carries
        /// the position there, a DAMAGE carries the health it went from and to.
        /// The reach test read A/B as coordinates for both, so on a damage
        /// event it measured the distance from the attacker to a point a few
        /// raw units off the map ORIGIN. That corner is where slot 0's base
        /// stands and 120 cells from slot 1's, so the wide path could only ever
        /// name a unit of slot 0 — wherever the victim was standing.
        /// </para>
        /// <para>
        /// Checked against the RECORDED TRACK rather than against a plausible
        /// range: a health value of 55 sits inside the map's coordinate range
        /// too, so a range check would pass on the broken version. The track
        /// says where the unit really stood at that tick, and that is the only
        /// thing the reach test may be handed.
        /// </para>
        /// <para>
        /// Not asserted on the attributions themselves on purpose: the strict
        /// path names an attacker for almost every hit in a real match, so the
        /// wide path barely runs and a test on its output would pass either way.
        /// </para>
        /// </summary>
        [Test]
        public void TheAttackerReconstructionIsHandedAPositionAndNotAHealthValue()
        {
            MatchRunResult played = PlayedMatch();

            // The track, replayed the way the player replays it: absolutes set,
            // deltas accumulate, ended ids drop out.
            var position = new Dictionary<uint, (int X, int Y)>();
            var atTick = new Dictionary<uint, Dictionary<uint, (int X, int Y)>>();
            foreach (TrackFrame frame in played.Tracks)
            {
                foreach (TrackSample s in frame.Absolute) position[s.Id] = (s.X, s.Y);
                foreach (TrackSample s in frame.Delta)
                {
                    if (position.TryGetValue(s.Id, out (int X, int Y) p)) position[s.Id] = (p.X + s.X, p.Y + s.Y);
                }
                foreach (uint ended in frame.Ended) position.Remove(ended);
                atTick[frame.Tick] = new Dictionary<uint, (int X, int Y)>(position);
            }

            int checkedEvents = 0;
            foreach (DebugEvent e in played.Events)
            {
                // Damage only: at a DEATH the id has already left the track of
                // that tick, and its position is the previous tick's by design.
                if (e.Kind != DebugEventKind.Damage) continue;
                if (!atTick.TryGetValue(e.Tick, out Dictionary<uint, (int X, int Y)> snapshot)) continue;
                if (!snapshot.TryGetValue(e.Id, out (int X, int Y) walked)) continue;

                checkedEvents++;
                Assert.That((e.VictimX, e.VictimY), Is.EqualTo(walked),
                    $"the damage event at tick {e.Tick} has to carry where unit {e.Id} actually stood. " +
                    "A/B are the health it went from and to, and reading those as coordinates put the " +
                    "victim at the map origin — next to one corner base and 120 cells from the other");

                Assert.That(e.A, Is.GreaterThan(e.B), "and the health pair stays health");
            }

            Assert.That(checkedEvents, Is.GreaterThan(0), "the match has to have produced damage to test on");
        }

        [Test]
        public void TheStreamCarriesEveryEdgeThatExplainsAMatch()
        {
            var kinds = new HashSet<DebugEventKind>();
            foreach (DebugEvent e in PlayedMatch().Events) kinds.Add(e.Kind);

            foreach (DebugEventKind required in new[]
            {
                DebugEventKind.Spawn, DebugEventKind.Death, DebugEventKind.Damage,
                DebugEventKind.Order, DebugEventKind.PathGoal, DebugEventKind.MoveStart,
                DebugEventKind.MoveStop, DebugEventKind.AttackStart, DebugEventKind.SiteOpen,
                DebugEventKind.SiteDone, DebugEventKind.HarvestStart, DebugEventKind.CargoFull,
            })
            {
                Assert.That(kinds, Does.Contain(required), $"a played match must produce {required}");
            }
        }

        [Test]
        public void EveryDeathHasExactlyOneSpawnBeforeIt()
        {
            var spawned = new HashSet<uint>();
            var died = new HashSet<uint>();

            foreach (DebugEvent e in PlayedMatch().Events)
            {
                if (e.Kind == DebugEventKind.Spawn)
                {
                    Assert.That(spawned.Add(e.Id), Is.True,
                        $"entity {e.Id} spawned twice — the id version is what keeps a reused pool slot apart");
                }
                else if (e.Kind == DebugEventKind.Death)
                {
                    Assert.That(spawned, Does.Contain(e.Id), $"entity {e.Id} died without ever spawning");
                    Assert.That(died.Add(e.Id), Is.True, $"entity {e.Id} died twice");
                }
                else
                {
                    Assert.That(spawned, Does.Contain(e.Id),
                        $"entity {e.Id} produced a {e.Kind} before it existed");
                }
            }

            Assert.That(died.Count, Is.GreaterThan(0));
        }

        [Test]
        public void DamageChainsUpWithTheHealthItReports()
        {
            // Every damage and heal names the health BEFORE and AFTER. Read in
            // order they have to form one unbroken chain per entity — a gap
            // means an edge was missed, which is the one thing a per-tick log
            // must not do.
            var health = new Dictionary<uint, int>();

            foreach (DebugEvent e in PlayedMatch().Events)
            {
                switch (e.Kind)
                {
                    case DebugEventKind.Spawn:
                        health[e.Id] = e.C;
                        break;

                    case DebugEventKind.Damage:
                    case DebugEventKind.Heal:
                        Assert.That(health[e.Id], Is.EqualTo(e.A),
                            $"tick {e.Tick}, entity {e.Id}: the {e.Kind} starts at {e.A} but the last " +
                            $"known health was {health[e.Id]} — a health change went unreported");
                        Assert.That(e.B, Is.Not.EqualTo(e.A));
                        health[e.Id] = e.B;
                        break;

                    case DebugEventKind.Death:
                        Assert.That(e.C, Is.EqualTo(health[e.Id]),
                            $"tick {e.Tick}, entity {e.Id}: the death reports a different last health");
                        health.Remove(e.Id);
                        break;
                }
            }
        }

        [Test]
        public void TheDerivedAttackerIsAnEnemyThatExists_AndSaysWhenItIsUnsure()
        {
            MatchRunResult result = PlayedMatch();
            var slotOf = new Dictionary<uint, byte>();
            foreach (DebugEvent e in result.Events)
            {
                if (e.Kind == DebugEventKind.Spawn) slotOf[e.Id] = e.Slot;
            }

            int attributed = 0, sure = 0, total = 0;
            foreach (DebugEvent e in result.Events)
            {
                if (e.Kind != DebugEventKind.Damage && e.Kind != DebugEventKind.Death) continue;
                total++;
                if (e.By == null)
                {
                    Assert.That(e.BySure, Is.False, "no candidate can never be a sure one");
                    continue;
                }

                attributed++;
                if (e.BySure)
                {
                    sure++;
                    Assert.That(e.By.Count, Is.EqualTo(1), "sure means exactly one candidate, never more");
                }

                foreach (uint attacker in e.By)
                {
                    Assert.That(slotOf, Does.ContainKey(attacker),
                        $"tick {e.Tick}: the derivation named {attacker}, which never spawned");
                    Assert.That(slotOf[attacker], Is.Not.EqualTo(e.Slot),
                        $"tick {e.Tick}: the derivation named an attacker of the victim's own slot");
                }
            }

            Assert.That(total, Is.GreaterThan(0));
            Assert.That(attributed, Is.GreaterThan(total / 2),
                "the derivation is meant to answer most hits; if it does not, the reasoning behind it " +
                "no longer matches the combat system");
            Assert.That(sure, Is.GreaterThan(0));
        }

        [Test]
        public void StuckIsOnlyReportedAfterTheThresholdAndAlwaysEnds()
        {
            var stuckSince = new Dictionary<uint, uint>();
            foreach (DebugEvent e in PlayedMatch().Events)
            {
                if (e.Kind == DebugEventKind.Stuck)
                {
                    Assert.That(stuckSince.ContainsKey(e.Id), Is.False, "stuck reported twice without an end");
                    stuckSince[e.Id] = e.Tick;
                }
                else if (e.Kind == DebugEventKind.Unstuck)
                {
                    Assert.That(stuckSince, Does.ContainKey(e.Id), "unstuck without a stuck before it");
                    Assert.That(e.A, Is.GreaterThanOrEqualTo(DebugEventLog.StuckThresholdTicks),
                        "a stuck run shorter than the threshold must never have been reported");
                    stuckSince.Remove(e.Id);
                }
                else if (e.Kind == DebugEventKind.Death)
                {
                    stuckSince.Remove(e.Id);
                }
            }
        }

        [Test]
        public void EventJson_ContainsNoFloatingPointNumber()
        {
            foreach (DebugEvent e in PlayedMatch().Events)
            {
                string line = e.ToJsonLine();
                Assert.That(line, Does.Not.Contain("."),
                    "everything that leaves this lab is an integer; a decimal point means a float escaped:\n" + line);
            }
        }

        // ================================================================
        // The per-unit columns
        // ================================================================

        [Test]
        public void EveryTrackedUnitGetsARowAndTheRowsAreSortedById()
        {
            MatchRunResult result = PlayedMatch();
            Assert.That(result.Units.Count, Is.GreaterThan(0));

            uint previous = 0;
            var spawned = new HashSet<uint>();
            foreach (DebugEvent e in result.Events)
            {
                if (e.Kind == DebugEventKind.Spawn) spawned.Add(e.Id);
            }

            foreach (RouteMetrics row in result.Units)
            {
                Assert.That(row.Id, Is.GreaterThan(previous), "rows must be sorted, so a diff of two runs is readable");
                previous = row.Id;
                Assert.That(spawned, Does.Contain(row.Id));
                Assert.That(row.LastTick, Is.GreaterThanOrEqualTo(row.FirstTick));
                Assert.That(row.BlockedTicks, Is.LessThanOrEqualTo(row.MovingTicks),
                    "a unit cannot stand still more often than it wanted to move");
            }
        }

        [Test]
        public void DetourIsNeverBelowTheBeeline()
        {
            // Measured between the two points the unit actually visited, the
            // ratio cannot fall under 100. A value that does is a defect in
            // RouteMetrics, not a finding about the movement code.
            foreach (RouteMetrics row in PlayedMatch().Units)
            {
                if (row.DetourPercent < 0)
                {
                    Assert.That(row.Segments, Is.EqualTo(0));
                    continue;
                }
                Assert.That(row.DetourPercent, Is.GreaterThanOrEqualTo(100),
                    $"entity {row.Id} walked less than the straight line between its own end points");
                Assert.That(row.Segments, Is.GreaterThan(0));
            }
        }

        [Test]
        public void UnitRowsJson_ContainsNoFloatingPointNumber()
        {
            MatchRunResult result = PlayedMatch();
            string json = RunArtifacts.BuildUnitsJson(result);
            foreach (string line in json.Split('\n'))
            {
                if (!line.TrimStart().StartsWith("{\"id\":", StringComparison.Ordinal)) continue;
                Assert.That(line, Does.Not.Contain("."), "a decimal point means a float escaped:\n" + line);
            }
        }
    }
}
