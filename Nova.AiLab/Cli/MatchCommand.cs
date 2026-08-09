using System;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>match</c> — one AI-vs-AI match, optionally repeated. With --repeat the
    /// exit code is the finding, not the text: 2 means two runs of the same
    /// spec drifted apart, and every number from that run is worthless.
    /// </summary>
    internal static class MatchCommand
    {
        public static int Run(Options options)
        {
            TerminalView live = null;
            if (options.Watch)
            {
                live = new TerminalView(options.Spec.MapWidth, options.Spec.MapHeight);
                Console.WriteLine(TerminalView.Legend);
            }

            MatchRunResult first = null;
            for (int run = 0; run < options.Repeat; run++)
            {
                // Only the first run is watched: two overlapping live views in
                // one terminal would be unreadable, and the point of --repeat
                // is the hash comparison, not the picture.
                Action<ViewFrame> onFrame = (live != null && run == 0) ? live.Draw : (Action<ViewFrame>)null;
                MatchRunResult result = MatchRun.Execute(options.Spec, onFrame);
                Report(result, run);

                if (options.OutputDirectory != null && run == 0)
                {
                    RunArtifacts.Write(options.OutputDirectory, options.Spec, result);
                    Console.WriteLine($"[run {run}] artifacts written to {options.OutputDirectory}");
                }

                if (first == null)
                {
                    first = result;
                    continue;
                }

                string difference = SweepRunner.Compare(first, result);
                if (difference != null)
                {
                    Console.Error.WriteLine($"NON-DETERMINISTIC: run {run} differs from run 0 — {difference}");
                    return 2;
                }
            }

            if (options.Repeat > 1)
            {
                Console.WriteLine($"determinism: {options.Repeat} runs of seed 0x{options.Spec.Seed:X} agree on every hash");
            }
            return 0;
        }

        private static void Report(MatchRunResult r, int run)
        {
            Console.WriteLine(
                $"[run {run}] seed 0x{r.Seed:X}  slots {r.SlotCount} ({r.AiSlotCount} AI)  " +
                $"outcome {r.Outcome}  winner slot {r.WinnerSlot}  " +
                $"decided tick {r.DecidedTick}  final tick {r.FinalTick}  " +
                $"state hash 0x{r.FinalStateHash:X16}  {r.ElapsedMilliseconds} ms");

            if (!r.IsDecided)
            {
                Console.WriteLine($"[run {run}] undecided within the budget of {r.TickBudget} ticks");
            }
            if (r.Trace.Count > 0 || r.HashChain.Count > 0 || r.View.Count > 0)
            {
                Console.WriteLine($"[run {run}] {r.Trace.Count} metric samples, " +
                                  $"{r.HashChain.Count} hash chain entries, {r.View.Count} view frames");
            }
        }
    }
}
