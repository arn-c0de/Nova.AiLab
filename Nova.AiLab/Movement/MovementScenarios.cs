using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>The four movement scenarios of plan section 3.9 (decision 24).</summary>
    public enum MovementScenario
    {
        /// <summary>Group, open field, one target order — ticks to arrival, share arrived, spread.</summary>
        Arrival = 0,

        /// <summary>Two crossing groups, one squeezed through a gap between footprints — who blocks whom.</summary>
        Blocking = 1,

        /// <summary>Ranged units with an attack order on a standing target — do they keep their distance?</summary>
        Standoff = 2,

        /// <summary>Target behind a wall of buildings with one gap — path length against straight line.</summary>
        Detour = 3,
    }

    public sealed class MovementSpec
    {
        public ulong Seed = 0xA17E57DE57UL;
        public MovementScenario Scenario = MovementScenario.Arrival;
        public FactionId Faction = FactionId.Alliance;
        public UnitRole Role = UnitRole.BasicInfantry;
        public int GroupSize = 8;
        public int TickBudget = 1500;
        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 256;

        /// <summary>Ticks a moving unit must stand still to count as blocked.</summary>
        public int StuckTicks = 20;
    }

    public sealed class MovementResult
    {
        public MovementScenario Scenario;
        public FactionId Faction;
        public UnitRole Role;
        public int GroupSize;

        public int Arrived;
        public uint TicksToFirstArrival;
        public uint TicksToLastArrival;

        /// <summary>Largest Chebyshev distance from the target centre at the end — the spread.</summary>
        public int SpreadCells;

        /// <summary>Distance the group started at — so an approach measurement is readable as one.</summary>
        public int StartDistanceCells;

        /// <summary>Straight-line and travelled distance in cells (detour).</summary>
        public int StraightLineCells;
        public int TravelledCells;

        /// <summary>Units that stood still while moving for at least StuckTicks (blocking).</summary>
        public int BlockedUnits;
        /// <summary>Full stand-still duration of every run that reached the threshold, summed over units.</summary>
        public int BlockedTicksTotal;
        public int LongestSingleBlockTicks;

        /// <summary>
        /// The opening the wall scenarios actually left, MEASURED back out of
        /// the cost field rather than assumed from the parameters (blocking,
        /// detour). It cannot be narrower than the 3-cell building footprint,
        /// and the old code left a second, unintended opening at the top map
        /// edge — so this number is reported instead of trusted.
        /// </summary>
        public int WallGapStartCell;
        public int WallGapCells;

        /// <summary>
        /// Smallest centre distance a ranged unit actually reached (standoff).
        /// <para>
        /// READ THIS WITH <see cref="FirstContactDistanceCells"/>, never alone.
        /// The group is ordered to MOVE onto the enemy — an attack order alone
        /// moves nothing (GB-002, no attack-move), so the approach has to be
        /// commanded, exactly as the AI commands it. A closest approach of 0 is
        /// therefore first of all obedience to that order.
        /// </para>
        /// </summary>
        public int ClosestApproachCells;
        public int AttackRangeCells;

        /// <summary>Default sight radius in cells — the reason the nominal range is not the usable one.</summary>
        public int SightRadiusCells;

        /// <summary>
        /// The distance at which the target first LOST HEALTH: the range at
        /// which the weapon actually became usable. -1 when nothing was ever
        /// hit.
        /// <para>
        /// This is the number Issue 03 needs. Measured: artillery with a
        /// 20-cell gun and a 10-cell sight opens fire at 10, not at 20 — and a
        /// control run ordered to stop at 20 cells never fired a shot. "Keeps
        /// no distance" and "cannot see that far" are two different findings,
        /// and only this field separates them.
        /// </para>
        /// </summary>
        public int FirstContactDistanceCells = -1;

        /// <summary>
        /// How far inside its NOMINAL range the unit walked. Not an Issue 03
        /// verdict on its own — see <see cref="UsableRangeOvershootCells"/>.
        /// </summary>
        public int OvershootCells => AttackRangeCells - ClosestApproachCells;

        /// <summary>
        /// How far inside its USABLE range the unit walked: the distance it
        /// opened fire at, minus the distance it ended up at. This is the part
        /// that behaviour work can actually recover, because it does not ask
        /// the unit to shoot at something it cannot see.
        /// </summary>
        public int UsableRangeOvershootCells =>
            FirstContactDistanceCells < 0 ? 0 : FirstContactDistanceCells - ClosestApproachCells;

        public int RejectedOrders;
        public uint FinalTick;
        public ulong FinalStateHash;
    }

    /// <summary>
    /// One group, one order, obstacles placed as DATA rather than code (plan
    /// section 3.9). Runs in seconds, so a movement question is a loop of
    /// seconds instead of a match to evaluate.
    /// <para>
    /// Since v1.1.0 <c>Simulation/Pathfinding/</c> is ours as well — flow field
    /// and <c>CostField</c> included — so the whole way from the order to the
    /// arrival lies in our own scope, and this run mode covers it.
    /// </para>
    /// <para>
    /// <see cref="MovementScenario.Standoff"/> needs combat in the run and is
    /// therefore a mixed scenario. That is intended: keeping distance IS a
    /// combat property, not a pure movement topic.
    /// </para>
    /// </summary>
    public static class MovementScenarios
    {
        /// <summary>A unit counts as arrived within this Chebyshev distance of the target cell.</summary>
        private const int ArrivalToleranceCells = 3;

        public static MovementResult Run(MovementSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var matchSpec = new MatchSpec
            {
                Seed = spec.Seed,
                TickBudget = spec.TickBudget,
                MapWidth = spec.MapWidth,
                MapHeight = spec.MapHeight,
                EntityCapacity = spec.EntityCapacity,
                CountIntents = true,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = spec.Faction, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = Other(spec.Faction), Controller = SlotController.Scripted },
                },
            };

            MultiSlotAiHost host = MultiSlotAiHost.Build(matchSpec);
            var result = new MovementResult
            {
                Scenario = spec.Scenario,
                Faction = spec.Faction,
                Role = spec.Role,
                GroupSize = spec.GroupSize,
            };

            switch (spec.Scenario)
            {
                case MovementScenario.Arrival: RunArrival(host, spec, result); break;
                case MovementScenario.Blocking: RunBlocking(host, spec, result); break;
                case MovementScenario.Standoff: RunStandoff(host, spec, result); break;
                default: RunDetour(host, spec, result); break;
            }

            for (int i = 0; i < host.Peers.Length; i++)
            {
                if (host.Peers[i].IntentCounter != null) result.RejectedOrders += host.Peers[i].IntentCounter.Rejected;
            }
            result.FinalTick = host.Kernel.CurrentTick.Value;
            result.FinalStateHash = host.Kernel.CalculateStateHash();
            return result;
        }

        private static FactionId Other(FactionId faction) =>
            faction == FactionId.Alliance ? FactionId.Legion : FactionId.Alliance;

        // ================================================================
        // arrival — the baseline: does a group get there, and how tidily
        // ================================================================

        private static void RunArrival(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            var group = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, 20, 64);
            const int targetX = 100, targetY = 64;

            Order(host, 0, group, targetX, targetY);
            WatchUntilArrived(host, spec, group, targetX, targetY, result);
        }

        // ================================================================
        // blocking — the scenario Issue 03 is really about
        // ================================================================

        private static void RunBlocking(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            // A gap between two footprints: the big group has to file through
            // it while a second group crosses their path. The bottleneck has to
            // actually BE one, or "no blocking" is a property of the scenario
            // rather than of the movement code — so the opening is measured
            // back out of the cost field and reported with the row.
            //
            // The narrowest opening a 3x3 footprint can leave here is two
            // cells. Asking for one does not produce one, and the earlier
            // comment claiming a one-cell bottleneck was describing a
            // three-cell one.
            PlaceWall(host, 1, spec.MapWidth, spec.MapHeight, columnX: 64, gapY: 64, gapHeight: 1,
                out int gapStart, out int gapCells);
            result.WallGapStartCell = gapStart;
            result.WallGapCells = gapCells;

            // The main group is deliberately larger than the caller's default:
            // a queue needs a crowd.
            int mainSize = Math.Max(spec.GroupSize, 16);
            result.GroupSize = mainSize;
            var main = SpawnGroup(host, 0, spec.Faction, spec.Role, mainSize, 50, 64);
            var crossing = SpawnGroup(host, 0, spec.Faction, spec.Role, Math.Max(4, spec.GroupSize / 2), 55, 54);

            const int targetX = 100, targetY = 64;
            Order(host, 0, main, targetX, targetY);
            // Straight across the main group's path to the gap.
            Order(host, 0, crossing, 55, 74);

            var all = new List<uint>(main);
            all.AddRange(crossing);
            all.Sort();

            WatchBlocking(host, spec, all, main, targetX, targetY, result);
            MeasureArrival(host, main, targetX, targetY, result);
        }

        // ================================================================
        // standoff — does a ranged unit stop at its own range?
        // ================================================================

        private static void RunStandoff(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            if (!SimDefinitions.TryGetUnit(spec.Faction, spec.Role, out SimUnitDefinition def))
            {
                throw new ArgumentException($"unknown unit definition ({spec.Faction}, {spec.Role})");
            }
            result.AttackRangeCells = def.AttackRangeTiles;
            // The nominal range is not the usable one, and that gap is the
            // whole reason this scenario needs two numbers instead of one.
            result.SightRadiusCells = UnitState.DefaultSightRadius.Floor();

            // THE SHOOTERS MUST START WELL OUTSIDE THEIR OWN RANGE, otherwise
            // the "closest approach" is just the spawn distance and the whole
            // scenario measures nothing. The first version spawned them 8 cells
            // from a 20-cell-range target and dutifully reported 8.
            int startDistance = Math.Max(30, def.AttackRangeTiles * 2);
            int targetX = 64;
            int shooterX = targetX - startDistance;
            result.StartDistanceCells = startDistance;

            var shooters = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, shooterX, 64);

            // A standing target of the opposing slot. Huge health, so the
            // measurement is the approach and not how fast the target dies.
            if (!SimDefinitions.TryGetUnit(Other(spec.Faction), UnitRole.BasicInfantry, out SimUnitDefinition targetDef))
            {
                throw new ArgumentException($"unknown unit definition ({Other(spec.Faction)}, BasicInfantry)");
            }
            EntityId target = host.Entities.SpawnUnit(
                1, new Transform2D(SimFixed.FromInt(targetX), SimFixed.FromInt(64)),
                targetDef.MoveSpeed, maxHealth: 100000, role: targetDef.Role);

            uint targetRaw = UnitCommandStateView.ToRawEntityId(target);
            SlotPeer peer = host.PeerOf(0);

            // A MOVE order onto the target, plus the attack order. Attack alone
            // does not close the distance — measured: artillery given only
            // AttackTarget on a target 40 cells away never moved a single cell
            // and never fired. That is GB-002 in practice (no attack-move), and
            // it means the approach has to be ordered explicitly, exactly as
            // the AI does it.
            //
            // WHAT THIS DOES AND DOES NOT MEASURE — the distinction the first
            // version of this scenario got wrong. The group is ordered onto the
            // target's own cell, so a closest approach of 0 is in the first
            // place a unit obeying its order; it is NOT by itself proof that
            // ranged units fail to hold distance. A control run settles it:
            // ordered to stop at exactly 20 cells, the same artillery reached
            // 19 cells and did ZERO damage over 2000 ticks, because sight is 10
            // cells and CombatSystem needs the target Visible. So the naive
            // reading — "stop at weapon range" — would make the gun useless.
            //
            // Hence two numbers: ClosestApproachCells (how far in it walked)
            // and FirstContactDistanceCells (where it could actually shoot
            // from). The second is what behaviour work can move.
            Order(host, 0, shooters, targetX, 64);
            peer.Ingress.TrySubmitIntent(
                CommandIntent.Create(new AttackTargetPayload(shooters.ToArray(), targetRaw)), out _);

            // The closest approach is a running minimum: the interesting moment
            // is the deepest point, not where the unit happens to end up.
            //
            // The SECOND number is the one that carries Issue 03: the distance
            // at which the target first loses health. That is the range the
            // weapon is actually usable at, and it is not the range on the
            // definition — sight is 10 cells, the gun reaches 20. Without it
            // the row says "walked in to 0, wasted 20 cells of range", which
            // reads like a movement defect and is half a vision one.
            result.ClosestApproachCells = int.MaxValue;
            int previousTargetHealth = -1;

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();
                if (!host.Entities.TryGetUnit(target, out UnitState targetState) || !targetState.IsActive) break;

                int targetCellX = SimFixed.WorldToGrid(targetState.Transform.PositionX);
                int targetCellY = SimFixed.WorldToGrid(targetState.Transform.PositionY);

                int nearestThisTick = int.MaxValue;
                foreach (uint raw in shooters)
                {
                    if (!TryReadUnit(host, raw, out UnitState shooter)) continue;
                    int distance = Chebyshev(
                        SimFixed.WorldToGrid(shooter.Transform.PositionX),
                        SimFixed.WorldToGrid(shooter.Transform.PositionY),
                        targetCellX, targetCellY);
                    if (distance < nearestThisTick) nearestThisTick = distance;
                }
                if (nearestThisTick < result.ClosestApproachCells) result.ClosestApproachCells = nearestThisTick;

                if (previousTargetHealth >= 0
                    && targetState.CurrentHealth < previousTargetHealth
                    && result.FirstContactDistanceCells < 0
                    && nearestThisTick != int.MaxValue)
                {
                    result.FirstContactDistanceCells = nearestThisTick;
                }
                previousTargetHealth = targetState.CurrentHealth;
            }

            if (result.ClosestApproachCells == int.MaxValue) result.ClosestApproachCells = 0;
        }

        // ================================================================
        // detour — flow field and CostField, directly
        // ================================================================

        private static void RunDetour(MultiSlotAiHost host, MovementSpec spec, MovementResult result)
        {
            PlaceWall(host, 1, spec.MapWidth, spec.MapHeight, columnX: 64, gapY: 20, gapHeight: 3,
                out int gapStart, out int gapCells);
            result.WallGapStartCell = gapStart;
            result.WallGapCells = gapCells;

            var group = SpawnGroup(host, 0, spec.Faction, spec.Role, spec.GroupSize, 40, 100);
            const int targetX = 90, targetY = 100;

            result.StraightLineCells = Chebyshev(40, 100, targetX, targetY);
            Order(host, 0, group, targetX, targetY);
            WatchUntilArrived(host, spec, group, targetX, targetY, result);
        }

        // ================================================================
        // Shared measurement
        // ================================================================

        private static void WatchUntilArrived(MultiSlotAiHost host, MovementSpec spec, List<uint> group,
            int targetX, int targetY, MovementResult result)
        {
            var lastCell = new Dictionary<uint, (int X, int Y)>();
            long travelled = 0;

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();

                foreach (uint raw in group)
                {
                    if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                    int x = SimFixed.WorldToGrid(unit.Transform.PositionX);
                    int y = SimFixed.WorldToGrid(unit.Transform.PositionY);

                    if (lastCell.TryGetValue(raw, out (int X, int Y) previous))
                    {
                        travelled += Chebyshev(previous.X, previous.Y, x, y);
                    }
                    lastCell[raw] = (x, y);
                }

                int arrived = CountArrived(host, group, targetX, targetY);
                if (arrived > 0 && result.TicksToFirstArrival == 0)
                {
                    result.TicksToFirstArrival = host.Kernel.CurrentTick.Value;
                }
                if (arrived == group.Count)
                {
                    result.TicksToLastArrival = host.Kernel.CurrentTick.Value;
                    break;
                }
            }

            result.TravelledCells = (int)(group.Count > 0 ? travelled / group.Count : 0);
            MeasureArrival(host, group, targetX, targetY, result);
        }

        private static void MeasureArrival(MultiSlotAiHost host, List<uint> group, int targetX, int targetY,
            MovementResult result)
        {
            result.Arrived = CountArrived(host, group, targetX, targetY);

            int spread = 0;
            foreach (uint raw in group)
            {
                if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                int distance = Chebyshev(
                    SimFixed.WorldToGrid(unit.Transform.PositionX),
                    SimFixed.WorldToGrid(unit.Transform.PositionY),
                    targetX, targetY);
                if (distance > spread) spread = distance;
            }
            result.SpreadCells = spread;
        }

        /// <summary>
        /// Counts units that claim to be moving while their cell does not
        /// change — the operational definition of "blocked" from section 3.9.
        /// </summary>
        private static void WatchBlocking(MultiSlotAiHost host, MovementSpec spec, List<uint> group,
            List<uint> arrivalGroup, int targetX, int targetY, MovementResult result)
        {
            var lastCell = new Dictionary<uint, (int X, int Y)>();
            var stillFor = new Dictionary<uint, int>();
            var everBlocked = new HashSet<uint>();

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();

                foreach (uint raw in group)
                {
                    if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                    int x = SimFixed.WorldToGrid(unit.Transform.PositionX);
                    int y = SimFixed.WorldToGrid(unit.Transform.PositionY);

                    bool sameCell = lastCell.TryGetValue(raw, out (int X, int Y) previous)
                                    && previous.X == x && previous.Y == y;
                    lastCell[raw] = (x, y);

                    if (unit.IsMoving && sameCell)
                    {
                        int run = stillFor.TryGetValue(raw, out int held) ? held + 1 : 1;
                        stillFor[raw] = run;
                        if (run >= spec.StuckTicks)
                        {
                            everBlocked.Add(raw);
                            // The FULL stand-still duration, not only the ticks
                            // past the threshold: the plan asks for the total
                            // duration, and counting from the threshold onward
                            // silently drops the first StuckTicks of every
                            // blockage.
                            result.BlockedTicksTotal += run == spec.StuckTicks ? spec.StuckTicks : 1;
                            if (run > result.LongestSingleBlockTicks) result.LongestSingleBlockTicks = run;
                        }
                    }
                    else
                    {
                        stillFor[raw] = 0;
                    }
                }

                // Arrival time is the readable half of this scenario: 16 units
                // through a one-cell gap against 8 across open ground is the
                // comparison that says whether the bottleneck cost anything.
                int arrived = CountArrived(host, arrivalGroup, targetX, targetY);
                if (arrived > 0 && result.TicksToFirstArrival == 0)
                {
                    result.TicksToFirstArrival = host.Kernel.CurrentTick.Value;
                }
                if (arrived == arrivalGroup.Count && result.TicksToLastArrival == 0)
                {
                    result.TicksToLastArrival = host.Kernel.CurrentTick.Value;
                }
            }

            result.BlockedUnits = everBlocked.Count;
        }

        private static int CountArrived(MultiSlotAiHost host, List<uint> group, int targetX, int targetY)
        {
            int arrived = 0;
            foreach (uint raw in group)
            {
                if (!TryReadUnit(host, raw, out UnitState unit)) continue;
                if (Chebyshev(SimFixed.WorldToGrid(unit.Transform.PositionX),
                              SimFixed.WorldToGrid(unit.Transform.PositionY),
                              targetX, targetY) <= ArrivalToleranceCells)
                {
                    arrived++;
                }
            }
            return arrived;
        }

        // ================================================================
        // Setup helpers — obstacles are data, not code
        // ================================================================

        private static List<uint> SpawnGroup(MultiSlotAiHost host, byte slot, FactionId faction, UnitRole role,
            int count, int originX, int originY)
        {
            if (!SimDefinitions.TryGetUnit(faction, role, out SimUnitDefinition def))
            {
                throw new ArgumentException($"unknown unit definition ({faction}, {role})");
            }

            var raws = new List<uint>(count);
            for (int i = 0; i < count; i++)
            {
                EntityId id = host.Entities.SpawnUnit(
                    slot,
                    new Transform2D(SimFixed.FromInt(originX - i / 5), SimFixed.FromInt(originY - 2 + i % 5)),
                    def.MoveSpeed, maxHealth: def.MaxHealth, role: def.Role);
                raws.Add(UnitCommandStateView.ToRawEntityId(id));
            }
            raws.Sort();
            return raws;
        }

        /// <summary>
        /// A wall of completed buildings across the whole map with exactly ONE
        /// opening. Footprints have been impassable since the troop-command
        /// sprint, so this is a real obstacle for the cost field — no special
        /// terrain needed.
        /// <para>
        /// THE WALL SPANS THE WHOLE MAP. A partial wall is not a bottleneck:
        /// the first version was 60 cells tall on a 128-cell map and the group
        /// simply walked around it, which is why "blocking" reported zero
        /// blocked units — a property of the scenario, not of the movement
        /// code. Two later versions of the same mistake, both silent:
        /// </para>
        /// <list type="number">
        /// <item><b>A second opening at the top edge.</b> The old loop ran
        /// <c>y &lt;= mapHeight - step</c> in 3-cell strides. 128 is not a
        /// multiple of 3, so rows 126 and 127 stayed walkable. The detour group
        /// walked through THAT, not through the intended gap: 84 cells
        /// travelled against a 50-cell straight line, where the intended gap
        /// costs more than 160. The scenario measured an opening nobody
        /// designed.</item>
        /// <item><b>An opening narrower than the lattice allows.</b>
        /// <c>gapHeight: 1</c> produced a three-cell opening, because skipping
        /// one 3x3 footprint frees three rows. The comment claiming a one-cell
        /// bottleneck described something that was never built.</item>
        /// </list>
        /// <para>
        /// So: tile from BOTH map edges toward the gap, check every placement
        /// verdict, and then MEASURE the opening back out of the cost field.
        /// The caller is told the gap that exists, not the gap that was asked
        /// for — and a layout that would leave two openings fails loudly here
        /// instead of quietly producing a number.
        /// </para>
        /// </summary>
        private static void PlaceWall(MultiSlotAiHost host, byte slot, int mapWidth, int mapHeight,
            int columnX, int gapY, int gapHeight, out int gapStart, out int gapCells)
        {
            // The definition follows the SLOT's faction, not the caller's: the
            // wall belongs to the opposing slot, and handing it the other
            // faction's building was an inconsistency waiting to matter.
            FactionId faction = host.Economy.GetSlotFaction(slot);
            ushort defId = SimDefinitions.ToDefinitionId(faction, UnitRole.Power);
            int step = SimDefinitions.BuildingFootprintCells;
            int gapEnd = gapY + gapHeight;

            // Below the gap, upward from the bottom edge.
            for (int y = 0; y + step <= gapY; y += step) PlaceWallBlock(host, slot, defId, columnX, y);
            // Above the gap, upward from the first row past it — this is what
            // closes the top edge that the old stride left open.
            for (int y = gapEnd; y + step <= mapHeight; y += step) PlaceWallBlock(host, slot, defId, columnX, y);

            MeasureOpening(host, columnX, mapHeight, out gapStart, out gapCells);
        }

        /// <summary>
        /// One wall block, with its verdict checked. <c>PlaceCompletedBuilding</c>
        /// returns <c>EntityId.Invalid</c> on an occupied footprint, an
        /// off-map origin or an exhausted placement table
        /// (<c>MaxBuildings = 256</c>) — and a wall with a silent hole in it
        /// turns "nobody was blocked" into a property of the scenario.
        /// </summary>
        private static void PlaceWallBlock(MultiSlotAiHost host, byte slot, ushort defId, int x, int y)
        {
            if (host.Construction.PlaceCompletedBuilding(slot, defId, x, y).IsValid) return;
            throw new InvalidOperationException(
                $"[AiLab] wall block at ({x},{y}) was refused — the wall would have a hole in it, " +
                "and the scenario would measure the hole instead of the movement code");
        }

        /// <summary>
        /// Reads the wall column back out of the cost field and insists on
        /// exactly one contiguous opening. Asserting the geometry is the whole
        /// point: both wall bugs this scenario had were invisible in the
        /// results and visible immediately in the walkability map.
        /// </summary>
        private static void MeasureOpening(MultiSlotAiHost host, int columnX, int mapHeight,
            out int gapStart, out int gapCells)
        {
            gapStart = -1;
            gapCells = 0;
            int openings = 0;
            bool inRun = false;

            for (int y = 0; y < mapHeight; y++)
            {
                bool walkable = host.Pathfinding.CostField.IsWalkable((ushort)columnX, (ushort)y);
                if (walkable && !inRun)
                {
                    openings++;
                    if (openings == 1) gapStart = y;
                    inRun = true;
                }
                else if (!walkable)
                {
                    inRun = false;
                }
                if (walkable && openings == 1) gapCells++;
            }

            if (openings != 1)
            {
                throw new InvalidOperationException(
                    $"[AiLab] the wall at x={columnX} has {openings} openings, not 1 — " +
                    "a second opening makes the scenario measure a hole nobody designed " +
                    "(this is exactly how the detour run walked past its own gap)");
            }
        }

        /// <summary>
        /// The scenario's move order, through <see cref="MoveOrders"/> like the
        /// arena's.
        /// <para>
        /// The refusal count it returns is deliberately dropped here:
        /// <see cref="Run"/> reads <c>RejectedOrders</c> off the counting
        /// transports once at the end, which catches every refusal in the run
        /// and not only the ones this method caused. A per-call sum beside it
        /// would count the same refusals twice.
        /// </para>
        /// </summary>
        private static void Order(MultiSlotAiHost host, byte slot, List<uint> raws, int targetX, int targetY) =>
            MoveOrders.Submit(host, slot, raws, targetX, targetY);

        private static bool TryReadUnit(MultiSlotAiHost host, uint raw, out UnitState unit)
        {
            EntityId id = UnitCommandStateView.ToEntityId(raw);
            return host.Entities.TryGetUnit(id, out unit) && unit.IsActive;
        }

        private static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        // ================================================================

        public static string ToNdjson(IReadOnlyList<MovementResult> results)
        {
            var output = new StringBuilder(results.Count * 256);
            foreach (MovementResult r in results)
            {
                output.Append("{\"scenario\":\"").Append(r.Scenario)
                      .Append("\",\"faction\":\"").Append(r.Faction)
                      .Append("\",\"role\":\"").Append(r.Role)
                      .Append("\",\"groupSize\":").Append(r.GroupSize)
                      .Append(",\"arrived\":").Append(r.Arrived)
                      .Append(",\"ticksToFirstArrival\":").Append(r.TicksToFirstArrival)
                      .Append(",\"ticksToLastArrival\":").Append(r.TicksToLastArrival)
                      .Append(",\"spreadCells\":").Append(r.SpreadCells)
                      .Append(",\"startDistanceCells\":").Append(r.StartDistanceCells)
                      .Append(",\"straightLineCells\":").Append(r.StraightLineCells)
                      .Append(",\"travelledCells\":").Append(r.TravelledCells)
                      .Append(",\"blockedUnits\":").Append(r.BlockedUnits)
                      .Append(",\"blockedTicksTotal\":").Append(r.BlockedTicksTotal)
                      .Append(",\"longestSingleBlockTicks\":").Append(r.LongestSingleBlockTicks)
                      .Append(",\"wallGapStartCell\":").Append(r.WallGapStartCell)
                      .Append(",\"wallGapCells\":").Append(r.WallGapCells)
                      .Append(",\"closestApproachCells\":").Append(r.ClosestApproachCells)
                      .Append(",\"attackRangeCells\":").Append(r.AttackRangeCells)
                      .Append(",\"sightRadiusCells\":").Append(r.SightRadiusCells)
                      .Append(",\"firstContactDistanceCells\":").Append(r.FirstContactDistanceCells)
                      .Append(",\"overshootCells\":").Append(r.OvershootCells)
                      .Append(",\"usableRangeOvershootCells\":").Append(r.UsableRangeOvershootCells)
                      .Append(",\"rejectedOrders\":").Append(r.RejectedOrders)
                      .Append(",\"finalTick\":").Append(r.FinalTick)
                      .Append(",\"finalStateHash\":\"0x")
                      .Append(r.FinalStateHash.ToString("X16", CultureInfo.InvariantCulture))
                      .Append("\"}\n");
            }
            return output.ToString();
        }
    }
}
