using System;
using System.Collections.Generic;
using System.Globalization;
using Nova.AI;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// Everything the five modes read out of the command line. A spec file is
    /// the base, explicit flags override it — so a saved spec can be re-run
    /// with one number changed without editing the file.
    /// </summary>
    internal sealed class Options
    {
        public MatchSpec Spec;
        public int Repeat = 1;
        public int SeedCount = 8;
        public int Parallelism;
        public string OutputDirectory;
        public bool Watch;
        public int UnitsPerSide = DuelTable.DefaultUnitsPerSide;
        public int GroupSize = 8;
        public string AgainstFile;

        /// <summary>
        /// Flags that carry no value. Named once: a switch missing from this
        /// set would swallow the NEXT argument as its value, which is the kind
        /// of failure that produces a run nobody can explain.
        /// </summary>
        private static readonly HashSet<string> SwitchFlags = new HashSet<string> { "--fog", "--watch" };

        /// <summary>
        /// Which flags each mode actually READS.
        /// <para>
        /// A flag a mode ignores used to be accepted in silence:
        /// <c>duel --view-every 25</c> parsed, ran, and wrote no frames, because
        /// the duel arena builds its own spec and never looks at that field.
        /// The same lab refuses an unknown key in a spec file with the argument
        /// that "a misspelled key must not silently fall back to a default"
        /// (<see cref="SpecFile"/>) — the command line had no such rule, and a
        /// number measured under settings that were never applied is exactly
        /// the number the spec reader exists to prevent.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> FlagsPerMode =
            new Dictionary<string, HashSet<string>>
            {
                ["match"] = new HashSet<string>
                {
                    "--spec", "--seed", "--slots", "--ticks",
                    "--trace-every", "--hash-every", "--view-every", "--track-every", "--fog",
                    "--profile", "--profile0", "--profile1",
                    "--repeat", "--watch", "--out",
                },
                ["sweep"] = new HashSet<string>
                {
                    "--spec", "--seed", "--slots", "--ticks",
                    "--trace-every", "--hash-every", "--view-every", "--track-every", "--fog",
                    "--profile", "--profile0", "--profile1",
                    "--seeds", "--parallel", "--out",
                },
                // The arena and the scenarios build their own specs from their
                // own types, so only the budget and the output reach them.
                ["duel"] = new HashSet<string> { "--ticks", "--units", "--parallel", "--out" },
                ["movement"] = new HashSet<string> { "--ticks", "--group", "--out" },
                // The tournament seats and profiles the candidates itself
                // (that IS the comparison), so --slots and --profile would
                // name something it overrules.
                ["compare"] = new HashSet<string>
                {
                    "--spec", "--seed", "--ticks", "--seeds", "--parallel", "--against", "--out",
                },
            };

        /// <summary>Every flag any mode knows — separates "wrong mode" from "typo".</summary>
        private static readonly HashSet<string> AllFlags = BuildAllFlags();

        private static HashSet<string> BuildAllFlags()
        {
            var all = new HashSet<string>();
            foreach (KeyValuePair<string, HashSet<string>> mode in FlagsPerMode)
            {
                foreach (string flag in mode.Value) all.Add(flag);
            }
            return all;
        }

        /// <summary>The flags of one mode, in a stable order, for an error message.</summary>
        private static string FlagsOf(string mode)
        {
            var names = new List<string>(FlagsPerMode[mode]);
            names.Sort(StringComparer.Ordinal);
            return string.Join(" ", names);
        }

        /// <summary>Binds a named lab profile to one slot; an unknown id names the known ones instead of failing mutely.</summary>
        private static void ApplyProfile(SlotSpec slot, string profileId)
        {
            string id = profileId == "canonical" ? SlotSpec.CanonicalProfileId : profileId;
            if (!LabProfiles.TryGet(id, out AiProfile profile))
            {
                throw new ArgumentException($"profile '{profileId}' is unknown — known ids: {LabProfiles.KnownIds()}");
            }
            slot.Profile = new AiFactionProfile(slot.Faction.ToString(), profile);
            slot.ProfileId = profile.ProfileId;
        }

        public static Options Parse(string[] args, string mode)
        {
            if (!FlagsPerMode.ContainsKey(mode)) throw new ArgumentException($"unknown mode '{mode}'");

            var options = new Options();
            var flags = new Dictionary<string, string>();

            for (int i = 1; i < args.Length; i++)
            {
                string flag = args[i];
                if (SwitchFlags.Contains(flag))
                {
                    flags[flag] = "true";
                    continue;
                }
                if (i + 1 >= args.Length) throw new ArgumentException($"option '{flag}' needs a value");
                flags[flag] = args[++i];
            }

            // A flag this mode does not read is refused, not ignored. The
            // distinction between the two messages matters: a typo and a flag
            // meant for another mode are different mistakes.
            HashSet<string> allowed = FlagsPerMode[mode];
            foreach (string flag in flags.Keys)
            {
                if (allowed.Contains(flag)) continue;
                throw new ArgumentException(AllFlags.Contains(flag)
                    ? $"option '{flag}' does not apply to mode '{mode}' — nothing would read it. " +
                      $"'{mode}' takes: {FlagsOf(mode)}"
                    : $"unknown option '{flag}'");
            }

            // The spec file is the base; explicit flags override it, so a
            // saved spec can be re-run with one number changed without
            // editing the file.
            options.Spec = flags.TryGetValue("--spec", out string specPath)
                ? SpecFile.Load(specPath)
                : new MatchSpec();

            int? slots = null;
            string profileAll = null, profileSlot0 = null, profileSlot1 = null;
            foreach (KeyValuePair<string, string> flag in flags)
            {
                switch (flag.Key)
                {
                    case "--spec": break;
                    case "--seed": options.Spec.Seed = ParseSeed(flag.Value); break;
                    case "--slots": slots = ParsePositive(flag.Value, flag.Key); break;
                    case "--ticks": options.Spec.TickBudget = ParsePositive(flag.Value, flag.Key); break;
                    case "--trace-every": options.Spec.TraceIntervalTicks = ParseInterval(flag.Value, flag.Key); break;
                    case "--hash-every": options.Spec.HashIntervalTicks = ParseInterval(flag.Value, flag.Key); break;
                    case "--view-every": options.Spec.ViewIntervalTicks = ParseInterval(flag.Value, flag.Key); break;
                    case "--track-every": options.Spec.TrackIntervalTicks = ParseInterval(flag.Value, flag.Key); break;
                    case "--fog": options.Spec.RecordFog = true; break;
                    case "--watch": options.Watch = true; break;
                    case "--repeat": options.Repeat = ParsePositive(flag.Value, flag.Key); break;
                    case "--seeds": options.SeedCount = ParsePositive(flag.Value, flag.Key); break;
                    case "--parallel": options.Parallelism = ParsePositive(flag.Value, flag.Key); break;
                    case "--units": options.UnitsPerSide = ParsePositive(flag.Value, flag.Key); break;
                    case "--group": options.GroupSize = ParsePositive(flag.Value, flag.Key); break;
                    case "--against": options.AgainstFile = flag.Value; break;
                    case "--profile": profileAll = flag.Value; break;
                    case "--profile0": profileSlot0 = flag.Value; break;
                    case "--profile1": profileSlot1 = flag.Value; break;
                    case "--out": options.OutputDirectory = flag.Value; break;
                    default: throw new ArgumentException($"unknown option '{flag.Key}'");
                }
            }

            if (slots.HasValue) options.Spec.Slots = MatchSpec.DefaultSlots(slots.Value);

            // A named profile per slot, so ONE-SIDED runs need no spec file.
            // That matters more than convenience: a rule that lives in C#
            // reaches both AIs of a self-play match, and "with" against
            // "without" is the only arrangement that can tell a better rule
            // from two stronger armies (behaviour journal M001).
            if (profileAll != null)
            {
                for (int i = 0; i < options.Spec.Slots.Length; i++) ApplyProfile(options.Spec.Slots[i], profileAll);
            }
            if (profileSlot0 != null && options.Spec.Slots.Length > 0) ApplyProfile(options.Spec.Slots[0], profileSlot0);
            if (profileSlot1 != null && options.Spec.Slots.Length > 1) ApplyProfile(options.Spec.Slots[1], profileSlot1);

            // Watching needs frames; 20 ticks = 2 s of simulated time, the
            // AI's own decision cadence, so every frame can differ.
            if (options.Watch && options.Spec.ViewIntervalTicks <= 0) options.Spec.ViewIntervalTicks = 20;

            // A duel is seconds, not a match: the 27.000-tick match default
            // would just idle after the last unit died. An explicit --ticks
            // still wins.
            if ((mode == "duel" || mode == "movement") && !flags.ContainsKey("--ticks")) options.Spec.TickBudget = 3000;

            // The seed axis is empty, so a comparison defaults to ONE seed
            // instead of pretending eight of them are eight observations.
            if (mode == "compare" && !flags.ContainsKey("--seeds")) options.SeedCount = 1;

            if (options.Spec.Slots.Length > CanonicalOpening.MaxSeatedSlots)
            {
                throw new ArgumentException(
                    $"{options.Spec.Slots.Length} slots: the canonical map seats " +
                    $"{CanonicalOpening.MaxSeatedSlots} bases (more seats are map work, plan E11)");
            }
            return options;
        }

        private static ulong ParseSeed(string value)
        {
            bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string digits = hex ? value.Substring(2) : value;
            NumberStyles style = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (!ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out ulong parsed))
            {
                throw new ArgumentException($"'{value}' is not a valid seed");
            }
            return parsed;
        }

        private static int ParseInt(string value, string flag)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException($"'{value}' is not a valid value for {flag}");
            }
            return parsed;
        }

        private static int ParsePositive(string value, string flag)
        {
            int parsed = ParseInt(value, flag);
            if (parsed < 1) throw new ArgumentException($"{flag} must be positive, got {parsed}");
            return parsed;
        }

        /// <summary>
        /// A sampling interval: 0 turns the stream off, negative is refused.
        /// <para>
        /// <see cref="SpecFile"/> has rejected a negative interval in a spec
        /// file since it was written, but the flags OVERRIDE the spec file and
        /// had no such check — so <c>--view-every -5</c> walked straight past
        /// it. It did not fail: the interval is cast to <c>uint</c> in the
        /// match loop, -5 becomes 4.294.967.291, and <c>tick % that</c> is
        /// never 0. The run finished green, wrote no frames, and said nothing
        /// about why. A stream silently switched off by a typo is the one
        /// outcome worse than a refused command line.
        /// </para>
        /// </summary>
        private static int ParseInterval(string value, string flag)
        {
            int parsed = ParseInt(value, flag);
            if (parsed < 0) throw new ArgumentException($"{flag} must not be negative, got {parsed} (0 turns it off)");
            return parsed;
        }
    }
}
