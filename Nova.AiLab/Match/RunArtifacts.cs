using System;
using System.Globalization;
using System.IO;
using System.Text;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// Writes one run's artifacts (plan section 3.2): <c>result.json</c>,
    /// <c>trace.ndjson</c> and <c>hashchain.json</c>.
    /// <para>
    /// JSON is written by hand rather than serialized. That is not stubbornness
    /// — it is the only way to GUARANTEE the hard rule that no float leaves the
    /// simulation. A serializer will happily render a double the moment someone
    /// adds one to a metric type; here there is no code path that can.
    /// </para>
    /// <para>
    /// <c>match.replay</c> from the plan's table is not written yet: it belongs
    /// with the replay conformance check of section 3.5, which needs a playable
    /// build on this platform — outstanding from the network strand.
    /// </para>
    /// </summary>
    public static class RunArtifacts
    {
        public const string ResultFileName = "result.json";
        public const string TraceFileName = "trace.ndjson";
        public const string HashChainFileName = "hashchain.json";
        public const string ViewFileName = "view.ndjson";
        public const string TracksFileName = "tracks.ndjson";
        public const string EventsFileName = "events.ndjson";
        public const string UnitsFileName = "units.json";

        public static void Write(string directory, MatchSpec spec, MatchRunResult result)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, ResultFileName), BuildResultJson(spec, result));

            if (result.Trace.Count > 0)
            {
                var trace = new StringBuilder(result.Trace.Count * 256);
                for (int i = 0; i < result.Trace.Count; i++)
                {
                    trace.Append(result.Trace[i].ToJsonLine()).Append('\n');
                }
                File.WriteAllText(Path.Combine(directory, TraceFileName), trace.ToString());
            }

            if (result.HashChain.Count > 0)
            {
                File.WriteAllText(Path.Combine(directory, HashChainFileName), BuildHashChainJson(result));
            }

            if (result.View.Count > 0)
            {
                var view = new StringBuilder(result.View.Count * 512);
                for (int i = 0; i < result.View.Count; i++)
                {
                    view.Append(result.View[i].ToJsonLine()).Append('\n');
                }
                File.WriteAllText(Path.Combine(directory, ViewFileName), view.ToString());

                // The player travels with the frames: an artifact directory is
                // copyable as a unit and opens with a double-click.
                File.WriteAllText(
                    Path.Combine(directory, HtmlPlayer.FileName),
                    HtmlPlayer.Build(spec.MapWidth, spec.MapHeight, result.Seed, spec.Slots));
            }

            if (result.Tracks.Count > 0)
            {
                var tracks = new StringBuilder(result.Tracks.Count * 128);
                for (int i = 0; i < result.Tracks.Count; i++)
                {
                    tracks.Append(result.Tracks[i].ToJsonLine()).Append('\n');
                }
                File.WriteAllText(Path.Combine(directory, TracksFileName), tracks.ToString());
            }

            if (result.Events.Count > 0)
            {
                var events = new StringBuilder(result.Events.Count * 128);
                for (int i = 0; i < result.Events.Count; i++)
                {
                    events.Append(result.Events[i].ToJsonLine()).Append('\n');
                }
                File.WriteAllText(Path.Combine(directory, EventsFileName), events.ToString());
            }

            if (result.Units.Count > 0)
            {
                File.WriteAllText(Path.Combine(directory, UnitsFileName), BuildUnitsJson(result));
            }
        }

        /// <summary>
        /// One row per entity, ascending by id — the per-unit counterpart to
        /// the per-slot columns in <c>result.json</c>. It stays its own file:
        /// a result is a verdict about a match, and this is a hundred rows of
        /// diagnosis that would drown it.
        /// </summary>
        public static string BuildUnitsJson(MatchRunResult result)
        {
            var json = new StringBuilder(result.Units.Count * 256);
            json.Append("{\"units\":[\n");
            for (int i = 0; i < result.Units.Count; i++)
            {
                json.Append("  ").Append(result.Units[i].ToJsonLine());
                if (i < result.Units.Count - 1) json.Append(',');
                json.Append('\n');
            }
            json.Append("],\n\"note\": \"detourPercent -1 = the unit never had a goal to walk towards. ")
                .Append("damageDealtDerived and killsDerived are DERIVED from state, not reported by the ")
                .Append("simulation — see notes/schadensquelle.md.\"\n}\n");
            return json.ToString();
        }

        public static string BuildResultJson(MatchSpec spec, MatchRunResult result)
        {
            var json = new StringBuilder(1024);
            json.Append("{\n");
            json.Append("  \"specVersion\": ").Append(result.SpecVersion).Append(",\n");
            json.Append("  \"seed\": \"0x").Append(result.Seed.ToString("X", CultureInfo.InvariantCulture)).Append("\",\n");
            json.Append("  \"slotCount\": ").Append(result.SlotCount).Append(",\n");
            json.Append("  \"aiSlotCount\": ").Append(result.AiSlotCount).Append(",\n");
            json.Append("  \"tickBudget\": ").Append(result.TickBudget).Append(",\n");
            json.Append("  \"outcome\": \"").Append(result.Outcome).Append("\",\n");
            json.Append("  \"winnerSlot\": ").Append(result.WinnerSlot).Append(",\n");
            json.Append("  \"decidedTick\": ").Append(result.DecidedTick).Append(",\n");
            json.Append("  \"finalTick\": ").Append(result.FinalTick).Append(",\n");
            json.Append("  \"finalStateHash\": \"0x")
                .Append(result.FinalStateHash.ToString("X16", CultureInfo.InvariantCulture)).Append("\",\n");
            // The definitions hash makes a result set self-describing: a report
            // refuses to compare across a changed definition table instead of
            // silently mixing what is not comparable (plan section 3.7).
            json.Append("  \"definitionsHash64\": \"0x")
                .Append(result.DefinitionsHash64.ToString("X16", CultureInfo.InvariantCulture)).Append("\",\n");
            // WHICH AI produced these numbers. The definitions hash pins the
            // unit table, the state hash pins the outcome — neither says which
            // behaviour played. Without this a measurement cannot be tied to an
            // entry in reports/behavior-log.md, which is the whole point of
            // keeping that journal.
            json.Append("  \"aiBehaviorId\": \"").Append(AiBehaviorId.Value).Append("\",\n");
            json.Append("  \"traceIntervalTicks\": ").Append(spec.TraceIntervalTicks).Append(",\n");
            json.Append("  \"hashIntervalTicks\": ").Append(spec.HashIntervalTicks).Append(",\n");
            json.Append("  \"traceSamples\": ").Append(result.Trace.Count).Append(",\n");
            json.Append("  \"hashChainEntries\": ").Append(result.HashChain.Count).Append(",\n");
            json.Append("  \"elapsedMilliseconds\": ").Append(result.ElapsedMilliseconds).Append(",\n");
            json.Append("  \"slots\": [\n");
            for (int i = 0; i < spec.Slots.Length; i++)
            {
                SlotSpec slot = spec.Slots[i];
                // The profile is NAMED, never assumed. This line used to be the
                // literal string "canonical" for every slot of every run, which
                // made the sample run a comparison links into misreport the
                // profile that actually played.
                json.Append("    { \"slot\": ").Append(slot.Slot)
                    .Append(", \"faction\": \"").Append(slot.Faction.ToString().ToLowerInvariant())
                    .Append("\", \"controller\": \"").Append(slot.Controller.ToString().ToLowerInvariant())
                    .Append("\", \"profile\": \"")
                    .Append(slot.IsAi ? slot.ProfileId ?? "unknown" : "none")
                    .Append("\" }");
                if (i < spec.Slots.Length - 1) json.Append(',');
                json.Append('\n');
            }
            json.Append("  ],\n");
            // The four game-feel columns (NEXT-STEPS.md section 7). They sit
            // BESIDE the outcome, never inside it: nothing here is summed into
            // a verdict, and a missing trace leaves the array empty instead of
            // filling it with zeros that would read as measurements.
            json.Append("  \"feel\": [");
            for (int i = 0; i < result.Feel.Count; i++)
            {
                if (i > 0) json.Append(", ");
                result.Feel[i].AppendJson(json);
            }
            json.Append("],\n");
            json.Append("  \"evidence\": \"DIAGNOSIS — a lab run is never proof; ")
                .Append("what was not seen in the running game is reported as unseen.\"\n");
            json.Append("}\n");
            return json.ToString();
        }

        public static string BuildHashChainJson(MatchRunResult result)
        {
            var json = new StringBuilder(result.HashChain.Count * 48);
            json.Append("{\"seed\":\"0x").Append(result.Seed.ToString("X", CultureInfo.InvariantCulture))
                .Append("\",\"entries\":[\n");
            for (int i = 0; i < result.HashChain.Count; i++)
            {
                HashChainEntry entry = result.HashChain[i];
                json.Append("  {\"tick\":").Append(entry.Tick)
                    .Append(",\"stateHash\":\"0x")
                    .Append(entry.StateHash.ToString("X16", CultureInfo.InvariantCulture)).Append("\"}");
                if (i < result.HashChain.Count - 1) json.Append(',');
                json.Append('\n');
            }
            json.Append("]}\n");
            return json.ToString();
        }
    }
}
