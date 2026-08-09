using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Nova.Simulation.Definitions;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Runs the whole counter-table: every combat role of both factions
    /// against every other, at three starting distances, in both directions,
    /// plus the siege echelon (plan section 3.9).
    /// <para>
    /// EVERY PAIRING RUNS BOTH WAYS. The documented duel asymmetry — on a
    /// mutual kill in the same tick the lower entity index wins — makes A-vs-B
    /// and B-vs-A two different measurements. Where they disagree, the pairing
    /// is so close that spawn order decides it. That is itself a finding and
    /// belongs in the report, not calibrated away.
    /// </para>
    /// <para>
    /// EVERY UNIT NUMBER IS FACTION-BOUND. The values are deliberately
    /// asymmetric (artillery 20/18 tiles and 110/60 damage, harvester cargo
    /// 330/300 AE for Alliance/Legion). A report that says "the artillery"
    /// without naming the faction averages two different weapons.
    /// </para>
    /// </summary>
    public static class DuelTable
    {
        /// <summary>The six combat roles, BasicInfantry (12) through Artillery (17).</summary>
        public static readonly UnitRole[] CombatRoles =
        {
            UnitRole.BasicInfantry,
            UnitRole.AntiArmorInfantry,
            UnitRole.ScoutVehicle,
            UnitRole.LightTank,
            UnitRole.BattleTank,
            UnitRole.Artillery,
        };

        /// <summary>
        /// Building roles worth besieging. The Building armour class is a whole
        /// column of the counter matrix — kinetic hits it at 30%, explosive at
        /// 75% — and "what do I tear a base down with" is half of what a weapon
        /// has to do. The DefensePlatform is the one that shoots back.
        /// </summary>
        public static readonly UnitRole[] SiegeTargets =
        {
            UnitRole.Power,
            UnitRole.Barracks,
            UnitRole.DefensePlatform,
        };

        public static readonly FactionId[] Factions = { FactionId.Alliance, FactionId.Legion };

        /// <summary>
        /// Units the expensive side of a pairing fields; the budget follows
        /// from it per pairing (see DuelArena.DeriveBudget). A single global
        /// budget does not work: at 10.000 AE a cheap pairing fielded 83 units
        /// a side, and 83-on-83 measures formation and pathfinding, not the
        /// weapon.
        /// </summary>
        public const int DefaultUnitsPerSide = 6;

        public static List<DuelSpec> Plan(int unitsPerSide, int tickBudget)
        {
            var specs = new List<DuelSpec>();

            // Unit pairings: both factions, every role against every role,
            // three distances, both directions. Ascending, fixed order — the
            // table must not depend on enumeration order anywhere.
            foreach (FactionId factionA in Factions)
            foreach (FactionId factionB in Factions)
            foreach (UnitRole roleA in CombatRoles)
            foreach (UnitRole roleB in CombatRoles)
            foreach (DuelRange range in new[] { DuelRange.Contact, DuelRange.LongestWeapon, DuelRange.OutOfSight })
            {
                specs.Add(new DuelSpec
                {
                    FactionA = factionA, FactionB = factionB,
                    RoleA = roleA, RoleB = roleB,
                    Range = range,
                    UnitsPerSide = unitsPerSide,
                    TickBudget = tickBudget,
                });
            }

            // Siege echelon: attacker units against defender buildings, at
            // contact and at weapon range. Out-of-sight adds nothing here —
            // buildings do not move, so it would only re-measure the approach.
            foreach (FactionId attacker in Factions)
            foreach (FactionId defender in Factions)
            foreach (UnitRole roleA in CombatRoles)
            foreach (UnitRole target in SiegeTargets)
            foreach (DuelRange range in new[] { DuelRange.Contact, DuelRange.LongestWeapon })
            {
                specs.Add(new DuelSpec
                {
                    FactionA = attacker, FactionB = defender,
                    RoleA = roleA, RoleB = target,
                    Range = range,
                    SiegeEchelon = true,
                    UnitsPerSide = unitsPerSide,
                    TickBudget = tickBudget,
                });
            }

            return specs;
        }

        /// <summary>Runs a plan across all cores; results stay in plan order.</summary>
        public static DuelResult[] Run(IReadOnlyList<DuelSpec> specs, int maxParallelism)
        {
            var results = new DuelResult[specs.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount,
            };

            Parallel.For(0, specs.Count, options, i => { results[i] = DuelArena.Run(specs[i]); });
            return results;
        }

        /// <summary>
        /// The table as NDJSON, one line per duel. Integers only; the reader
        /// (E4's report) does the comparing.
        /// </summary>
        public static string ToNdjson(DuelResult[] results)
        {
            var output = new StringBuilder(results.Length * 256);
            foreach (DuelResult r in results)
            {
                output.Append("{\"factionA\":\"").Append(r.FactionA)
                      .Append("\",\"roleA\":\"").Append(r.RoleA)
                      .Append("\",\"factionB\":\"").Append(r.FactionB)
                      .Append("\",\"roleB\":\"").Append(r.RoleB)
                      .Append("\",\"range\":\"").Append(r.Range)
                      .Append("\",\"siege\":").Append(r.SiegeEchelon ? "true" : "false")
                      .Append(",\"startDistanceCells\":").Append(r.StartDistanceCells)
                      .Append(",\"budgetAE\":").Append(r.BudgetAE)
                      .Append(",\"rejectedOrders\":").Append(r.RejectedOrders)
                      .Append(",\"countA\":").Append(r.CountA)
                      .Append(",\"countB\":").Append(r.CountB)
                      .Append(",\"spentA\":").Append(r.SpentA)
                      .Append(",\"spentB\":").Append(r.SpentB)
                      .Append(",\"remainderA\":").Append(r.RemainderA)
                      .Append(",\"remainderB\":").Append(r.RemainderB)
                      .Append(",\"parityWobbles\":").Append(r.ParityWobbles ? "true" : "false")
                      .Append(",\"noContact\":").Append(r.NoContact ? "true" : "false")
                      .Append(",\"startHealthA\":").Append(r.StartHealthA)
                      .Append(",\"startHealthB\":").Append(r.StartHealthB)
                      .Append(",\"decided\":").Append(r.Decided ? "true" : "false")
                      .Append(",\"decidedTick\":").Append(r.DecidedTick)
                      .Append(",\"winner\":").Append(r.Winner)
                      .Append(",\"survivorsA\":").Append(r.SurvivorsA)
                      .Append(",\"survivorsB\":").Append(r.SurvivorsB)
                      .Append(",\"survivingHealthA\":").Append(r.SurvivingHealthA)
                      .Append(",\"survivingHealthB\":").Append(r.SurvivingHealthB)
                      .Append(",\"finalStateHash\":\"0x")
                      .Append(r.FinalStateHash.ToString("X16", CultureInfo.InvariantCulture))
                      .Append("\"}\n");
            }
            return output.ToString();
        }

        /// <summary>
        /// Pairings whose two directions disagree — the ones so close that
        /// spawn order decides them. Section 3.9 wants these named, not
        /// smoothed over.
        /// <para>
        /// A PAIRING IS ONLY MIRRORED IF THE MIRROR IS A DIFFERENT RUN. For
        /// Alliance.LightTank against Alliance.LightTank the mirror key equals
        /// the pair key, so the old code compared a result WITH ITSELF and, for
        /// every decided one, reported "winner 0 one way, 0 the other". That
        /// was 33 of 38 reported rows — the five real findings were buried
        /// under a self-comparison that can never be consistent by
        /// construction. A self-mirrored pairing has nothing to disagree with;
        /// its spawn-order sensitivity is what the duel asymmetry says it is,
        /// not a measurement.
        /// </para>
        /// </summary>
        public static List<string> DirectionDisagreements(DuelResult[] results)
        {
            var byKey = new Dictionary<string, DuelResult>();
            foreach (DuelResult r in results)
            {
                byKey[Key(r.FactionA, r.RoleA, r.FactionB, r.RoleB, r.Range, r.SiegeEchelon)] = r;
            }

            var disagreements = new List<string>();
            var reported = new HashSet<string>();
            foreach (DuelResult r in results)
            {
                if (r.SiegeEchelon) continue; // buildings never attack back; the mirror is not the same experiment
                if (r.FactionA == r.FactionB && r.RoleA == r.RoleB) continue; // its own mirror — nothing to compare

                string mirrorKey = Key(r.FactionB, r.RoleB, r.FactionA, r.RoleA, r.Range, false);
                if (!byKey.TryGetValue(mirrorKey, out DuelResult mirror)) continue;

                // A consistent pairing has opposite winners in the two
                // directions (A wins as A, and loses when it spawns second).
                bool consistent = r.Winner == 0 ? mirror.Winner == 1 : (r.Winner == 1 ? mirror.Winner == 0 : mirror.Winner == -1);
                if (consistent) continue;

                string pair = Key(r.FactionA, r.RoleA, r.FactionB, r.RoleB, r.Range, false);
                string mirrored = mirrorKey;
                string canonical = string.CompareOrdinal(pair, mirrored) < 0 ? pair + "|" + mirrored : mirrored + "|" + pair;
                if (!reported.Add(canonical)) continue;

                disagreements.Add(
                    $"{r.FactionA}.{r.RoleA} vs {r.FactionB}.{r.RoleB} [{r.Range}]: " +
                    $"winner {r.Winner} one way, {mirror.Winner} the other — spawn order decides this pairing");
            }
            return disagreements;
        }

        private static string Key(FactionId fa, UnitRole ra, FactionId fb, UnitRole rb, DuelRange range, bool siege) =>
            $"{fa}.{ra}>{fb}.{rb}@{range}{(siege ? "#siege" : "")}";
    }
}
