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
                    set.Candidates.Add(new CandidateResult
                    {
                        ProfileId = Text(entry, "profileId"),
                        Matches = Int(entry, "matches"),
                        Wins = Int(entry, "wins"),
                        Losses = Int(entry, "losses"),
                        Draws = Int(entry, "draws"),
                        DecidedMatches = Int(entry, "matches"),
                        DecidedTickSum = (long)Int(entry, "averageDecidedTick") * Int(entry, "matches"),
                        CreditsAtEndSum = (long)Int(entry, "averageCredits") * Int(entry, "matches"),
                        ArmySizeAtEndSum = (long)Int(entry, "averageArmySize") * Int(entry, "matches"),
                        UnitsLostSum = (long)Int(entry, "averageUnitsLost") * Int(entry, "matches"),
                        IntentsSubmittedSum = Int(entry, "intentsSubmitted"),
                        IntentsRejectedSum = Int(entry, "intentsRejected"),
                        DifferencesFromReference = SplitChanges(Text(entry, "changes")),
                    });
                }
            }

            return set;
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
