using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nova.AI.Data;
using Nova.Simulation.Definitions;

namespace Nova.AiLab
{
    /// <summary>What one candidate scored over the reference seed set. Integers only.</summary>
    public sealed class CandidateResult
    {
        public string ProfileId;
        public List<string> DifferencesFromReference = new List<string>();

        public int Matches;
        public int Wins;
        public int Losses;
        public int Draws;

        /// <summary>Deciding tick summed over decided matches, and the count that produced it.</summary>
        public long DecidedTickSum;
        public int DecidedMatches;

        public long CreditsAtEndSum;
        public long ArmySizeAtEndSum;
        public long UnitsLostSum;
        public long IntentsSubmittedSum;
        public long IntentsRejectedSum;

        // ---- game feel (NEXT-STEPS.md section 7) ------------------------
        //
        // Four columns that describe HOW a match went, next to the ones that
        // say who won. They are summed here and averaged below; they are
        // never combined with each other, and nothing sorts by them
        // (decision 11).

        /// <summary>Exchange ratio summed over the matches that HAD losses — the others carry no ratio.</summary>
        public long ExchangeRatioSum;
        public int ExchangeRatioSamples;

        public long CombatIntervalsSum;
        public long LargestLossJumpSum;

        /// <summary>Reaction latency summed over the matches in which the slot answered at least once.</summary>
        public long ReactionLatencySum;
        public int ReactionLatencySamples;

        public long UnansweredDamageSum;
        public long ActionsPerMinuteSum;

        /// <summary>
        /// Distinct match endings this candidate produced, as
        /// <c>outcome|decidedTick|endStateHash</c>. The replay-value column
        /// (NEXT-STEPS section 7) is simply how many entries this list has:
        /// one means every seed and both seatings played the same match to the
        /// same end, which is the state of the world today.
        /// </summary>
        public List<string> DistinctEndings = new List<string>();

        /// <summary>Directory of one run kept for inspection — the link into the view window.</summary>
        public string SampleRunDirectory;

        public int WinPercent => Matches > 0 ? Wins * 100 / Matches : 0;
        public long AverageDecidedTick => DecidedMatches > 0 ? DecidedTickSum / DecidedMatches : 0;
        public long AverageCredits => Matches > 0 ? CreditsAtEndSum / Matches : 0;
        public long AverageArmySize => Matches > 0 ? ArmySizeAtEndSum / Matches : 0;
        public long AverageUnitsLost => Matches > 0 ? UnitsLostSum / Matches : 0;

        /// <summary>Enemy entities lost per 100 own; -1 when no match in the set produced a ratio.</summary>
        public long AverageExchangeRatio => ExchangeRatioSamples > 0 ? ExchangeRatioSum / ExchangeRatioSamples : -1;

        public long AverageCombatIntervals => Matches > 0 ? CombatIntervalsSum / Matches : 0;
        public long AverageLargestLossJump => Matches > 0 ? LargestLossJumpSum / Matches : 0;

        /// <summary>Mean ticks from damage to a new movement order; -1 when the candidate never answered.</summary>
        public long AverageReactionLatency => ReactionLatencySamples > 0 ? ReactionLatencySum / ReactionLatencySamples : -1;

        public long AverageUnansweredDamage => Matches > 0 ? UnansweredDamageSum / Matches : 0;
        public long AverageActionsPerMinute => Matches > 0 ? ActionsPerMinuteSum / Matches : 0;

        /// <summary>How many different endings the candidate produced over the whole set.</summary>
        public int ReplayValue => DistinctEndings.Count;

        /// <summary>Records one ending, keeping the list distinct and in first-seen order.</summary>
        public void RecordEnding(string ending)
        {
            if (DistinctEndings.Contains(ending)) return;
            DistinctEndings.Add(ending);
        }
    }

    /// <summary>
    /// A set of results, and the proof of where it came from.
    /// <para>
    /// THE PROVENANCE IS THE POINT (plan section 3.7). Comparing a candidate
    /// against numbers measured on a different spec, a different seed list or a
    /// different definition table is not a weak comparison — it is a wrong one,
    /// and it looks exactly like a right one. So every set carries its
    /// spec version, its seed list, the definitions hash and the commit it was
    /// measured at, and <see cref="Explain"/> refuses rather than mixes.
    /// </para>
    /// <para>
    /// A profile archive survives a merge window, a code archive does not: an
    /// old profile file can be re-measured on the new build, an old code state
    /// is gone and its result set retires with it. That is why the commit is
    /// recorded and why the report will not compare across it.
    /// </para>
    /// </summary>
    public sealed class ResultSet
    {
        public int SpecVersion = MatchSpec.SpecVersion;
        public int ProfileSchemaVersion = AiProfile.SchemaVersion;
        public ulong DefinitionsHash64;
        public string Commit = "unknown";
        public int TickBudget;
        public int SlotCount;
        public ulong[] Seeds = Array.Empty<ulong>();

        public List<CandidateResult> Candidates = new List<CandidateResult>();

        public static ResultSet Create(ulong[] seeds, int tickBudget, int slotCount, string commit) => new ResultSet
        {
            DefinitionsHash64 = SimDefinitions.ComputeDefinitionsHash64(),
            Seeds = seeds,
            TickBudget = tickBudget,
            SlotCount = slotCount,
            Commit = string.IsNullOrEmpty(commit) ? "unknown" : commit,
        };

        /// <summary>
        /// Null when the two sets are comparable, otherwise the reason they are
        /// not. Called before every comparison; the report prints the reason
        /// instead of a table.
        /// </summary>
        public string WhyNotComparableWith(ResultSet other)
        {
            if (other == null) return "there is nothing to compare against";
            if (SpecVersion != other.SpecVersion)
                return $"spec version {SpecVersion} vs {other.SpecVersion}";
            if (ProfileSchemaVersion != other.ProfileSchemaVersion)
                return $"profile schema {ProfileSchemaVersion} vs {other.ProfileSchemaVersion}";
            if (DefinitionsHash64 != other.DefinitionsHash64)
                return $"definitions hash 0x{DefinitionsHash64:X16} vs 0x{other.DefinitionsHash64:X16} — " +
                       "the unit and building table changed, so no number here means what it meant there";
            if (TickBudget != other.TickBudget)
                return $"tick budget {TickBudget} vs {other.TickBudget}";
            if (SlotCount != other.SlotCount)
                return $"slot count {SlotCount} vs {other.SlotCount}";
            if (!SameSeeds(Seeds, other.Seeds))
                return $"seed list differs ({Seeds.Length} vs {other.Seeds.Length} seeds) — " +
                       "a different starting set is a different experiment";
            if (!string.Equals(Commit, other.Commit, StringComparison.Ordinal))
                return $"measured at different commits ({Short(Commit)} vs {Short(other.Commit)}) — " +
                       "a merge window shifts behaviour, so frozen sets retire with their commit; " +
                       "re-measure the archive instead of comparing across it";
            return null;
        }

        private static bool SameSeeds(ulong[] a, ulong[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static string Short(string commit) =>
            commit != null && commit.Length > 8 ? commit.Substring(0, 8) : commit;

        // ----------------------------------------------------------------

        public string ToJson()
        {
            var json = new StringBuilder(1024 + Candidates.Count * 320);
            json.Append("{\n");
            json.Append("  \"specVersion\": ").Append(SpecVersion).Append(",\n");
            json.Append("  \"profileSchemaVersion\": ").Append(ProfileSchemaVersion).Append(",\n");
            json.Append("  \"definitionsHash64\": \"0x")
                .Append(DefinitionsHash64.ToString("X16", CultureInfo.InvariantCulture)).Append("\",\n");
            json.Append("  \"commit\": \"").Append(Commit).Append("\",\n");
            json.Append("  \"tickBudget\": ").Append(TickBudget).Append(",\n");
            json.Append("  \"slotCount\": ").Append(SlotCount).Append(",\n");
            json.Append("  \"seeds\": [");
            for (int i = 0; i < Seeds.Length; i++)
            {
                if (i > 0) json.Append(", ");
                json.Append('"').Append("0x").Append(Seeds[i].ToString("X", CultureInfo.InvariantCulture)).Append('"');
            }
            json.Append("],\n");
            json.Append("  \"candidates\": [\n");
            for (int i = 0; i < Candidates.Count; i++)
            {
                CandidateResult c = Candidates[i];
                json.Append("    { \"profileId\": \"").Append(c.ProfileId)
                    .Append("\", \"matches\": ").Append(c.Matches)
                    .Append(", \"wins\": ").Append(c.Wins)
                    .Append(", \"losses\": ").Append(c.Losses)
                    .Append(", \"draws\": ").Append(c.Draws)
                    .Append(", \"winPercent\": ").Append(c.WinPercent)
                    .Append(", \"averageDecidedTick\": ").Append(c.AverageDecidedTick)
                    .Append(", \"averageCredits\": ").Append(c.AverageCredits)
                    .Append(", \"averageArmySize\": ").Append(c.AverageArmySize)
                    .Append(", \"averageUnitsLost\": ").Append(c.AverageUnitsLost)
                    .Append(", \"intentsSubmitted\": ").Append(c.IntentsSubmittedSum)
                    .Append(", \"intentsRejected\": ").Append(c.IntentsRejectedSum)
                    // The four feel columns travel with the numbers they sit
                    // beside; a result set that carries only strength cannot
                    // be re-read later for rhythm.
                    .Append(", \"exchangeRatioPercent\": ").Append(c.AverageExchangeRatio)
                    .Append(", \"combatIntervals\": ").Append(c.AverageCombatIntervals)
                    .Append(", \"largestLossJump\": ").Append(c.AverageLargestLossJump)
                    .Append(", \"reactionLatencyTicks\": ").Append(c.AverageReactionLatency)
                    .Append(", \"unansweredDamage\": ").Append(c.AverageUnansweredDamage)
                    .Append(", \"actionsPerMinute\": ").Append(c.AverageActionsPerMinute)
                    .Append(", \"replayValue\": ").Append(c.ReplayValue)
                    .Append(", \"changes\": \"").Append(string.Join("; ", c.DifferencesFromReference))
                    .Append("\" }");
                if (i < Candidates.Count - 1) json.Append(',');
                json.Append('\n');
            }
            json.Append("  ],\n");
            json.Append("  \"evidence\": \"DIAGNOSIS — never proof. No scalar score, no ranking: ")
                .Append("the numbers sit side by side and a human picks.\"\n");
            json.Append("}\n");
            return json.ToString();
        }
    }
}
