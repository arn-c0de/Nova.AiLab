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

        /// <summary>Loopback port of the <c>live</c> session. Only the port is settable — the address never is.</summary>
        public int Port = 8787;

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
            var options = new Options();
            var flags = new Dictionary<string, string>();
            var switches = new HashSet<string> { "--fog", "--watch" };

            for (int i = 1; i < args.Length; i++)
            {
                string flag = args[i];
                if (switches.Contains(flag))
                {
                    flags[flag] = "true";
                    continue;
                }
                if (i + 1 >= args.Length) throw new ArgumentException($"option '{flag}' needs a value");
                flags[flag] = args[++i];
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
                    case "--slots": slots = ParseInt(flag.Value, flag.Key); break;
                    case "--ticks": options.Spec.TickBudget = ParseInt(flag.Value, flag.Key); break;
                    case "--trace-every": options.Spec.TraceIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                    case "--hash-every": options.Spec.HashIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                    case "--view-every": options.Spec.ViewIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                    case "--track-every": options.Spec.TrackIntervalTicks = ParseInt(flag.Value, flag.Key); break;
                    case "--fog": options.Spec.RecordFog = true; break;
                    case "--watch": options.Watch = true; break;
                    case "--repeat": options.Repeat = ParseInt(flag.Value, flag.Key); break;
                    case "--seeds": options.SeedCount = ParseInt(flag.Value, flag.Key); break;
                    case "--parallel": options.Parallelism = ParseInt(flag.Value, flag.Key); break;
                    case "--units": options.UnitsPerSide = ParseInt(flag.Value, flag.Key); break;
                    case "--group": options.GroupSize = ParseInt(flag.Value, flag.Key); break;
                    case "--against": options.AgainstFile = flag.Value; break;
                    case "--port": options.Port = ParseInt(flag.Value, flag.Key); break;
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
            if (options.Spec.TickBudget < 1) throw new ArgumentException("--ticks must be positive");
            if (options.Port < 1 || options.Port > 65535) throw new ArgumentException("--port must be a port number");
            if (options.Repeat < 1) throw new ArgumentException("--repeat must be positive");
            if (options.SeedCount < 1) throw new ArgumentException("--seeds must be positive");

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
    }
}
