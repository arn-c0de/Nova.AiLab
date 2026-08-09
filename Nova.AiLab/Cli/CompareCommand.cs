using System;
using System.IO;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>compare</c> — every candidate profile against the frozen reference,
    /// in both faction seatings. Metrics side by side, never a ranking
    /// (decision 11): a single number reliably rewards the wrong thing.
    /// </summary>
    internal static class CompareCommand
    {
        public static int Run(Options options)
        {
            ulong[] seeds = SeedSeries.Derive(options.Spec.Seed, options.SeedCount);
            string commit = CurrentCommit();

            Console.WriteLine($"compare: {LabProfiles.Candidates.Count} candidates x {seeds.Length} seeds " +
                              $"x 2 faction seatings, budget {options.Spec.TickBudget} ticks, commit {commit}");
            if (seeds.Length > 1)
            {
                Console.WriteLine("NOTE: the seed axis is empty — no simulation system draws from the kernel " +
                                  "PRNG, so extra seeds cost time and add no observations.");
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            ResultSet set = TournamentRunner.Run(
                LabProfiles.Candidates, seeds, options.Spec.TickBudget,
                options.OutputDirectory, commit, options.Parallelism);
            watch.Stop();

            string referenceId = LabProfiles.Reference.ProfileId;
            foreach (CandidateResult c in set.Candidates)
            {
                Console.WriteLine(
                    $"  {c.ProfileId,-16} win {c.WinPercent,3}%  {c.Wins}/{c.Losses}/{c.Draws}  " +
                    $"decided {c.AverageDecidedTick,6}  credits {c.AverageCredits,7}  " +
                    $"army {c.AverageArmySize,3}  lost {c.AverageUnitsLost,4}  rejected {c.IntentsRejectedSum}");
            }
            Console.WriteLine($"{set.Candidates.Count} candidates in {watch.ElapsedMilliseconds} ms");

            // An archived set is only comparable when its provenance matches.
            // The refusal is the product here, not an error path — but so is
            // the comparison when it IS allowed: a cleared archive goes into
            // the report and shows its numbers beside the new ones. Loading it,
            // clearing it and then building the report without it produced a
            // yes/no verdict where the caller asked for a comparison.
            string refusal = null;
            ResultSet archived = null;
            if (options.AgainstFile != null)
            {
                archived = ResultSetFile.Load(options.AgainstFile);
                refusal = set.WhyNotComparableWith(archived);
                Console.WriteLine(refusal == null
                    ? $"archived set {options.AgainstFile} is comparable — its numbers are in the report"
                    : $"COMPARISON REFUSED against {options.AgainstFile}: {refusal}");
                if (refusal != null) archived = null;
            }

            if (options.OutputDirectory != null)
            {
                Directory.CreateDirectory(options.OutputDirectory);
                File.WriteAllText(Path.Combine(options.OutputDirectory, ResultSetFile.FileName), set.ToJson());
                File.WriteAllText(
                    Path.Combine(options.OutputDirectory, ComparisonReport.FileName),
                    refusal == null
                        ? ComparisonReport.Build(set, referenceId, archived)
                        : ComparisonReport.BuildRefusal(refusal, set));

                foreach (CandidateResult c in set.Candidates)
                {
                    if (string.Equals(c.ProfileId, referenceId, StringComparison.Ordinal)) continue;
                    File.WriteAllText(
                        Path.Combine(options.OutputDirectory, PrDraft.FileNameFor(c.ProfileId)),
                        PrDraft.Build(set, referenceId, c.ProfileId));
                }

                Console.WriteLine($"report written to {Path.Combine(options.OutputDirectory, ComparisonReport.FileName)}");
                Console.WriteLine("PR drafts written — the played-observation section in each is deliberately empty.");
            }

            return refusal == null ? 0 : 2;
        }

        /// <summary>
        /// The commit a result set was measured at; a set retires with it
        /// (plan section 3.7).
        /// <para>
        /// Asked of the MEASURED CHECKOUT, not of the working directory. The
        /// lab lives outside the game repository, so the working directory is
        /// the lab's own — it would answer with the lab's commit, or with
        /// nothing, and the refusal to compare across a merge window would
        /// quietly stop refusing. <see cref="NovaRepo.Path"/> is baked into
        /// the binary at build time and names the checkout whose sources were
        /// compiled in, which is the only honest answer to "where did these
        /// numbers come from".
        /// </para>
        /// </summary>
        private static string CurrentCommit()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                {
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                };
                if (!string.IsNullOrEmpty(NovaRepo.Path)) startInfo.WorkingDirectory = NovaRepo.Path;

                var process = new System.Diagnostics.Process { StartInfo = startInfo };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrEmpty(output) ? "unknown" : output;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
