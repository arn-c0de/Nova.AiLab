using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Nova.AiLab
{
    /// <summary>
    /// Reads an archived result set back (plan section 3.7: old sets are
    /// archived with their commit, not deleted — they stay readable, they just
    /// stop being comparable).
    /// <para>
    /// Only the provenance and the aggregate numbers are restored. That is
    /// enough for what an archive is for: telling a report whether a comparison
    /// is allowed, and showing the old numbers beside the new ones when it is.
    /// </para>
    /// </summary>
    public static class ResultSetFile
    {
        public const string FileName = "resultset.json";

        public static ResultSet Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"result set not found: {path}", path);
            return Parse(File.ReadAllText(path), path);
        }

        public static ResultSet Parse(string json, string origin = "<inline>")
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            var set = new ResultSet();

            // EVERY PROVENANCE FIELD IS REQUIRED. Falling back to a default
            // here means falling back to the CURRENT build's value, which makes
            // an archive that never recorded its spec version compare as if it
            // matched. That is the one failure mode section 3.7 exists to
            // prevent: a wrong comparison looks exactly like a right one. A
            // truncated or hand-edited archive must refuse, not agree.
            set.SpecVersion = RequireInt(root, "specVersion", origin);
            set.ProfileSchemaVersion = RequireInt(root, "profileSchemaVersion", origin);
            set.TickBudget = RequireInt(root, "tickBudget", origin);
            set.SlotCount = RequireInt(root, "slotCount", origin);
            set.DefinitionsHash64 = ParseHex(RequireText(root, "definitionsHash64", origin), origin, "definitionsHash64");

            set.Commit = RequireText(root, "commit", origin);
            if (string.IsNullOrWhiteSpace(set.Commit))
            {
                throw new FormatException(
                    $"{origin}: 'commit' is empty — a result set retires with the commit it was measured at, " +
                    "so a set that cannot name one cannot be compared against anything");
            }

            if (!root.TryGetProperty("seeds", out JsonElement seeds) || seeds.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException(
                    $"{origin}: 'seeds' is missing — a different starting set is a different experiment, " +
                    "and a set without its seed list cannot prove it was the same one");
            }
            {
                var parsed = new List<ulong>(seeds.GetArrayLength());
                foreach (JsonElement seed in seeds.EnumerateArray())
                {
                    parsed.Add(ParseHex(seed.GetString(), origin, "seed"));
                }
                set.Seeds = parsed.ToArray();
            }

            if (root.TryGetProperty("candidates", out JsonElement candidates)
                && candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in candidates.EnumerateArray())
                {
                    set.Candidates.Add(ReadCandidate(entry));
                }
            }

            return set;
        }

        /// <summary>
        /// One candidate row, restored to the SUMS the aggregate properties
        /// divide again — an average alone cannot be re-averaged.
        /// <para>
        /// EVERY WRITTEN COLUMN IS READ BACK. The six game-feel columns and the
        /// replay value used to be written and then silently dropped here, so
        /// an archived set reported <c>0</c> exchange ratio, <c>0</c> combat
        /// intervals and <c>-1</c> reaction latency no matter what it had
        /// measured — zeros that read exactly like measurements, in the one
        /// class whose whole job is to keep a wrong comparison from looking
        /// like a right one.
        /// </para>
        /// <para>
        /// The two <c>-1</c> columns keep their meaning across the round trip:
        /// "not measurable in this set" restores as zero SAMPLES, not as a
        /// sample of value -1 that would drag the average down.
        /// </para>
        /// </summary>
        private static CandidateResult ReadCandidate(JsonElement entry)
        {
            int matches = Int(entry, "matches");
            // Older archives predate the explicit divisor; falling back to the
            // match count is what they were read with, so they keep the numbers
            // they always had instead of quietly turning into zeros.
            int decided = entry.TryGetProperty("decidedMatches", out _) ? Int(entry, "decidedMatches") : matches;

            var candidate = new CandidateResult
            {
                ProfileId = Text(entry, "profileId"),
                Matches = matches,
                Wins = Int(entry, "wins"),
                Losses = Int(entry, "losses"),
                Draws = Int(entry, "draws"),
                DecidedMatches = decided,
                DecidedTickSum = (long)Int(entry, "averageDecidedTick") * decided,
                CreditsAtEndSum = (long)Int(entry, "averageCredits") * matches,
                ArmySizeAtEndSum = (long)Int(entry, "averageArmySize") * matches,
                UnitsLostSum = (long)Int(entry, "averageUnitsLost") * matches,
                IntentsSubmittedSum = Int(entry, "intentsSubmitted"),
                IntentsRejectedSum = Int(entry, "intentsRejected"),
                DifferencesFromReference = SplitChanges(Text(entry, "changes")),

                CombatIntervalsSum = (long)Int(entry, "combatIntervals") * matches,
                LargestLossJumpSum = (long)Int(entry, "largestLossJump") * matches,
                UnansweredDamageSum = (long)Int(entry, "unansweredDamage") * matches,
                ActionsPerMinuteSum = (long)Int(entry, "actionsPerMinute") * matches,

                // The archive carries the COUNT of distinct endings, never the
                // endings themselves — so it is restored as the count and the
                // list stays honestly empty.
                ArchivedReplayValue = Int(entry, "replayValue"),
            };

            RestoreSampledAverage(Int(entry, "exchangeRatioPercent"), matches,
                out candidate.ExchangeRatioSum, out candidate.ExchangeRatioSamples);
            RestoreSampledAverage(Int(entry, "reactionLatencyTicks"), matches,
                out candidate.ReactionLatencySum, out candidate.ReactionLatencySamples);

            return candidate;
        }

        /// <summary>
        /// A column that averages over its OWN sample count: <c>-1</c> means
        /// the set produced no sample at all, and restoring it as one sample of
        /// -1 would turn "not measurable" into a measurement below zero.
        /// </summary>
        private static void RestoreSampledAverage(int average, int matches, out long sum, out int samples)
        {
            if (average < 0 || matches <= 0)
            {
                sum = 0;
                samples = 0;
                return;
            }
            sum = (long)average * matches;
            samples = matches;
        }

        private static List<string> SplitChanges(string changes)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(changes)) return list;
            foreach (string part in changes.Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>A provenance integer that must be present — never defaulted.</summary>
        private static int RequireInt(JsonElement root, string name, string origin)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed))
            {
                throw new FormatException(
                    $"{origin}: '{name}' is missing or not a whole number. Provenance is not optional: " +
                    "a missing field would inherit this build's value and the comparison would pass by accident");
            }
            return parsed;
        }

        /// <summary>A provenance string that must be present — never defaulted.</summary>
        private static string RequireText(JsonElement root, string name, string origin)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException($"{origin}: '{name}' is missing — provenance is not optional");
            }
            return value.GetString();
        }

        private static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;

        private static int Int(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : 0;

        private static ulong ParseHex(string text, string origin, string field)
        {
            if (text == null) return 0;
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string digits = hex ? text.Substring(2) : text;
            NumberStyles style = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (!ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out ulong value))
            {
                throw new FormatException($"{origin}: '{field}' is not a number: {text}");
            }
            return value;
        }
    }
}
