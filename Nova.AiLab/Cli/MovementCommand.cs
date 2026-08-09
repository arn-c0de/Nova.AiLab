using System;
using System.Collections.Generic;
using System.IO;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// <c>movement</c> — the four scenarios (arrival, blocking, standoff,
    /// detour) for both factions. A row whose orders were refused is printed
    /// to stderr and is not a measurement.
    /// </summary>
    internal static class MovementCommand
    {
        public static int Run(Options options)
        {
            var results = new List<MovementResult>();
            foreach (MovementScenario scenario in new[]
                     {
                         MovementScenario.Arrival, MovementScenario.Blocking,
                         MovementScenario.Standoff, MovementScenario.Detour,
                     })
            foreach (FactionId faction in DuelTable.Factions)
            {
                // Standoff only makes sense for a unit that HAS a range worth
                // keeping; running it on melee-range infantry would measure
                // nothing and read like a result.
                UnitRole role = scenario == MovementScenario.Standoff ? UnitRole.Artillery : UnitRole.BasicInfantry;
                results.Add(MovementScenarios.Run(new MovementSpec
                {
                    Scenario = scenario,
                    Faction = faction,
                    Role = role,
                    GroupSize = options.GroupSize,
                    TickBudget = options.Spec.TickBudget,
                }));
            }

            foreach (MovementResult r in results)
            {
                // Standoff measures an approach, not an arrival. Printing
                // "arrived 0/8" there reads like a failed run instead of a
                // column that does not apply.
                bool measuresArrival = r.Scenario != MovementScenario.Standoff;
                string arrival = measuresArrival
                    ? $"arrived {r.Arrived}/{r.GroupSize}  spread {r.SpreadCells,3}  " +
                      $"first/last {r.TicksToFirstArrival,5}/{r.TicksToLastArrival,5}"
                    : $"arrival n/a{new string(' ', 34)}";

                Console.WriteLine($"{r.Scenario,-9} {r.Faction,-8} {r.Role,-14} {arrival}  " + Detail(r));
                if (r.RejectedOrders > 0)
                {
                    Console.Error.WriteLine($"  {r.RejectedOrders} orders refused — this row is not a measurement");
                }
            }

            if (options.OutputDirectory != null)
            {
                Directory.CreateDirectory(options.OutputDirectory);
                string path = Path.Combine(options.OutputDirectory, "movement.ndjson");
                File.WriteAllText(path, MovementScenarios.ToNdjson(results));
                Console.WriteLine($"results written to {path}");
            }
            return 0;
        }

        private static string Detail(MovementResult r) => r.Scenario switch
        {
            // The gap is MEASURED out of the cost field, not assumed from the
            // parameter: a wall with an unnoticed second opening turns "nobody
            // was blocked" into a property of the scenario.
            MovementScenario.Blocking =>
                $"gap {r.WallGapCells} cells at y={r.WallGapStartCell}  " +
                $"blocked {r.BlockedUnits} units, {r.BlockedTicksTotal} tick-units, longest {r.LongestSingleBlockTicks}",

            // Two distances, never one: how far in it walked, and how far out
            // it could actually shoot from. Sight is the reason they differ.
            MovementScenario.Standoff =>
                $"from {r.StartDistanceCells} in to {r.ClosestApproachCells}  " +
                $"range {r.AttackRangeCells}, sight {r.SightRadiusCells}, opened fire at " +
                $"{(r.FirstContactDistanceCells < 0 ? "never" : r.FirstContactDistanceCells.ToString())} — " +
                $"nominal overshoot {r.OvershootCells}, usable {r.UsableRangeOvershootCells}",

            MovementScenario.Detour =>
                $"gap {r.WallGapCells} cells at y={r.WallGapStartCell}  " +
                $"straight {r.StraightLineCells}, travelled {r.TravelledCells}",

            _ => $"travelled {r.TravelledCells}",
        };
    }
}
