using System;
using System.Collections.Generic;
using System.IO;
using Nova.AI;
using Nova.Simulation.Combat;
using Nova.Simulation.Definitions;
using Nova.Simulation.State;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// E3 acceptance suite: the 2D view window.
    /// <para>
    /// The first test is the one the plan names explicitly (section 3.4): a run
    /// with and without the view window must deliver the same hash chain, "as
    /// a test, not as an intention". Everything the window ever shows is only
    /// worth looking at while that holds.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ViewRecorderTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const int ShortBudget = 2500;

        private static MatchSpec ShortSpec() => new MatchSpec { Seed = Seed, TickBudget = ShortBudget };

        // ================================================================
        // (a) THE HARD CONDITION: a pure observer
        // ================================================================

        [Test]
        public void RecordingTheView_DoesNotChangeTheHashChain()
        {
            MatchSpec quiet = ShortSpec();
            quiet.HashIntervalTicks = 100;

            MatchSpec watched = ShortSpec();
            watched.HashIntervalTicks = 100;
            watched.ViewIntervalTicks = 5;
            watched.RecordFog = true;

            MatchRunResult withoutView = MatchRun.Execute(quiet);
            MatchRunResult withView = MatchRun.Execute(watched);

            Assert.That(withView.View.Count, Is.GreaterThan(0), "the watched run must actually have captured");
            Assert.That(SweepRunner.Compare(withoutView, withView), Is.Null,
                "the view window is a pure observer: it reads the committed state, never writes back, and " +
                "is no part of the tick order, the state hash or a snapshot");
        }

        [Test]
        public void TheLiveViewAndTheFileReadTheSameFrames()
        {
            // Decision 10: both renderings share one frame stream. A second
            // capture path could drift, and then the terminal would show
            // something the recorded run does not contain.
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 50;

            var live = new List<ViewFrame>();
            MatchRunResult result = MatchRun.Execute(spec, live.Add);

            Assert.That(live.Count, Is.EqualTo(result.View.Count));
            for (int i = 0; i < live.Count; i++)
            {
                Assert.That(ReferenceEquals(live[i], result.View[i]), Is.True,
                    "the live callback must receive the very frame that lands in the file, not a copy of it");
            }
        }

        // ================================================================
        // (b) THE FRAMES THEMSELVES
        // ================================================================

        [Test]
        public void OpeningFrame_ShowsExactlyTheCanonicalOpening()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            ViewFrame opening = MatchRun.Execute(spec).View[0];

            Assert.That(opening.Tick, Is.EqualTo(0u));
            Assert.That(opening.Entities.Count, Is.EqualTo(4),
                "the D-077 opening is one HQ and one Builder per slot — nothing else");

            var shapes = new List<ViewShape>();
            foreach (ViewEntity e in opening.Entities) shapes.Add(e.Shape);
            Assert.That(shapes, Is.EquivalentTo(new[]
            {
                ViewShape.Building, ViewShape.Builder, ViewShape.Building, ViewShape.Builder,
            }));

            foreach (ViewEntity e in opening.Entities)
            {
                Assert.That(e.HealthPercent, Is.EqualTo(100), "nothing has taken damage at tick 0");
                Assert.That(e.Line, Is.EqualTo(ViewLine.None), "nobody has an order yet");
            }
        }

        [Test]
        public void Frames_EncodeActivityNotJustPosition()
        {
            // The reason the view exists: a position dump cannot explain a bad
            // match. Somewhere in a played match there must be harvest lines,
            // move lines and attack lines.
            MatchSpec spec = ShortSpec();
            spec.TickBudget = 6000;
            spec.ViewIntervalTicks = 25;

            var seen = new HashSet<ViewLine>();
            var shapes = new HashSet<ViewShape>();
            bool sawDamage = false, sawCargo = false;

            foreach (ViewFrame frame in MatchRun.Execute(spec).View)
            {
                foreach (ViewEntity e in frame.Entities)
                {
                    seen.Add(e.Line);
                    shapes.Add(e.Shape);
                    if (e.HealthPercent < 100) sawDamage = true;
                    if ((e.Flags & ViewFlags.ReturningCargo) != 0) sawCargo = true;
                }
            }

            Assert.That(seen, Does.Contain(ViewLine.Harvest), "harvesters must show their field");
            Assert.That(seen, Does.Contain(ViewLine.Move), "moving units must show where they are going");
            Assert.That(seen, Does.Contain(ViewLine.Attack), "an attacking unit must show its target");
            Assert.That(shapes, Does.Contain(ViewShape.ConstructionSite), "a site must be distinguishable from a building");
            Assert.That(shapes, Does.Contain(ViewShape.Combat));
            Assert.That(sawDamage, Is.True, "health is the brightness channel and must actually vary");
            Assert.That(sawCargo, Is.True, "the hollow fill marks a harvester on its way home");
        }

        [Test]
        public void AttackOrder_OutranksMoveOnTheSameUnit()
        {
            // Priority is what the plan lists: an attack order says more about
            // what a unit is doing than the move carrying it there.
            MatchSpec spec = ShortSpec();
            spec.TickBudget = 6000;
            spec.ViewIntervalTicks = 25;

            bool sawAttackingMover = false;
            foreach (ViewFrame frame in MatchRun.Execute(spec).View)
            {
                foreach (ViewEntity e in frame.Entities)
                {
                    if (e.Line != ViewLine.Attack || (e.Flags & ViewFlags.Moving) == 0) continue;
                    sawAttackingMover = true;
                }
            }

            Assert.That(sawAttackingMover, Is.True,
                "a unit that is both moving and attacking must draw the attack line, not the move line");
        }

        [Test]
        public void FogRuns_CoverTheWholeMapExactlyOnce()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 200;
            spec.RecordFog = true;

            MatchRunResult result = MatchRun.Execute(spec);
            int cells = spec.MapWidth * spec.MapHeight;

            foreach (ViewFrame frame in result.View)
            {
                Assert.That(frame.FogRle, Is.Not.Null);
                Assert.That(frame.FogRle.Length, Is.EqualTo(spec.Slots.Length), "one fog layer per slot");

                foreach (int[] runs in frame.FogRle)
                {
                    Assert.That(runs.Length % 2, Is.EqualTo(0), "run-length pairs are (count, state)");
                    int total = 0;
                    for (int i = 0; i < runs.Length; i += 2)
                    {
                        Assert.That(runs[i], Is.GreaterThan(0));
                        Assert.That(runs[i + 1], Is.InRange(0, 2), "vision state is Unexplored/Explored/Visible");
                        total += runs[i];
                    }
                    Assert.That(total, Is.EqualTo(cells),
                        $"the runs must reconstruct exactly {spec.MapWidth}x{spec.MapHeight} cells");
                }
            }
        }

        [Test]
        public void FogLayer_ActuallyOpensUpAsTheMatchRuns()
        {
            // A fog layer that never changes would pass the arithmetic test
            // above and still be useless.
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;
            spec.RecordFog = true;

            List<ViewFrame> view = MatchRun.Execute(spec).View;

            Assert.That(ExploredCells(view[0].FogRle[0]), Is.EqualTo(0),
                "before the first fog recompute nothing is explored");
            Assert.That(ExploredCells(view[view.Count - 1].FogRle[0]), Is.GreaterThan(0),
                "by the end the slot must have explored something");
        }

        private static int ExploredCells(int[] runs)
        {
            int explored = 0;
            for (int i = 0; i < runs.Length; i += 2)
            {
                if (runs[i + 1] != 0) explored += runs[i];
            }
            return explored;
        }

        [Test]
        public void ViewJson_ContainsNoFloatingPointNumber()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;
            spec.RecordFog = true;

            foreach (ViewFrame frame in MatchRun.Execute(spec).View)
            {
                string line = frame.ToJsonLine();
                Assert.That(line, Does.Not.Contain("."),
                    $"positions travel as Q16.16 raw integers; a decimal point means a float escaped:\n" +
                    line.Substring(0, Math.Min(200, line.Length)));
            }
        }

        // ================================================================
        // (c) THE PLAYER
        // ================================================================

        [Test]
        public void Player_IsWrittenBesideTheFramesAndIsSelfContained()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            MatchRunResult result = MatchRun.Execute(spec);
            string directory = Path.Combine(Path.GetTempPath(), "nova-ailab-tests", Guid.NewGuid().ToString("N"));

            try
            {
                RunArtifacts.Write(directory, spec, result);

                string playerPath = Path.Combine(directory, HtmlPlayer.FileName);
                Assert.That(File.Exists(playerPath), Is.True, "the player travels with the frames");
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.ViewFileName)), Is.True);

                string html = File.ReadAllText(playerPath);
                // "No build, no server, no dependency" is the whole design.
                Assert.That(html, Does.Not.Contain("http://"));
                Assert.That(html, Does.Not.Contain("https://"));
                Assert.That(html, Does.Not.Contain("<script src"));
                Assert.That(html, Does.Contain(RunArtifacts.ViewFileName), "it must know which file to load");
                Assert.That(html, Does.Contain("DIAGNOSIS").Or.Contain("diagnosis"),
                    "the page states what a lab run is worth");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// The scoreboard weighs the two armies with the AI's own formula, and
        /// the two numbers it needs — attack damage and firing interval — are
        /// baked into the page per slot and role.
        /// <para>
        /// THIS IS THE GUARD AGAINST A SECOND DEFINITION TABLE. A page that
        /// carried its own values would keep answering ""who is stronger"" long
        /// after the definitions moved, and it would answer wrong. So the test
        /// reads the numbers back out of the generated page and holds them
        /// against <see cref="WeaponProfiles"/> for every role of both
        /// factions, and against <see cref="CombatStrength"/> for the one
        /// multiplication the page does itself.
        /// </para>
        /// </summary>
        [Test]
        public void PlayerCarriesTheWeaponNumbersItWeighsArmiesWith()
        {
            MatchSpec spec = ShortSpec();
            string html = HtmlPlayer.Build(spec.MapWidth, spec.MapHeight, Seed, spec.Slots);

            const string marker = "const WEAPONS = ";
            int start = html.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the page must carry the table");
            int end = html.IndexOf(";", start, StringComparison.Ordinal);
            string json = html.Substring(start + marker.Length, end - start - marker.Length);

            // [[ [d,c], [d,c], … ], … ] — one array per slot, one pair per role.
            string[] perSlot = json.Trim('[', ']').Split(new[] { "],[[" }, StringSplitOptions.None);
            Assert.That(perSlot.Length, Is.EqualTo(spec.Slots.Length), "one row per seat");

            for (int slot = 0; slot < spec.Slots.Length; slot++)
            {
                FactionId faction = spec.Slots[slot].Faction;
                string[] pairs = perSlot[slot].Trim('[', ']').Split(new[] { "],[" }, StringSplitOptions.None);

                for (int role = 0; role < pairs.Length; role++)
                {
                    string[] values = pairs[role].Trim('[', ']').Split(',');
                    int damage = int.Parse(values[0]);
                    int cooldown = int.Parse(values[1]);

                    WeaponProfile weapon = WeaponProfiles.Get(faction, (UnitRole)role);
                    Assert.That(damage, Is.EqualTo(weapon.AttackDamage),
                        $"slot {slot} ({faction}) role {(UnitRole)role}: attack damage");
                    Assert.That(cooldown, Is.EqualTo(weapon.AttackCooldownTicks),
                        $"slot {slot} ({faction}) role {(UnitRole)role}: firing interval");

                    // And the page's one line of arithmetic, at a health the
                    // formula cannot round away: it must land where the AI's
                    // own strength lands, or the bar contradicts the wave gate
                    // it is meant to explain.
                    const int health = 37;
                    int fromTheTable = damage <= 0 || cooldown <= 0 ? 0 : damage * health / cooldown;
                    Assert.That(fromTheTable, Is.EqualTo(CombatStrength.Of(faction, (UnitRole)role, health)),
                        $"slot {slot} ({faction}) role {(UnitRole)role}: strength at {health} health");
                }
            }
        }

        /// <summary>
        /// The bar says what the gathered army weighs and what the wave gate
        /// wants for it. Both numbers are recomputed in the page, so both are
        /// pinned here: the inputs against the profile, and the arithmetic
        /// against <see cref="WaveStrengthGate"/> itself.
        /// <para>
        /// The ring radius is the one that matters most. ""Gathered"" is not
        /// ""every combat unit"" — it is what stands inside
        /// <c>StagingDistanceCells + StagingToleranceCells</c> of the own HQ,
        /// and a page that got that radius wrong would show a full wave next to
        /// an AI that is still waiting, which is worse than showing nothing.
        /// </para>
        /// </summary>
        [Test]
        public void PlayerCarriesTheWaveNumbersAndRepeatsTheGate()
        {
            MatchSpec spec = ShortSpec();
            string html = HtmlPlayer.Build(spec.MapWidth, spec.MapHeight, Seed, spec.Slots);

            const string marker = "const WAVE = ";
            int start = html.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the page must carry the wave rules");
            string json = html.Substring(start + marker.Length,
                html.IndexOf(";", start, StringComparison.Ordinal) - start - marker.Length);

            string[] seats = json.Trim('[', ']').Split(new[] { "},{" }, StringSplitOptions.None);
            Assert.That(seats.Length, Is.EqualTo(spec.Slots.Length), "one rule set per seat");

            for (int i = 0; i < spec.Slots.Length; i++)
            {
                SlotSpec seat = spec.Slots[i];
                AiFactionProfile profile = seat.Profile;

                Assert.That(Number(seats[i], "ring"),
                    Is.EqualTo(profile.Profile.StagingDistanceCells + profile.Profile.StagingToleranceCells),
                    "the staging ring is what tells a gathered unit from one that already marched");
                Assert.That(Number(seats[i], "waveSize"), Is.EqualTo(profile.Profile.WaveSize));
                Assert.That(Number(seats[i], "points"), Is.EqualTo(profile.Profile.WaveStrengthPoints));
                Assert.That(Number(seats[i], "cap"), Is.EqualTo(profile.TargetArmySize));
                Assert.That(Number(seats[i], "cadence"), Is.EqualTo(profile.Profile.DecisionTickInterval));
                Assert.That(Number(seats[i], "produced"),
                    Is.EqualTo(CombatStrength.OfFullHealth(seat.Faction, UnitRole.BasicInfantry)),
                    "one more unit is worth what the Barracks actually builds");
                Assert.That(Number(seats[i], "producerRole"), Is.EqualTo((int)UnitRole.Barracks));

                // And the arithmetic the page repeats, over the states a match
                // walks through: an empty ring, a filling one, everything out,
                // and more alive than the cap allows for at home.
                int points = Number(seats[i], "points");
                int cap = Number(seats[i], "cap");
                int produced = Number(seats[i], "produced");
                foreach (int gathered in new[] { 0, 1, 6, 12, 15 })
                {
                    foreach (int committed in new[] { 0, 4, 12 })
                    {
                        foreach (bool canProduce in new[] { true, false })
                        {
                            long strength = (long)gathered * produced;

                            // Line for line what waveState() computes.
                            int free = canProduce ? cap - committed - gathered : 0;
                            if (free < 0) free = 0;
                            long attainable = strength + (long)free * produced;
                            long need = points < attainable ? points : attainable;

                            Assert.That(need, Is.EqualTo(WaveStrengthGate.Threshold(
                                    points, strength, gathered, committed, produced, cap, canProduce)),
                                $"threshold at gathered {gathered}, committed {committed}, produce {canProduce}");
                            Assert.That(strength >= need, Is.EqualTo(WaveStrengthGate.IsReady(
                                    points, strength, gathered, committed, produced, cap, canProduce)),
                                $"verdict at gathered {gathered}, committed {committed}, produce {canProduce}");
                        }
                    }
                }
            }
        }

        /// <summary>One integer field out of the page's own JSON, without a JSON dependency.</summary>
        private static int Number(string json, string key)
        {
            int start = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"the page must carry '{key}'");
            start += key.Length + 3;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.Parse(json.Substring(start, end - start));
        }

        [Test]
        public void NoViewRequested_WritesNoViewArtifacts()
        {
            MatchSpec spec = ShortSpec();
            MatchRunResult result = MatchRun.Execute(spec);
            string directory = Path.Combine(Path.GetTempPath(), "nova-ailab-tests", Guid.NewGuid().ToString("N"));

            try
            {
                RunArtifacts.Write(directory, spec, result);
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.ViewFileName)), Is.False);
                Assert.That(File.Exists(Path.Combine(directory, HtmlPlayer.FileName)), Is.False,
                    "a player without frames would be a broken page in the artifact directory");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        // ================================================================
        // (d) THE TERMINAL VIEW
        // ================================================================

        [Test]
        public void TerminalView_DrawsEveryFrameWithoutThrowing()
        {
            // The live view runs inside the match loop; an exception there
            // would kill a run that was otherwise fine.
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            var terminal = new TerminalView(spec.MapWidth, spec.MapHeight, columns: 40, rows: 20);
            var writer = new StringWriter();
            System.Console.SetOut(writer);
            try
            {
                MatchRun.Execute(spec, terminal.Draw);
            }
            finally
            {
                var standardOut = new StreamWriter(System.Console.OpenStandardOutput()) { AutoFlush = true };
                System.Console.SetOut(standardOut);
            }

            string drawn = writer.ToString();
            Assert.That(drawn, Does.Contain("slot 0"), "the per-slot header line is part of the live view");
            Assert.That(drawn, Does.Contain("#"), "buildings must appear in the grid");
        }
    }
}
