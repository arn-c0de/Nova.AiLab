using System;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>sweep</c> — the same spec across a seed series, in parallel, with a
    /// determinism spot check on every n-th run. A mismatch is not a warning:
    /// it invalidates the whole table, including the rows that look fine.
    /// </summary>
    internal static class SweepCommand
    {
        /// <summary>
        /// Chain interval a sweep falls back to when the caller named none.
        /// <para>
        /// The self-check advertises "diverges at tick N", and without a chain
        /// it cannot say that: <c>SweepRunner.Compare</c> then sees two empty
        /// chains and can only compare the end state and the decision. That
        /// still catches a divergence, but it throws away the one diagnostic
        /// the sweep exists to produce — and shared state between parallel
        /// matches is exactly the bug where knowing the tick is the difference
        /// between a finding and a shrug.
        /// </para>
        /// </summary>
        public const int DefaultHashIntervalTicks = 500;

        public static int Run(Options options)
        {
            ulong[] seeds = SeedSeries.Derive(options.Spec.Seed, options.SeedCount);

            bool chainDefaulted = options.Spec.HashIntervalTicks <= 0;
            if (chainDefaulted) options.Spec.HashIntervalTicks = DefaultHashIntervalTicks;

            Console.WriteLine(
                $"sweep: {seeds.Length} seeds, {options.Spec.Slots.Length} slots, budget {options.Spec.TickBudget}, " +
                $"parallelism {(options.Parallelism > 0 ? options.Parallelism : Environment.ProcessorCount)}, " +
                $"hash chain every {options.Spec.HashIntervalTicks} ticks" +
                (chainDefaulted ? " (default — the self-check needs a chain to name a divergence tick)" : ""));

            SweepResult sweep = SweepRunner.Run(
                options.Spec, seeds, options.OutputDirectory, options.Parallelism);

            for (int i = 0; i < sweep.Runs.Length; i++)
            {
                MatchRunResult r = sweep.Runs[i];
                Console.WriteLine(
                    $"  seed 0x{r.Seed:X}  {r.Outcome}  winner slot {r.WinnerSlot}  " +
                    $"tick {r.FinalTick}  state hash 0x{r.FinalStateHash:X16}");
            }

            Console.WriteLine(
                $"throughput: {sweep.TotalTicks} ticks in {sweep.WallClockMilliseconds} ms " +
                $"= {sweep.TicksPerSecond} ticks/s across all cores");
            Console.WriteLine(
                $"self-check: {sweep.DoubleCheckedRuns} of {seeds.Length} runs played twice " +
                $"(every {SweepRunner.DoubleCheckEveryNthRun}th), {sweep.Mismatches.Count} mismatches");

            if (sweep.DistinctDecisions == 1 && seeds.Length > 1)
            {
                // Said plainly, because the table above looks like evidence and
                // is not: no simulation system draws from the kernel PRNG, so
                // the seed moves the state hash and nothing else.
                Console.WriteLine(
                    $"NOTE: all {seeds.Length} seeds produced the SAME decision — the seed axis is empty. " +
                    "No simulation system draws from the kernel PRNG today, so a seed changes the state " +
                    "hash but not the match. Variance has to come from profiles (E6) or starting " +
                    "positions, not from seeds.");
            }
            else
            {
                Console.WriteLine($"variance: {sweep.DistinctDecisions} distinct decisions across {seeds.Length} seeds");
            }

            if (sweep.Mismatches.Count > 0)
            {
                // Not a warning. A sweep with a mismatch measured nothing:
                // shared state between parallel matches disguises itself as
                // spread in the numbers, and every result here is suspect.
                Console.Error.WriteLine("SWEEP INVALID — parallel runs disagreed with themselves:");
                foreach (string mismatch in sweep.Mismatches) Console.Error.WriteLine($"  {mismatch}");
                return 2;
            }

            if (options.OutputDirectory != null)
            {
                Console.WriteLine($"artifacts written to {options.OutputDirectory}");
            }
            return 0;
        }
    }
}
