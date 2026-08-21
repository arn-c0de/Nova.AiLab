using System;
using System.Collections.Generic;
using System.IO;
using Nova.AI.Data;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>raid</c> — the field raid at a sweep of guard distances, for both
    /// fields the AI owns.
    /// <para>
    /// THE SWEEP IS THE MEASUREMENT, not one run of it. A single distance
    /// answers "did it react here"; the row of distances answers "where does
    /// the reaction stop", and only the second one distinguishes a range
    /// problem from a missing rule. So the default is the sweep, and a single
    /// <c>--guard-distance</c> narrows it to one row on purpose.
    /// </para>
    /// </summary>
    internal static class RaidCommand
    {
        /// <summary>
        /// Distances in cells. Chosen around the weapons in play rather than
        /// evenly: Legion rifles reach 6, the tanks 8, anti-armour 9 — so the
        /// interesting band is 2 to 12, and 20 and 35 are there to show what a
        /// definite out-of-range row looks like. 35 is roughly the distance
        /// from a base to its expansion field.
        /// </summary>
        private static readonly int[] DistanceSweep = { 2, 4, 6, 8, 10, 12, 20, 35 };

        public static int Run(Options options)
        {
            var results = new List<RaidResult>();
            int[] distances = options.GuardDistanceCells > 0
                ? new[] { options.GuardDistanceCells }
                : DistanceSweep;

            RaidField[] fields = options.RaidField.HasValue
                ? new[] { options.RaidField.Value }
                : new[] { RaidField.Start, RaidField.Expansion };

            AiProfile profile = options.Spec.Slots[1].Profile.Profile;
            string profileId = options.Spec.Slots[1].ProfileId;

            foreach (RaidField field in fields)
            foreach (int distance in distances)
            {
                results.Add(RaidScenarios.Run(new RaidSpec
                {
                    Seed = options.Spec.Seed,
                    Field = field,
                    GuardCount = options.GroupSize,
                    GuardDistanceCells = distance,
                    GuardRole = options.GuardRole,
                    RaidDelayTicks = options.RaidDelayTicks,
                    TickBudget = options.Spec.TickBudget,
                    Profile = profile,
                    ProfileId = profileId,
                }));
            }

            RaidField? printed = null;
            foreach (RaidResult r in results)
            {
                if (printed != r.Field)
                {
                    printed = r.Field;
                    Console.WriteLine();
                    Console.WriteLine(
                        $"{r.Field}-Feld ({r.RaidCellX},{r.RaidCellY}) — {r.RaidToHomeCells} Zellen vom HQ " +
                        $"({r.HqCellX},{r.HqCellY}), DefendHomeCells = {r.DefendHomeCells} " +
                        $"=> {(r.RaidToHomeCells <= r.DefendHomeCells ? "INNERHALB" : "ausserhalb")} des Heimradius");
                    Console.WriteLine(
                        $"  {r.GuardCount} x {r.GuardRole} (Reichweite {r.GuardRangeCells}) gegen einen Angreifer am Harvester");
                    if (r.RaidDelayTicks > 0)
                    {
                        Console.WriteLine(
                            $"  Vorlauf {r.RaidDelayTicks} Ticks — der Angreifer erscheint erst danach");
                    }
                    Console.WriteLine(
                        "  Abstand  bei Start  Rueckfeuer  DefendHome  naeher bis  Ticks i.R.  Harvester  Verdikt");
                }

                string fire = r.ReturnFireTick < 0 ? "nie" : $"T{r.ReturnFireTick}";
                string defend = r.DefendHomeTick < 0 ? "nie" : $"T{r.DefendHomeTick}";
                string harvester = r.HarvesterDied
                    ? $"tot T{r.HarvesterDeathTick}"
                    : $"{r.HarvesterEndHealth}/{r.HarvesterStartHealth}";

                Console.WriteLine(
                    $"  {r.GuardDistanceCells,7}  {r.GuardDistanceAtRaidStart,9}  {fire,10}  {defend,10}  " +
                    $"{r.ClosestGuardApproachCells,10}  {r.GuardTicksInRange,10}  {harvester,9}  {r.Verdict}");

                if (r.RejectedOrders > 0)
                {
                    Console.Error.WriteLine(
                        $"  {r.RejectedOrders} Befehle abgelehnt — diese Zeile ist keine Messung");
                }
            }

            if (options.OutputDirectory != null)
            {
                Directory.CreateDirectory(options.OutputDirectory);
                string path = Path.Combine(options.OutputDirectory, "raid.ndjson");
                File.WriteAllText(path, RaidScenarios.ToNdjson(results));
                Console.WriteLine();
                Console.WriteLine($"results written to {path}");
            }
            return 0;
        }
    }
}
