using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Writes <c>player.html</c> again into artifact directories that already
    /// have their data.
    /// <para>
    /// WHY THIS IS A MODE AND NOT A NOTE IN THE README. Every run drops its own
    /// player beside its frames, so an artifact directory stays copyable and
    /// opens with a double-click — that is deliberate and stays. The price is
    /// that a run measured last week is looked at through last week's page, and
    /// every improvement to the view is invisible for everything already
    /// measured. Re-measuring to get a better VIEWER is the wrong trade: it
    /// costs minutes and it changes the numbers underneath.
    /// </para>
    /// <para>
    /// The page needs three things the data cannot carry: the seed, who sat in
    /// which seat, and the map size. The first two come out of
    /// <c>result.json</c>; the map is the canonical one every mode of this lab
    /// runs, so its size comes from the spec defaults — and if a run ever used
    /// another, this is the line that has to learn about it.
    /// </para>
    /// </summary>
    internal static class PlayerCommand
    {
        public static int Run(Options options)
        {
            if (string.IsNullOrEmpty(options.OutputDirectory))
            {
                Console.Error.WriteLine("player: --out <dir> names the artifact directory (or a parent of several)");
                return 1;
            }

            var directories = new List<string>();
            Collect(options.OutputDirectory, directories);
            if (directories.Count == 0)
            {
                Console.Error.WriteLine(
                    $"player: no artifact directory under '{options.OutputDirectory}' — " +
                    $"a directory counts when it holds {RunArtifacts.ViewFileName}");
                return 1;
            }

            int written = 0;
            foreach (string directory in directories)
            {
                string resultPath = Path.Combine(directory, "result.json");
                ulong seed = options.Spec.Seed;
                SlotSpec[] slots = options.Spec.Slots;
                if (File.Exists(resultPath))
                {
                    string json = File.ReadAllText(resultPath);
                    seed = ReadSeed(json, seed);
                    slots = ReadSlots(json) ?? slots;
                }
                else
                {
                    Console.WriteLine($"[player] {directory}: no result.json — seed and seats come from the flags");
                }

                File.WriteAllText(
                    Path.Combine(directory, HtmlPlayer.FileName),
                    HtmlPlayer.Build(options.Spec.MapWidth, options.Spec.MapHeight, seed, slots));
                Console.WriteLine($"[player] {Path.Combine(directory, HtmlPlayer.FileName)}");
                written++;
            }

            Console.WriteLine($"[player] {written} page(s) rewritten — the data beside them is untouched");
            return 0;
        }

        /// <summary>The directory itself when it holds frames, otherwise every child that does.</summary>
        private static void Collect(string root, List<string> found)
        {
            if (!Directory.Exists(root)) return;
            if (File.Exists(Path.Combine(root, RunArtifacts.ViewFileName)))
            {
                found.Add(root);
                return;
            }
            foreach (string child in Directory.GetDirectories(root))
            {
                if (File.Exists(Path.Combine(child, RunArtifacts.ViewFileName))) found.Add(child);
            }
        }

        // result.json is written by RunArtifacts one field per line, so the two
        // values are read with the string tools rather than a JSON dependency
        // this project deliberately does not have.
        private static ulong ReadSeed(string json, ulong fallback)
        {
            string value = Field(json, "\"seed\": \"");
            if (value == null) return fallback;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(value.Substring(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out ulong parsed) ? parsed : fallback;
            }
            return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong plain)
                ? plain : fallback;
        }

        private static SlotSpec[] ReadSlots(string json)
        {
            int start = json.IndexOf("\"slots\": [", StringComparison.Ordinal);
            if (start < 0) return null;
            int end = json.IndexOf(']', start);
            if (end < 0) return null;

            var slots = new List<SlotSpec>();
            foreach (string line in json.Substring(start, end - start).Split('\n'))
            {
                string slot = Field(line, "\"slot\": ", '}');
                string faction = Field(line, "\"faction\": \"");
                string profile = Field(line, "\"profile\": \"");
                if (slot == null || faction == null) continue;

                int comma = slot.IndexOf(',');
                if (comma >= 0) slot = slot.Substring(0, comma);
                if (!byte.TryParse(slot.Trim(), out byte number)) continue;
                if (!Enum.TryParse(faction, true, out FactionId id)) continue;

                slots.Add(new SlotSpec
                {
                    Slot = number,
                    Faction = id,
                    ProfileId = profile ?? SlotSpec.CanonicalProfileId,
                });
            }
            return slots.Count > 0 ? slots.ToArray() : null;
        }

        private static string Field(string json, string key, char terminator = '"')
        {
            int start = json.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            int end = json.IndexOf(terminator, start);
            return end < 0 ? null : json.Substring(start, end - start);
        }
    }
}
