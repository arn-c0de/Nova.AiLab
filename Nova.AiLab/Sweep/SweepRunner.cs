using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Nova.AiLab
{
    /// <summary>Outcome of one sweep, in the seed order of the plan — never in completion order.</summary>
    public sealed class SweepResult
    {
        public MatchRunResult[] Runs;
        public long WallClockMilliseconds;
        public long TotalTicks;
        public int DoubleCheckedRuns;
        /// <summary>Double-checked runs whose second pass disagreed. Anything above 0 invalidates the sweep.</summary>
        public List<string> Mismatches = new List<string>();

        /// <summary>
        /// Distinct (outcome, winner, deciding tick) triples across the sweep.
        /// <para>
        /// Reported because of a finding this lab made on its first sweep: NO
        /// simulation system draws from the kernel PRNG today — the seed feeds
        /// the state hash and the snapshot, and nothing else. So a seed changes
        /// the hash but not the match, and a sweep over N seeds plays the same
        /// game N times. A value of 1 here means the seed axis measured
        /// nothing, and the sweep must say so rather than present N rows as N
        /// observations.
        /// </para>
        /// </summary>
        public int DistinctDecisions;

        /// <summary>Simulated ticks per wall-clock second across all cores.</summary>
        public long TicksPerSecond => WallClockMilliseconds > 0
            ? TotalTicks * 1000L / WallClockMilliseconds
            : 0;
    }

    /// <summary>
    /// Runs a matrix of seeds in parallel (plan E2). One match per core, no
    /// locks — the isolation argument of section 3.1 makes that safe: every
    /// match builds its own kernel, entity manager and systems, and the only
    /// shared data is immutable (<c>SimDefinitions</c>, <c>WeaponProfiles</c>,
    /// <c>DamageMatrix</c> are static readonly).
    /// <para>
    /// That argument is checked, not trusted. <b>Every twentieth run is played
    /// twice and its hash chain compared</b> (section 3.7). It costs 5% of the
    /// compute and catches precisely the bug a single determinism test never
    /// sees: shared state between matches running in parallel, which only
    /// appears under full core load and disguises itself as "unexplained
    /// spread" in the results.
    /// </para>
    /// <para>
    /// Results are stored at fixed indices, so the report reads in seed order
    /// regardless of which core finished when. A sweep whose output depends on
    /// scheduling would be unreproducible in exactly the way the whole lab
    /// exists to avoid.
    /// </para>
    /// </summary>
    public static class SweepRunner
    {
        /// <summary>Every n-th run is played twice and compared (section 3.7).</summary>
        public const int DoubleCheckEveryNthRun = 20;

        public static SweepResult Run(MatchSpec template, ulong[] seeds, string outputDirectory, int maxParallelism)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (seeds == null || seeds.Length == 0) throw new ArgumentException("no seeds given", nameof(seeds));

            var result = new SweepResult { Runs = new MatchRunResult[seeds.Length] };
            var mismatches = new List<string>[seeds.Length];
            var watch = Stopwatch.StartNew();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount,
            };

            Parallel.For(0, seeds.Length, options, index =>
            {
                MatchSpec spec = template.WithSeed(seeds[index]);
                MatchRunResult run = MatchRun.Execute(spec);
                result.Runs[index] = run;

                if (outputDirectory != null)
                {
                    string runDirectory = Path.Combine(outputDirectory, $"seed-0x{seeds[index]:X}");
                    RunArtifacts.Write(runDirectory, spec, run);
                }

                if (index % DoubleCheckEveryNthRun != 0) return;

                MatchRunResult second = MatchRun.Execute(template.WithSeed(seeds[index]));
                string difference = Compare(run, second);
                if (difference != null)
                {
                    mismatches[index] = new List<string> { $"seed 0x{seeds[index]:X}: {difference}" };
                }
            });

            watch.Stop();
            result.WallClockMilliseconds = watch.ElapsedMilliseconds;

            var decisions = new HashSet<string>();
            for (int i = 0; i < seeds.Length; i++)
            {
                MatchRunResult run = result.Runs[i];
                result.TotalTicks += run.FinalTick;
                decisions.Add($"{run.Outcome}/{run.WinnerSlot}/{run.DecidedTick}");
                if (i % DoubleCheckEveryNthRun == 0) result.DoubleCheckedRuns++;
                if (mismatches[i] != null) result.Mismatches.AddRange(mismatches[i]);
            }
            result.DistinctDecisions = decisions.Count;

            return result;
        }

        /// <summary>
        /// Returns null when both runs agree, otherwise the first difference.
        /// <para>
        /// NOTE ON REACH: with no hash chain recorded this can only see the end
        /// state and the decision. That still catches a divergence, but it
        /// cannot say at which tick — which is why <c>SweepCommand</c> gives a
        /// sweep a default chain interval rather than leaving the self-check
        /// half blind.
        /// </para>
        /// </summary>
        public static string Compare(MatchRunResult a, MatchRunResult b)
        {
            if (a.FinalStateHash != b.FinalStateHash)
            {
                return $"end state 0x{a.FinalStateHash:X16} vs 0x{b.FinalStateHash:X16}";
            }
            if (a.DecidedTick != b.DecidedTick || a.Outcome != b.Outcome || a.WinnerSlot != b.WinnerSlot)
            {
                return $"decision {a.Outcome}@{a.DecidedTick}/slot {a.WinnerSlot} vs " +
                       $"{b.Outcome}@{b.DecidedTick}/slot {b.WinnerSlot}";
            }
            if (a.HashChain.Count != b.HashChain.Count)
            {
                return $"chain length {a.HashChain.Count} vs {b.HashChain.Count}";
            }
            for (int i = 0; i < a.HashChain.Count; i++)
            {
                if (a.HashChain[i].StateHash == b.HashChain[i].StateHash) continue;
                return $"chain diverges at tick {a.HashChain[i].Tick}: " +
                       $"0x{a.HashChain[i].StateHash:X16} vs 0x{b.HashChain[i].StateHash:X16}";
            }
            return null;
        }
    }
}
