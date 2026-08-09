using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>The three starting distances of plan section 3.9.</summary>
    public enum DuelRange
    {
        /// <summary>Contact: damage, armour and reload decide; range plays no part.</summary>
        Contact = 0,

        /// <summary>The longest weapon range of the pairing: does the longer gun get its free shots?</summary>
        LongestWeapon = 1,

        /// <summary>Beyond any sight: approach and scouting, not just firepower.</summary>
        OutOfSight = 2,
    }

    /// <summary>One duel to run.</summary>
    public sealed class DuelSpec
    {
        public ulong Seed = 0xA17E57DE57UL;
        public FactionId FactionA = FactionId.Alliance;
        public FactionId FactionB = FactionId.Legion;
        public UnitRole RoleA = UnitRole.BasicInfantry;
        public UnitRole RoleB = UnitRole.BasicInfantry;

        /// <summary>
        /// Parity is over AE COST, not unit count (decision 20). Equal counts
        /// would be no finding at all — a tank that costs twice as much is
        /// SUPPOSED to beat an infantryman.
        /// <para>
        /// 0 means "derive it": <see cref="DuelArena.DeriveBudget"/> sizes the
        /// budget so the EXPENSIVE side fields <see cref="UnitsPerSide"/>
        /// units. A fixed global budget was tried first and was wrong — at
        /// 10.000 AE a cheap pairing fielded 83 units a side, which measures
        /// formation and pathfinding, not the weapon.
        /// </para>
        /// </summary>
        public long BudgetAE;

        /// <summary>
        /// Units the expensive side fields. The plan asks for at least four
        /// (section 3.9); six keeps a loss from swinging the result while the
        /// group stays small enough to be a duel.
        /// </summary>
        public int UnitsPerSide = 6;

        public DuelRange Range = DuelRange.Contact;

        /// <summary>Side B is a building instead of units — the siege echelon (decision 25).</summary>
        public bool SiegeEchelon;

        public int TickBudget = 3000;
        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 512;
    }

    /// <summary>What one duel measured. Integers only.</summary>
    public sealed class DuelResult
    {
        public string PairingLabel;
        public FactionId FactionA, FactionB;
        public UnitRole RoleA, RoleB;
        public DuelRange Range;
        public bool SiegeEchelon;

        public int CountA, CountB;
        public long SpentA, SpentB;
        /// <summary>Budget left over per side; above 10% the parity itself wobbles and the report marks it.</summary>
        public long RemainderA, RemainderB;

        public int SurvivorsA, SurvivorsB;
        public long SurvivingHealthA, SurvivingHealthB;
        public uint DecidedTick;
        public bool Decided;

        /// <summary>-1 nobody, 0 side A, 1 side B.</summary>
        public int Winner = -1;

        public int StartDistanceCells;
        public long BudgetAE;

        /// <summary>
        /// Health both sides started with, so "did anything happen at all" is
        /// answerable without a second run.
        /// </summary>
        public long StartHealthA, StartHealthB;

        /// <summary>
        /// Nobody took a scratch. Distinct from an undecided fight: at the
        /// weapon-range echelon this is the documented finding that a gun
        /// out-ranging its own sight (artillery 20/18 tiles against a 10-tile
        /// default sight) cannot use its range without scouting.
        /// </summary>
        public bool NoContact => SurvivingHealthA == StartHealthA && SurvivingHealthB == StartHealthB;
        public ulong FinalStateHash;

        /// <summary>
        /// Move intents the executor refused. Anything above zero means the
        /// scenario did not set up what it thinks it set up, and the row is
        /// not a measurement.
        /// </summary>
        public int RejectedOrders;

        /// <summary>Parity wobbles when a side leaves more than a tenth of its budget unspent.</summary>
        public bool ParityWobbles =>
            SpentA > 0 && SpentB > 0 &&
            (RemainderA * 10 > SpentA + RemainderA || RemainderB * 10 > SpentB + RemainderB);
    }

    /// <summary>
    /// N versus M units on an empty field (plan section 3.9, run mode
    /// <c>duel</c>): the counter-table measured instead of read off
    /// <c>DamageMatrix</c>. The matrix names the multiplier; the duel shows
    /// what range, reload and hit points make of it.
    /// <para>
    /// SAME SYSTEM REGISTRATION as a match. The arena registers economy,
    /// construction and production too — they simply tick over empty tables. A
    /// dropped system would be a different tick order and therefore a
    /// different game, and then the measurement describes something that does
    /// not exist.
    /// </para>
    /// <para>
    /// REAL FOG OF WAR, as in the game (decision 21): <c>CombatSystem</c>
    /// requires the target to be Visible in the committed team view. That
    /// artillery cannot use its range without scouting is therefore a genuine
    /// balance finding, not a measurement error.
    /// </para>
    /// <para>
    /// Orders travel the canonical sealed command path through a scripted
    /// slot's own session — the same path a human's orders take.
    /// </para>
    /// </summary>
    public static class DuelArena
    {
        /// <summary>Rows the spawn column wraps at, so a large group is a block rather than a line off the map.</summary>
        private const int MaxRows = 12;

        /// <summary>Out-of-sight distance: beyond the 10-tile default sight and beyond every weapon (Alliance artillery 20).</summary>
        private const int OutOfSightCells = 34;

        public static DuelResult Run(DuelSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var matchSpec = new MatchSpec
            {
                Seed = spec.Seed,
                TickBudget = spec.TickBudget,
                MapWidth = spec.MapWidth,
                MapHeight = spec.MapHeight,
                EntityCapacity = spec.EntityCapacity,
                // The arena is a measuring rig: it wants the real host verdict
                // on every order. TrySubmitIntent returns the PEER ingress's
                // verdict, which is Accepted regardless of what the host made
                // of the record — only the transport sees the intake result.
                CountIntents = true,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = spec.FactionA, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = spec.FactionB, Controller = SlotController.Scripted },
                },
            };

            MultiSlotAiHost host = MultiSlotAiHost.Build(matchSpec);

            var result = new DuelResult
            {
                FactionA = spec.FactionA, FactionB = spec.FactionB,
                RoleA = spec.RoleA, RoleB = spec.RoleB,
                Range = spec.Range, SiegeEchelon = spec.SiegeEchelon,
                PairingLabel = $"{spec.FactionA}.{spec.RoleA} vs {spec.FactionB}.{spec.RoleB}" +
                               $" [{spec.Range}{(spec.SiegeEchelon ? ", siege" : "")}]",
            };

            if (!SimDefinitions.TryGetUnit(spec.FactionA, spec.RoleA, out SimUnitDefinition defA))
            {
                throw new ArgumentException($"unknown unit definition ({spec.FactionA}, {spec.RoleA})");
            }

            long budget = spec.BudgetAE > 0 ? spec.BudgetAE : DeriveBudget(spec);
            result.BudgetAE = budget;

            int distance = StartDistance(spec, defA);
            result.StartDistanceCells = distance;

            int centreY = spec.MapHeight / 2;
            int xA = spec.MapWidth / 2 - distance / 2;
            int xB = spec.MapWidth / 2 + distance / 2;

            // Spawn order is load-bearing: entity ids come from a deterministic
            // free list, and the documented duel asymmetry (on a mutual kill in
            // the same tick the LOWER entity index wins) makes A-vs-B and
            // B-vs-A two different measurements. Side A always spawns first;
            // the caller runs the mirrored pairing to see the other direction.
            var unitsA = new List<uint>();
            result.CountA = SpawnSide(host, 0, spec.FactionA, spec.RoleA, budget, xA, centreY,
                out result.SpentA, out result.RemainderA, unitsA);

            var unitsB = new List<uint>();
            if (spec.SiegeEchelon)
            {
                // THE SIEGE ECHELON IS NOT AN AE-PARITY EXPERIMENT. The plan
                // measures it differently (section 3.9): ticks to demolition,
                // AE spent against building cost, and for the DefensePlatform
                // the attacker's losses on top. So the target is ONE building,
                // always — sizing it by budget made the building count swing
                // from 6 to 12 across the table and the demolition times
                // incomparable.
                result.CountB = SpawnBuildings(host, 1, spec.FactionB, spec.RoleB, xB, centreY,
                    out result.SpentB);
                result.RemainderB = 0;
            }
            else
            {
                result.CountB = SpawnSide(host, 1, spec.FactionB, spec.RoleB, budget, xB, centreY,
                    out result.SpentB, out result.RemainderB, unitsB);
            }

            // On the long echelon nothing happens without a move order:
            // auto-acquisition (D-087) only reaches VISIBLE targets in range,
            // so both sides would stand still until the budget runs out. The
            // long echelon therefore measures approach behaviour as well —
            // intended, but it belongs in the reading of the numbers.
            if (spec.Range == DuelRange.OutOfSight)
            {
                // BOTH SIDES WALK TO THE MIDPOINT, not onto the other's start
                // cell. Sent to each other's position they walk THROUGH one
                // another and end up as far apart as they began — the first
                // duel table measured exactly that and reported 144 of 144
                // long-echelon duels as "undecided" with nobody scratched.
                // Against a building the attacker still walks all the way in:
                // the target does not move.
                int meetX = spec.SiegeEchelon ? xB : (xA + xB) / 2;
                result.RejectedOrders += MoveOrders.Submit(host, 0, unitsA, meetX, centreY);
                if (!spec.SiegeEchelon) result.RejectedOrders += MoveOrders.Submit(host, 1, unitsB, meetX, centreY);
            }

            CountSide(host, 0, out _, out result.StartHealthA);
            CountSide(host, 1, out _, out result.StartHealthB);

            RunUntilOneSideIsGone(host, spec, result);
            result.FinalStateHash = host.Kernel.CalculateStateHash();
            return result;
        }

        private static int StartDistance(DuelSpec spec, in SimUnitDefinition defA)
        {
            switch (spec.Range)
            {
                case DuelRange.Contact:
                    return 2;

                case DuelRange.LongestWeapon:
                    int rangeA = defA.AttackRangeTiles;
                    int rangeB = 0;
                    if (spec.SiegeEchelon)
                    {
                        if (SimDefinitions.TryGetBuilding(spec.FactionB, spec.RoleB, out SimBuildingDefinition b))
                        {
                            rangeB = b.AttackRangeTiles;
                        }
                    }
                    else if (SimDefinitions.TryGetUnit(spec.FactionB, spec.RoleB, out SimUnitDefinition u))
                    {
                        rangeB = u.AttackRangeTiles;
                    }
                    return Math.Max(2, Math.Max(rangeA, rangeB));

                default:
                    return OutOfSightCells;
            }
        }

        /// <summary>
        /// Spawns as many units as the budget buys, in a deterministic block.
        /// Returns the count; the unspent remainder is reported, never hidden —
        /// a pairing that cannot spend its budget evenly is a pairing whose
        /// parity wobbles, and the reader has to know.
        /// </summary>
        private static int SpawnSide(MultiSlotAiHost host, byte slot, FactionId faction, UnitRole role,
            long budgetAE, int x, int centreY, out long spent, out long remainder, List<uint> raws)
        {
            if (!SimDefinitions.TryGetUnit(faction, role, out SimUnitDefinition def) || def.CostAE <= 0)
            {
                throw new ArgumentException($"unknown or free unit definition ({faction}, {role})");
            }

            int count = (int)(budgetAE / def.CostAE);
            spent = count * def.CostAE;
            remainder = budgetAE - spent;

            for (int i = 0; i < count; i++)
            {
                int row = i % MaxRows;
                int column = i / MaxRows;
                int cellX = slot == 0 ? x - column : x + column;
                int cellY = centreY - MaxRows / 2 + row;

                EntityId id = host.Entities.SpawnUnit(
                    slot,
                    new Transform2D(SimFixed.FromInt(cellX), SimFixed.FromInt(cellY)),
                    def.MoveSpeed,
                    maxHealth: def.MaxHealth,
                    role: def.Role);
                raws.Add(UnitCommandStateView.ToRawEntityId(id));
            }

            raws.Sort();
            return count;
        }

        /// <summary>
        /// The siege echelon: side B is buildings. They do not shoot back —
        /// the DefensePlatform is the single exception — so what is measured is
        /// ticks to demolition and AE spent against building cost.
        /// </summary>
        private static int SpawnBuildings(MultiSlotAiHost host, byte slot, FactionId faction, UnitRole role,
            int x, int centreY, out long spent)
        {
            if (!SimDefinitions.TryGetBuilding(faction, role, out SimBuildingDefinition def) || def.CostAE <= 0)
            {
                throw new ArgumentException($"unknown or free building definition ({faction}, {role})");
            }

            bool placed = host.Construction.PlaceCompletedBuilding(slot, def.DefinitionId, x, centreY).IsValid;
            spent = placed ? def.CostAE : 0;
            return placed ? 1 : 0;
        }

        /// <summary>
        /// Budget sized so the EXPENSIVE side fields
        /// <see cref="DuelSpec.UnitsPerSide"/> units — the cheap side then
        /// fields as many as the same AE buys, which IS the parity the plan
        /// asks for (decision 20).
        /// </summary>
        public static long DeriveBudget(DuelSpec spec)
        {
            long costA = UnitCost(spec.FactionA, spec.RoleA);
            if (spec.SiegeEchelon)
            {
                // The attacker simply fields UnitsPerSide of its role; the
                // defender is one building and buys nothing.
                return costA * Math.Max(1, spec.UnitsPerSide);
            }

            long costB = UnitCost(spec.FactionB, spec.RoleB);
            return Math.Max(costA, costB) * Math.Max(1, spec.UnitsPerSide);
        }

        private static long UnitCost(FactionId faction, UnitRole role) =>
            SimDefinitions.TryGetUnit(faction, role, out SimUnitDefinition u) ? u.CostAE : 0;

        /// <summary>
        /// Runs until one side owns nothing living or the budget runs out.
        /// The victory system ticks along in its canonical position but does
        /// NOT decide here: without an HQ its elimination rule judges a
        /// situation the arena never sets up.
        /// </summary>
        private static void RunUntilOneSideIsGone(MultiSlotAiHost host, DuelSpec spec, DuelResult result)
        {
            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();

                CountSide(host, 0, out int aliveA, out long healthA);
                CountSide(host, 1, out int aliveB, out long healthB);
                if (aliveA != 0 && aliveB != 0) continue;

                result.Decided = true;
                result.DecidedTick = host.Kernel.CurrentTick.Value;
                result.Winner = aliveA > 0 ? 0 : (aliveB > 0 ? 1 : -1);
                break;
            }

            CountSide(host, 0, out result.SurvivorsA, out result.SurvivingHealthA);
            CountSide(host, 1, out result.SurvivorsB, out result.SurvivingHealthB);
            if (!result.Decided) result.DecidedTick = host.Kernel.CurrentTick.Value;
        }

        private static void CountSide(MultiSlotAiHost host, byte slot, out int alive, out long health)
        {
            alive = 0;
            health = 0;
            UnitState[] units = host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                if (!u.IsActive || u.PlayerId != slot) continue;
                alive++;
                health += u.CurrentHealth;
            }
        }
    }
}
