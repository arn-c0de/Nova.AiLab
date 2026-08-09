using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Nova.AI;
using Nova.AI.Data;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Reads the JSON MatchSpec of plan section 3.2.
    /// <para>
    /// Deliberately hand-rolled instead of deserialized onto the type: the spec
    /// is a CONTRACT, and an unknown or misspelled key must be an error rather
    /// than a silent default. A sweep that quietly ran with
    /// <c>tickBudget</c> = 27.000 because the file said <c>tickbudget</c> would
    /// produce numbers nobody can reproduce.
    /// </para>
    /// <para>
    /// Integers only, everywhere. A float in a spec file becomes a float in the
    /// simulation, and <c>NoFloatInSimulationTests</c> exists for exactly that
    /// reason.
    /// </para>
    /// </summary>
    public static class SpecFile
    {
        public static MatchSpec Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"spec file not found: {path}", path);
            return Parse(File.ReadAllText(path), path);
        }

        public static MatchSpec Parse(string json, string origin = "<inline>")
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"{origin}: the spec must be a JSON object");
            }

            var spec = new MatchSpec();
            int slotCount = 2;
            bool slotsGiven = false;

            foreach (JsonProperty property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "specVersion":
                        int version = RequireInt(property, origin);
                        if (version != MatchSpec.SpecVersion)
                        {
                            throw new FormatException(
                                $"{origin}: specVersion {version} is not readable by this lab " +
                                $"(expected {MatchSpec.SpecVersion}). A result set carries its spec version " +
                                "so a comparison across versions is refused instead of silently mixed.");
                        }
                        break;

                    case "mode":
                        // A spec file describes a MATCH. The duel arena and the
                        // movement scenarios exist (E5) but build their own
                        // hosts from their own spec types, so they are CLI
                        // modes, not spec modes — naming one here would read
                        // like it configured something and would configure
                        // nothing.
                        string mode = property.Value.GetString();
                        if (mode != "match")
                        {
                            throw new FormatException(
                                $"{origin}: mode '{mode}' is not a spec mode — a spec file describes a match. " +
                                "'duel' and 'movement' are command-line modes with their own scenario parameters " +
                                "(nova-ailab duel --units …, nova-ailab movement --group …)");
                        }
                        break;

                    case "seed": spec.Seed = RequireSeed(property, origin); break;
                    case "tickBudget": spec.TickBudget = RequireInt(property, origin); break;
                    case "mapWidth": spec.MapWidth = RequireMapExtent(property, origin); break;
                    case "mapHeight": spec.MapHeight = RequireMapExtent(property, origin); break;
                    case "entityCapacity": spec.EntityCapacity = RequireInt(property, origin); break;
                    case "startingCreditsAE": spec.StartingCreditsAE = RequireLong(property, origin); break;
                    case "traceIntervalTicks": spec.TraceIntervalTicks = RequireInt(property, origin); break;
                    case "hashIntervalTicks": spec.HashIntervalTicks = RequireInt(property, origin); break;
                    case "viewIntervalTicks": spec.ViewIntervalTicks = RequireInt(property, origin); break;
                    case "trackIntervalTicks": spec.TrackIntervalTicks = RequireInt(property, origin); break;
                    case "recordFog": spec.RecordFog = property.Value.GetBoolean(); break;

                    case "slots":
                        spec.Slots = ReadSlots(property.Value, origin);
                        slotCount = spec.Slots.Length;
                        slotsGiven = true;
                        break;

                    default:
                        throw new FormatException(
                            $"{origin}: unknown spec key '{property.Name}' — a misspelled key must not " +
                            "silently fall back to a default");
                }
            }

            if (!slotsGiven) spec.Slots = MatchSpec.DefaultSlots(slotCount);
            Validate(spec, origin);
            return spec;
        }

        private static SlotSpec[] ReadSlots(JsonElement element, string origin)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"{origin}: 'slots' must be an array");
            }

            int count = element.GetArrayLength();
            if (count < 2 || count > CommandLimits.ReservedPlayerSlots)
            {
                throw new FormatException(
                    $"{origin}: a match needs between 2 and {CommandLimits.ReservedPlayerSlots} slots, got {count}");
            }

            var slots = new SlotSpec[count];
            int index = 0;
            foreach (JsonElement entry in element.EnumerateArray())
            {
                var slot = new SlotSpec { Slot = (byte)index, Faction = FactionId.Alliance, Controller = SlotController.Ai };
                bool factionGiven = false;
                bool profileGiven = false;
                AiProfile namedProfile = default;

                foreach (JsonProperty property in entry.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "slot":
                            int declared = RequireInt(property, origin);
                            if (declared != index)
                            {
                                throw new FormatException(
                                    $"{origin}: slot entry {index} declares itself as slot {declared} — " +
                                    "slots must be dense and ascending, their order fixes entity ids and every hash");
                            }
                            break;

                        case "faction":
                            slot.Faction = ParseFaction(property.Value.GetString(), origin);
                            factionGiven = true;
                            break;

                        case "controller":
                            string controller = property.Value.GetString();
                            slot.Controller = controller switch
                            {
                                "ai" => SlotController.Ai,
                                "passive" => SlotController.Passive,
                                "scripted" => SlotController.Scripted,
                                _ => throw new FormatException(
                                    $"{origin}: controller '{controller}' is unknown — 'ai', 'passive' or 'scripted'"),
                            };
                            break;

                        case "profile":
                            // E6 turned profiles into data under AI.Data/, so a
                            // spec may now name any candidate the lab knows.
                            // An unknown name still fails loudly rather than
                            // running the shipped profile under a foreign label
                            // — that would put a wrong provenance on every
                            // number the run produces.
                            string profile = property.Value.GetString();
                            if (profile == "canonical") profile = SlotSpec.CanonicalProfileId;
                            if (!LabProfiles.TryGet(profile, out AiProfile named))
                            {
                                throw new FormatException(
                                    $"{origin}: profile '{profile}' is unknown — known ids: {LabProfiles.KnownIds()}");
                            }
                            namedProfile = named;
                            profileGiven = true;
                            break;

                        default:
                            throw new FormatException($"{origin}: unknown slot key '{property.Name}'");
                    }
                }

                if (!factionGiven)
                {
                    slot.Faction = (index % 2) == 0 ? FactionId.Alliance : FactionId.Legion;
                }

                if (profileGiven)
                {
                    slot.Profile = new AiFactionProfile(slot.Faction.ToString(), namedProfile);
                    slot.ProfileId = namedProfile.ProfileId;
                }
                else
                {
                    slot.Profile = SlotSpec.CanonicalProfile(slot.Faction);
                    slot.ProfileId = SlotSpec.CanonicalProfileId;
                }

                slots[index] = slot;
                index++;
            }

            return slots;
        }

        private static FactionId ParseFaction(string value, string origin) => value switch
        {
            "alliance" => FactionId.Alliance,
            "legion" => FactionId.Legion,
            _ => throw new FormatException($"{origin}: unknown faction '{value}' — 'alliance' or 'legion'"),
        };

        private static void Validate(MatchSpec spec, string origin)
        {
            if (spec.TickBudget < 1) throw new FormatException($"{origin}: tickBudget must be positive");
            if (spec.TraceIntervalTicks < 0) throw new FormatException($"{origin}: traceIntervalTicks must not be negative");
            if (spec.ViewIntervalTicks < 0) throw new FormatException($"{origin}: viewIntervalTicks must not be negative");
            if (spec.TrackIntervalTicks < 0) throw new FormatException($"{origin}: trackIntervalTicks must not be negative");
            if (spec.HashIntervalTicks < 0) throw new FormatException($"{origin}: hashIntervalTicks must not be negative");
            if (spec.EntityCapacity < 1) throw new FormatException($"{origin}: entityCapacity must be positive");
            if (spec.Slots.Length > CanonicalOpening.MaxSeatedSlots)
            {
                throw new FormatException(
                    $"{origin}: {spec.Slots.Length} slots, but the canonical map seats " +
                    $"{CanonicalOpening.MaxSeatedSlots} bases (more seats are map work, plan E11)");
            }
        }

        // ----------------------------------------------------------------

        private static int RequireInt(JsonProperty property, string origin)
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int value))
            {
                throw new FormatException($"{origin}: '{property.Name}' must be a whole number");
            }
            return value;
        }

        /// <summary>
        /// A map extent, range-checked instead of cast. <c>(ushort)</c> on a
        /// raw int turns 70.000 into 4.464 and -1 into 65.535 without a word,
        /// and a spec that silently ran on a different map than it names would
        /// produce numbers nobody can reproduce.
        /// </summary>
        private static ushort RequireMapExtent(JsonProperty property, string origin)
        {
            int value = RequireInt(property, origin);
            if (value < 1 || value > ushort.MaxValue)
            {
                throw new FormatException(
                    $"{origin}: '{property.Name}' must be between 1 and {ushort.MaxValue}, got {value}");
            }
            return (ushort)value;
        }

        private static long RequireLong(JsonProperty property, string origin)
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out long value))
            {
                throw new FormatException($"{origin}: '{property.Name}' must be a whole number");
            }
            return value;
        }

        /// <summary>Seeds are written as 0x-hex strings or plain numbers.</summary>
        private static ulong RequireSeed(JsonProperty property, string origin)
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetUInt64(out ulong number))
            {
                return number;
            }

            string text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (text != null)
            {
                bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                string digits = hex ? text.Substring(2) : text;
                NumberStyles style = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
                if (ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out ulong parsed)) return parsed;
            }

            throw new FormatException($"{origin}: 'seed' must be a number or a 0x-hex string");
        }
    }
}
