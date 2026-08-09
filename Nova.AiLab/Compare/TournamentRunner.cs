using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nova.AI;
using Nova.AI.Data;
using Nova.Simulation.State;
using Nova.Simulation.Victory;

namespace Nova.AiLab
{
    /// <summary>
    /// Plays every candidate against the frozen reference AI and collects the
    /// numbers a human needs to choose (plan section 3.7).
    /// <para>
    /// EVERY CANDIDATE PLAYS BOTH FACTIONS. Slot 0 is Alliance and slot 1 is
    /// Legion, and the two are deliberately asymmetric — artillery 20/18 tiles
    /// and 110/60 damage, harvester cargo 330/300 AE. A candidate measured only
    /// as Legion has been measured against one half of the game. It also
    /// cancels the spawn-order advantage, which the duel table showed decides
    /// close pairings on its own.
    /// </para>
    /// <para>
    /// The seed list is carried and honoured, but today it adds nothing: no
    /// simulation system draws from the kernel PRNG, so every seed plays the
    /// identical match. The runner says so in the result rather than letting a
    /// wide sweep look like wide evidence.
    /// </para>
    /// </summary>
    public static class TournamentRunner
    {
        public static ResultSet Run(
            IReadOnlyList<AiProfile> candidates,
            ulong[] seeds,
            int tickBudget,
            string outputDirectory,
            string commit,
            int maxParallelism)
        {
            if (candidates == null || candidates.Count == 0) throw new ArgumentException("no candidates");
            if (seeds == null || seeds.Length == 0) throw new ArgumentException("no seeds");

            ResultSet set = ResultSet.Create(seeds, tickBudget, slotCount: 2, commit);

            var results = new CandidateResult[candidates.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount,
            };

            Parallel.For(0, candidates.Count, options, i =>
            {
                results[i] = RunCandidate(candidates[i], seeds, tickBudget, outputDirectory);
            });

            // Fixed order, never completion order.
            for (int i = 0; i < results.Length; i++) set.Candidates.Add(results[i]);
            return set;
        }

        private static CandidateResult RunCandidate(
            AiProfile candidate, ulong[] seeds, int tickBudget, string outputDirectory)
        {
            var result = new CandidateResult
            {
                ProfileId = candidate.ProfileId,
                DifferencesFromReference = LabProfiles.DifferencesFromReference(candidate),
            };

            bool keptSample = false;

            foreach (ulong seed in seeds)
            {
                // Both seatings: candidate as Alliance (slot 0), then as Legion.
                for (int candidateSlot = 0; candidateSlot < 2; candidateSlot++)
                {
                    var spec = new MatchSpec
                    {
                        Seed = seed,
                        TickBudget = tickBudget,
                        TraceIntervalTicks = 100,
                        Slots = MatchSpec.DefaultSlots(2),
                    };

                    for (int slot = 0; slot < 2; slot++)
                    {
                        AiProfile profile = slot == candidateSlot ? candidate : LabProfiles.Reference;
                        FactionId faction = spec.Slots[slot].Faction;
                        spec.Slots[slot].Profile = new AiFactionProfile(faction.ToString(), profile);
                        // The id travels with the numbers: without it the run
                        // artifact cannot say which profile played, and the
                        // sample run the report links into misnames it.
                        spec.Slots[slot].ProfileId = profile.ProfileId;
                    }

                    MatchRunResult run = MatchRun.Execute(spec);
                    Accumulate(result, run, (byte)candidateSlot);

                    // One run per candidate is kept whole, so the report can
                    // link into the view window instead of only asserting.
                    if (keptSample || outputDirectory == null) continue;
                    string directory = Path.Combine(outputDirectory, "runs", candidate.ProfileId);
                    var sampleSpec = CloneForSample(spec);
                    MatchRunResult sample = MatchRun.Execute(sampleSpec);
                    RunArtifacts.Write(directory, sampleSpec, sample);
                    result.SampleRunDirectory = Path.Combine("runs", candidate.ProfileId).Replace('\\', '/');
                    keptSample = true;
                }
            }

            return result;
        }

        /// <summary>The kept run additionally records view frames; the numbers come from the plain run.</summary>
        private static MatchSpec CloneForSample(MatchSpec spec)
        {
            var slots = new SlotSpec[spec.Slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new SlotSpec
                {
                    Slot = spec.Slots[i].Slot,
                    Faction = spec.Slots[i].Faction,
                    Controller = spec.Slots[i].Controller,
                    Profile = spec.Slots[i].Profile,
                    ProfileId = spec.Slots[i].ProfileId,
                };
            }

            return new MatchSpec
            {
                Seed = spec.Seed,
                TickBudget = spec.TickBudget,
                MapWidth = spec.MapWidth,
                MapHeight = spec.MapHeight,
                EntityCapacity = spec.EntityCapacity,
                StartingCreditsAE = spec.StartingCreditsAE,
                TraceIntervalTicks = spec.TraceIntervalTicks,
                ViewIntervalTicks = 50,
                HashIntervalTicks = 500,
                Slots = slots,
            };
        }

        private static void Accumulate(CandidateResult result, MatchRunResult run, byte candidateSlot)
        {
            result.Matches++;

            if (run.Outcome == MatchOutcome.VictoryElimination)
            {
                if (run.WinnerSlot == candidateSlot) result.Wins++; else result.Losses++;
            }
            else
            {
                result.Draws++;
            }

            if (run.IsDecided)
            {
                result.DecidedTickSum += run.DecidedTick;
                result.DecidedMatches++;
            }

            // Replay value: how many DIFFERENT matches this candidate played
            // across the whole set. One entry means the seeds and the seatings
            // all ended the same way — the emptiness of the seed axis, in a
            // column instead of a footnote.
            result.RecordEnding($"{run.Outcome}|{run.DecidedTick}|0x{run.FinalStateHash:X16}");

            if (run.Trace.Count == 0) return;
            SlotMetrics last = run.Trace[run.Trace.Count - 1].Slots[candidateSlot];
            result.CreditsAtEndSum += last.Credits;
            result.ArmySizeAtEndSum += last.ArmySize;
            result.UnitsLostSum += last.UnitsLost;
            result.IntentsSubmittedSum += last.IntentsSubmitted;
            result.IntentsRejectedSum += last.IntentsRejected;

            for (int i = 0; i < run.Feel.Count; i++)
            {
                FeelMetrics feel = run.Feel[i];
                if (feel.Slot != candidateSlot) continue;

                // The two -1 fields are "not measurable in this match", not
                // zero, so they are averaged over their own sample count.
                if (feel.ExchangeRatioPercent >= 0)
                {
                    result.ExchangeRatioSum += feel.ExchangeRatioPercent;
                    result.ExchangeRatioSamples++;
                }
                if (feel.MeanReactionLatencyTicks >= 0)
                {
                    result.ReactionLatencySum += feel.MeanReactionLatencyTicks;
                    result.ReactionLatencySamples++;
                }
                result.CombatIntervalsSum += feel.CombatIntervals;
                result.LargestLossJumpSum += feel.LargestLossJump;
                result.UnansweredDamageSum += feel.UnansweredDamageEvents;
                result.ActionsPerMinuteSum += feel.ActionsPerMinute;
            }
        }
    }
}
