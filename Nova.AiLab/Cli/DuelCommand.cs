using System;
using System.Collections.Generic;
using System.IO;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>duel</c> — the counter-table: every role pairing at three distances,
    /// both directions, plus the siege echelon, at AE parity.
    /// </summary>
    internal static class DuelCommand
    {
        public static int Run(Options options)
        {
            List<DuelSpec> plan = DuelTable.Plan(options.UnitsPerSide, options.Spec.TickBudget);
            Console.WriteLine($"duel: {plan.Count} duels, {options.UnitsPerSide} units on the expensive side, " +
                              $"budget {options.Spec.TickBudget} ticks each");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            DuelResult[] results = DuelTable.Run(plan, options.Parallelism);
            watch.Stop();

            int decided = 0, wobbling = 0, undecided = 0, noContact = 0;
            foreach (DuelResult r in results)
            {
                if (r.Decided) decided++; else undecided++;
                if (r.ParityWobbles) wobbling++;
                if (r.NoContact) noContact++;
            }

            Console.WriteLine($"{results.Length} duels in {watch.ElapsedMilliseconds} ms — " +
                              $"{decided} decided, {undecided} ran out the tick budget");
            if (noContact > 0)
            {
                // Its own outcome, not a stalemate: at the weapon-range echelon
                // this is the documented finding that a gun out-ranging its own
                // sight cannot use that range without scouting.
                Console.WriteLine($"no contact at all in {noContact} duels — nobody took a scratch " +
                                  "(at weapon range this is the sight-vs-range finding, not a stalemate)");
            }
            if (wobbling > 0)
            {
                // Not a footnote: where a side cannot spend its budget evenly,
                // the parity the whole comparison rests on is off.
                Console.WriteLine($"WARNING: {wobbling} pairings left over 10% of a side's budget unspent — " +
                                  "their parity wobbles and their outcome is weak evidence");
            }

            List<string> disagreements = DuelTable.DirectionDisagreements(results);
            Console.WriteLine($"direction disagreements: {disagreements.Count} " +
                              "(pairings so close that spawn order decides them)");
            for (int i = 0; i < Math.Min(10, disagreements.Count); i++)
            {
                Console.WriteLine($"  {disagreements[i]}");
            }
            if (disagreements.Count > 10) Console.WriteLine($"  … and {disagreements.Count - 10} more");

            if (options.OutputDirectory != null)
            {
                Directory.CreateDirectory(options.OutputDirectory);
                string path = Path.Combine(options.OutputDirectory, "duels.ndjson");
                File.WriteAllText(path, DuelTable.ToNdjson(results));
                Console.WriteLine($"table written to {path}");
            }

            return 0;
        }
    }
}
