using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// E2 acceptance suite: metrics, artifacts and the parallel sweep.
    /// <para>
    /// The load-bearing tests here are the two that prove MEASURING COSTS
    /// NOTHING — the trace collector and the counting transport must not move
    /// a single hash. The plan puts that condition on the view recorder in
    /// section 3.4 ("as a test, not as an intention"); it applies to every
    /// observer the lab attaches, and an observer that changes the match would
    /// invalidate everything measured through it.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MetricsAndSweepTests
    {
        private const ulong Seed = 0xA17E57DE57UL;

        /// <summary>Long enough for an economy, an army and a fight; short enough to keep the suite quick.</summary>
        private const int ShortBudget = 3000;

        private static MatchSpec ShortSpec(ulong seed = Seed) => new MatchSpec
        {
            Seed = seed,
            TickBudget = ShortBudget,
        };

        // ================================================================
        // (a) OBSERVING MUST NOT CHANGE THE MATCH
        // ================================================================

        [Test]
        public void CollectingMetrics_DoesNotChangeTheHashChain()
        {
            MatchSpec quiet = ShortSpec();
            quiet.HashIntervalTicks = 100;

            MatchSpec observed = ShortSpec();
            observed.HashIntervalTicks = 100;
            observed.TraceIntervalTicks = 10;

            MatchRunResult withoutTrace = MatchRun.Execute(quiet);
            MatchRunResult withTrace = MatchRun.Execute(observed);

            Assert.That(withTrace.Trace.Count, Is.GreaterThan(0), "the observed run must actually have sampled");
            Assert.That(SweepRunner.Compare(withoutTrace, withTrace), Is.Null,
                "a run with and without the trace collector must produce the identical hash chain — " +
                "the collector is a pure observer, and if that ever stops being true every number it " +
                "ever produced was measured on a different match than the game plays");
        }

        [Test]
        public void CountingIntents_DoesNotChangeTheHashChain()
        {
            MatchSpec canonical = ShortSpec();
            canonical.HashIntervalTicks = 100;
            canonical.CountIntents = false;

            MatchSpec counting = ShortSpec();
            counting.HashIntervalTicks = 100;
            counting.CountIntents = true;

            MatchRunResult withCanonicalTransport = MatchRun.Execute(canonical);
            MatchRunResult withCountingTransport = MatchRun.Execute(counting);

            Assert.That(SweepRunner.Compare(withCanonicalTransport, withCountingTransport), Is.Null,
                "the counting transport must be indistinguishable from AiPeerCommandTransport — " +
                "it exists only because a verdict can be counted nowhere else, and it may cost nothing for it");
        }

        [Test]
        public void CountingTransport_ReplacesTheCanonicalOne_NeverBoth()
        {
            MatchSpec counting = ShortSpec();
            counting.CountIntents = true;
            MultiSlotAiHost counted = MultiSlotAiHost.Build(counting);

            MatchSpec canonical = ShortSpec();
            canonical.CountIntents = false;
            MultiSlotAiHost plain = MultiSlotAiHost.Build(canonical);

            foreach (SlotPeer peer in counted.Peers)
            {
                Assert.That(peer.IntentCounter, Is.Not.Null);
                Assert.That(peer.Transport, Is.Null, "exactly one transport binds to a peer ingress");
            }
            foreach (SlotPeer peer in plain.Peers)
            {
                Assert.That(peer.Transport, Is.Not.Null);
                Assert.That(peer.IntentCounter, Is.Null);
            }
        }

        // ================================================================
        // (b) THE METRICS THEMSELVES
        // ================================================================

        [Test]
        public void Trace_StartsFromTheCanonicalOpeningPosition()
        {
            MatchSpec spec = ShortSpec();
            spec.TraceIntervalTicks = 100;

            MetricSample opening = MatchRun.Execute(spec).Trace[0];

            Assert.That(opening.Tick, Is.EqualTo(0u));
            foreach (SlotMetrics slot in opening.Slots)
            {
                Assert.That(slot.Credits, Is.EqualTo(3000L), "the D-077 start balance");
                Assert.That(slot.BuildingsByRole[0], Is.EqualTo(1), "one completed HQ per slot");
                Assert.That(slot.FieldReserveAE, Is.EqualTo(CanonicalOpening.FieldReserveAE));
                Assert.That(slot.ArmySize, Is.EqualTo(0), "the opening has one Builder and no army");
                Assert.That(slot.SitesOpen, Is.EqualTo(0));
                Assert.That(slot.VisibleEnemyUnits, Is.EqualTo(0), "the bases start outside each other's sight");
            }
        }

        [Test]
        public void Trace_ShowsTheEconomyAndArmyComingUp()
        {
            MatchSpec spec = ShortSpec();
            spec.TraceIntervalTicks = 100;

            MatchRunResult result = MatchRun.Execute(spec);
            SlotMetrics last = result.Trace[result.Trace.Count - 1].Slots[1];

            Assert.That(last.Harvesters, Is.GreaterThanOrEqualTo(1), "the AI must have harvesters working by now");
            Assert.That(last.BuildingsByRole[(int)Nova.Simulation.State.UnitRole.Refinery - SlotMetrics.FirstBuildingRole],
                Is.GreaterThanOrEqualTo(1), "the Refinery is the D-077 opening build");
            Assert.That(last.ArmySize, Is.GreaterThan(0), "the Barracks must have produced infantry");
            Assert.That(last.IntentsSubmitted, Is.GreaterThan(0), "the AI acts through the sealed command path");
            Assert.That(last.IntentsSubmitted, Is.EqualTo(last.IntentsAccepted + last.IntentsRejected),
                "every submitted intent gets exactly one verdict");
        }

        [Test]
        public void Trace_CountsOpenConstructionSites()
        {
            // Regression: the first version asked IsBuildingRole before
            // TryGetSite, which made the site branch unreachable — an
            // unfinished site carries UnitRole.Unit and only takes its
            // definition role on completion. sitesOpen was a permanent zero
            // and nothing said so.
            MatchSpec spec = ShortSpec();
            spec.TraceIntervalTicks = 20;

            int maxSitesSeen = 0;
            foreach (MetricSample sample in MatchRun.Execute(spec).Trace)
            {
                foreach (SlotMetrics slot in sample.Slots)
                {
                    if (slot.SitesOpen > maxSitesSeen) maxSitesSeen = slot.SitesOpen;
                }
            }

            Assert.That(maxSitesSeen, Is.GreaterThan(0),
                "the AI places buildings, so open sites must appear in the trace at some point");
        }

        [Test]
        public void TraceJson_ContainsNoFloatingPointNumber()
        {
            MatchSpec spec = ShortSpec();
            spec.TraceIntervalTicks = 50;

            MatchRunResult result = MatchRun.Execute(spec);

            foreach (MetricSample sample in result.Trace)
            {
                string line = sample.ToJsonLine();
                // The hard rule of section 3.2: no float leaves the simulation.
                // A '.' or an exponent in the numeric output would mean one did.
                Assert.That(line, Does.Not.Contain("."),
                    $"a decimal point appeared in the trace — no float may leave the simulation:\n{line}");
                Assert.That(line.ToLowerInvariant(), Does.Not.Contain("e+"));
                Assert.That(line, Does.Not.Contain("NaN"));
            }
        }

        // ================================================================
        // (c) ARTIFACTS
        // ================================================================

        [Test]
        public void Artifacts_AreWrittenAndReadable()
        {
            MatchSpec spec = ShortSpec();
            spec.TraceIntervalTicks = 100;
            spec.HashIntervalTicks = 200;

            MatchRunResult result = MatchRun.Execute(spec);
            string directory = Path.Combine(Path.GetTempPath(), "nova-ailab-tests", Guid.NewGuid().ToString("N"));

            try
            {
                RunArtifacts.Write(directory, spec, result);

                string resultPath = Path.Combine(directory, RunArtifacts.ResultFileName);
                string tracePath = Path.Combine(directory, RunArtifacts.TraceFileName);
                string chainPath = Path.Combine(directory, RunArtifacts.HashChainFileName);

                Assert.That(File.Exists(resultPath), Is.True);
                Assert.That(File.Exists(tracePath), Is.True);
                Assert.That(File.Exists(chainPath), Is.True);

                Assert.That(File.ReadAllLines(tracePath).Length, Is.EqualTo(result.Trace.Count),
                    "trace.ndjson holds exactly one line per metric tick");

                string resultJson = File.ReadAllText(resultPath);
                Assert.That(resultJson, Does.Contain("\"definitionsHash64\""),
                    "a result set must carry the definitions hash, so a report can refuse an incomparable comparison");
                Assert.That(resultJson, Does.Contain("DIAGNOSIS"),
                    "every result states what it is worth: diagnosis, never proof");

                // Both files must be parseable, not just present.
                using (var document = System.Text.Json.JsonDocument.Parse(resultJson)) { }
                using (var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(chainPath))) { }
                foreach (string line in File.ReadAllLines(tracePath))
                {
                    using var sample = System.Text.Json.JsonDocument.Parse(line);
                }
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void Artifacts_NameTheProfileThatActuallyPlayed()
        {
            // This line used to be the literal string "canonical" for every
            // slot of every run. In a comparison that is a lie about the ONE
            // artifact the report links into: the sample run kept for
            // `late-push` claimed both slots played the shipped profile. A
            // number with the wrong provenance still reads like a measurement,
            // which makes it worse than a missing one.
            MatchSpec spec = ShortSpec();
            spec.Slots[0].ProfileId = "late-push";
            spec.Slots[1].ProfileId = SlotSpec.CanonicalProfileId;

            string json = RunArtifacts.BuildResultJson(spec, MatchRun.Execute(spec));

            Assert.That(json, Does.Contain("\"profile\": \"late-push\""),
                "the run artifact must name the profile that played this slot");
            Assert.That(json, Does.Contain($"\"profile\": \"{SlotSpec.CanonicalProfileId}\""),
                "and the reference slot must be named too, not left as a generic label");
            Assert.That(json, Does.Not.Contain("\"profile\": \"canonical\""),
                "the hard-coded placeholder must be gone — it made every candidate's run look like the reference");
        }

        [Test]
        public void Artifacts_SayNoProfileForASlotThatNobodyDecidesFor()
        {
            // A scripted slot has a seat and no AI. Printing a profile id there
            // would claim a decision maker the arena deliberately does not have.
            MatchSpec spec = ShortSpec();
            spec.Slots[1].Controller = SlotController.Passive;

            string json = RunArtifacts.BuildResultJson(spec, MatchRun.Execute(spec));

            Assert.That(json, Does.Contain("\"profile\": \"none\""),
                "a slot without an AI has no profile, and saying so is not the same as saying 'canonical'");
        }

        // ================================================================
        // (d) SPEC FILE
        // ================================================================

        [Test]
        public void SpecFile_ReadsTheDocumentedShape()
        {
            const string json = @"{ ""specVersion"": 1, ""mode"": ""match"", ""seed"": ""0xA17E57DE57"",
                ""tickBudget"": 1234, ""mapWidth"": 128, ""mapHeight"": 128, ""entityCapacity"": 1024,
                ""slots"": [
                  { ""slot"": 0, ""faction"": ""legion"", ""controller"": ""ai"", ""profile"": ""canonical"" },
                  { ""slot"": 1, ""faction"": ""alliance"", ""controller"": ""passive"" }],
                ""traceIntervalTicks"": 10, ""hashIntervalTicks"": 100 }";

            MatchSpec spec = SpecFile.Parse(json);

            Assert.That(spec.Seed, Is.EqualTo(Seed));
            Assert.That(spec.TickBudget, Is.EqualTo(1234));
            Assert.That(spec.TraceIntervalTicks, Is.EqualTo(10));
            Assert.That(spec.Slots[0].Faction, Is.EqualTo(Nova.Simulation.State.FactionId.Legion));
            Assert.That(spec.Slots[0].IsAi, Is.True);
            Assert.That(spec.Slots[1].IsAi, Is.False);
        }

        [Test]
        public void SpecFile_RefusesAMisspelledKey()
        {
            // The whole reason the reader is hand-rolled: a typo must not fall
            // back to a default and produce numbers nobody can reproduce.
            Assert.Throws<FormatException>(() => SpecFile.Parse(@"{ ""tickbudget"": 500 }"));
        }

        [Test]
        public void SpecFile_RefusesAForeignSpecVersion()
        {
            Assert.Throws<FormatException>(() => SpecFile.Parse(@"{ ""specVersion"": 99 }"));
        }

        [Test]
        public void SpecFile_RefusesAModeThatDoesNotExistYet()
        {
            Assert.Throws<FormatException>(() => SpecFile.Parse(@"{ ""mode"": ""duel"" }"));
        }

        // ================================================================
        // (e) THE PARALLEL SWEEP
        // ================================================================

        [Test]
        public void Sweep_ProducesTheSameResultsAsRunningSerially()
        {
            MatchSpec template = ShortSpec();
            template.HashIntervalTicks = 500;
            ulong[] seeds = SeedSeries.Derive(Seed, 4);

            SweepResult parallel = SweepRunner.Run(template, seeds, outputDirectory: null, maxParallelism: 4);

            for (int i = 0; i < seeds.Length; i++)
            {
                MatchSpec single = ShortSpec(seeds[i]);
                single.HashIntervalTicks = 500;
                MatchRunResult serial = MatchRun.Execute(single);

                Assert.That(SweepRunner.Compare(parallel.Runs[i], serial), Is.Null,
                    $"seed 0x{seeds[i]:X} came out differently under parallel load than on its own — " +
                    "that is shared state between matches, and it would surface in a report as unexplained spread");
            }
        }

        [Test]
        public void Sweep_KeepsResultsInSeedOrderNotCompletionOrder()
        {
            MatchSpec template = ShortSpec();
            ulong[] seeds = SeedSeries.Derive(Seed, 6);

            SweepResult first = SweepRunner.Run(template, seeds, null, maxParallelism: 6);
            SweepResult second = SweepRunner.Run(template, seeds, null, maxParallelism: 6);

            for (int i = 0; i < seeds.Length; i++)
            {
                Assert.That(first.Runs[i].Seed, Is.EqualTo(seeds[i]),
                    "a sweep whose output order depends on scheduling would be unreproducible");
                Assert.That(second.Runs[i].FinalStateHash, Is.EqualTo(first.Runs[i].FinalStateHash));
            }
        }

        [Test]
        public void Sweep_DoubleChecksEveryTwentiethRunAndFindsNoSharedState()
        {
            MatchSpec template = ShortSpec();
            template.HashIntervalTicks = 250;
            ulong[] seeds = SeedSeries.Derive(Seed, 21);

            SweepResult sweep = SweepRunner.Run(template, seeds, null, maxParallelism: 0);

            Assert.That(sweep.DoubleCheckedRuns, Is.EqualTo(2), "runs 0 and 20 of 21 are played twice");
            Assert.That(sweep.Mismatches, Is.Empty,
                "a mismatch here means matches share state under full core load — the sweep would be worthless");
        }

        [Test]
        public void DerivedSeeds_ArePureFunctionsOfTheBaseSeed()
        {
            ulong[] first = SeedSeries.Derive(Seed, 8);
            ulong[] second = SeedSeries.Derive(Seed, 8);

            Assert.That(second, Is.EqualTo(first), "a seed list belongs to a result set and must be reproducible");
            Assert.That(new HashSet<ulong>(first).Count, Is.EqualTo(first.Length), "derived seeds must be distinct");
            Assert.That(SeedSeries.Derive(Seed ^ 1UL, 8), Is.Not.EqualTo(first));
        }

        // ================================================================
        // (f) THE FINDING: the seed axis is empty
        // ================================================================

        [Test]
        public void DifferentSeeds_ProduceTheIdenticalMatch_BecauseNothingDrawsFromThePrng()
        {
            // Documented as a test because it is the single most important
            // methodological constraint this lab found: NO simulation system
            // draws from the kernel PRNG. The seed feeds the state hash and the
            // snapshot, nothing else. A sweep over N seeds therefore plays the
            // same match N times, and any plan that treats seeds as a variance
            // axis (section 3.7's reference seed set, E4's seed x profile
            // matrix) is measuring one observation, not N.
            //
            // If this test ever fails, that is GOOD NEWS: something now draws
            // from the PRNG, seeds became a real axis, and the plan's sweep
            // design started working. Delete it then, and say so in the plan.
            MatchSpec a = ShortSpec(0x1UL);
            a.TraceIntervalTicks = 500;
            MatchSpec b = ShortSpec(0xDEADBEEFUL);
            b.TraceIntervalTicks = 500;

            MatchRunResult first = MatchRun.Execute(a);
            MatchRunResult second = MatchRun.Execute(b);

            Assert.That(second.DecidedTick, Is.EqualTo(first.DecidedTick));
            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Trace.Count, Is.EqualTo(first.Trace.Count));

            for (int i = 0; i < first.Trace.Count; i++)
            {
                for (int slot = 0; slot < first.Trace[i].Slots.Length; slot++)
                {
                    Assert.That(second.Trace[i].Slots[slot].ToJsonOfOneSlot(),
                        Is.EqualTo(first.Trace[i].Slots[slot].ToJsonOfOneSlot()),
                        $"seeds diverged at tick {first.Trace[i].Tick} — see the comment above, this is good news");
                }
            }

            Assert.That(second.FinalStateHash, Is.Not.EqualTo(first.FinalStateHash),
                "the state hash still covers the PRNG words, so it differs even though the match does not");
        }
    }
}
