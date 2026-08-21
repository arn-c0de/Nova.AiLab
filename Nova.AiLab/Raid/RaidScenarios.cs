using System;
using System.Collections.Generic;
using System.Text;
using Nova.AI;
using Nova.AI.Data;
using Nova.Core;
using Nova.Simulation.Combat;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>Which of the AI's fields the raider drives to.</summary>
    public enum RaidField
    {
        /// <summary>
        /// The seat's own start field, two cells off its HQ. Inside
        /// <c>DefendHomeCells</c> — the interesting question here is not the
        /// anchor but whether the rule is allowed to speak.
        /// </summary>
        Start = 0,

        /// <summary>
        /// A registered expansion field far outside the home radius — the case
        /// that only exists since the fields became finite (#80/#97) and the
        /// harvester has a distance to travel.
        /// </summary>
        Expansion = 1,
    }

    public sealed class RaidSpec
    {
        public ulong Seed = 0xA17E57DE57UL;
        public RaidField Field = RaidField.Start;

        /// <summary>The seat under attack. Slot 1 is the top-right seat of the canonical opening.</summary>
        public byte AiSlot = 1;

        /// <summary>The raiding seat. Scripted — one unit, one order, nothing decides on its own.</summary>
        public byte RaiderSlot = 0;

        public FactionId AiFaction = FactionId.Legion;
        public FactionId RaiderFaction = FactionId.Alliance;

        public AiProfile Profile = AiProfiles.Ms1Canonical;
        public string ProfileId = SlotSpec.CanonicalProfileId;

        /// <summary>How many combat units the defending seat has standing about.</summary>
        public int GuardCount = 6;

        /// <summary>
        /// Chebyshev distance between those units and the raided cell — THE
        /// AXIS of this scenario. Sweeping it is what separates "out of range"
        /// from "no rule": at 2 cells every weapon reaches, at 20 none does,
        /// and the distance where the reaction stops is the answer.
        /// </summary>
        public int GuardDistanceCells = 4;

        public UnitRole GuardRole = UnitRole.BasicInfantry;
        public UnitRole RaiderRole = UnitRole.BattleTank;

        /// <summary>
        /// Ticks the seat is left alone BEFORE the raider appears.
        /// <para>
        /// The ingredient the first version of this scenario was missing. With
        /// no delay the defending army is idle and the raider is the only thing
        /// on the map worth shooting at, so the wave walks over to it and the
        /// run reports a reaction that has nothing to do with defending
        /// anything. With a delay the seat first does what it does in a match —
        /// gather and march on the enemy base — and the raid then meets the
        /// position the played observation describes: units that WERE nearby
        /// and are now somewhere else.
        /// </para>
        /// </summary>
        public int RaidDelayTicks;

        public int TickBudget = 1200;
        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 512;

        /// <summary>Field id for the expansion variant — 1..4 belong to the corner seats.</summary>
        public ushort ExpansionFieldId = 5;

        /// <summary>
        /// The game's own expansion for the top-right base
        /// (<c>MatchBootstrap.FieldLayouts</c> id 4). Far enough from the HQ
        /// that <c>DefendHomeCells</c> cannot cover it.
        /// </summary>
        public int ExpansionX = 100;
        public int ExpansionY = 84;
    }

    public sealed class RaidResult
    {
        public RaidField Field;
        public int GuardCount;
        public int GuardDistanceCells;
        public UnitRole GuardRole;
        public int GuardRangeCells;
        public string ProfileId;

        public int RaidCellX, RaidCellY;
        public int HqCellX, HqCellY;

        /// <summary>Chebyshev HQ centre to raided cell — the number DefendHomeCells is compared against.</summary>
        public int RaidToHomeCells;
        public int DefendHomeCells;

        /// <summary>First tick the raider lost health: somebody fired back. -1 = never.</summary>
        public int ReturnFireTick = -1;

        /// <summary>First tick any of the seat's units was put under DefendHome. -1 = never.</summary>
        public int DefendHomeTick = -1;

        /// <summary>Was the home-threat flag ever up while the raid ran?</summary>
        public bool HomeThreatenedSeen;

        /// <summary>Largest number of units the seat had committed to a wave during the raid.</summary>
        public int MaxCommitted;

        /// <summary>Closest any guard ever came to the raider, in cells.</summary>
        public int ClosestGuardApproachCells = int.MaxValue;

        /// <summary>Ticks the seat was left alone before the raid began.</summary>
        public int RaidDelayTicks;

        /// <summary>
        /// Where the nearest guard actually stood when the raider appeared —
        /// which is the spawn distance only when nothing happened in between.
        /// </summary>
        public int GuardDistanceAtRaidStart = -1;

        /// <summary>How many guards were still alive when the raid began.</summary>
        public int GuardsAliveAtRaidStart;

        /// <summary>
        /// Ticks in which at least one guard stood inside its own weapon range
        /// of the raider — measured with <c>CombatSystem</c>'s own test, not
        /// with a cell count beside it. The two disagree on the diagonal: five
        /// cells across and five up is seven cells of distance, and a rifle
        /// that reaches six does not fire. Reading that as "in range and
        /// silent" invents a defect.
        /// </summary>
        public int GuardTicksInRange;

        /// <summary>Ticks in which the raider's cell was Visible to the defending team.</summary>
        public int RaiderVisibleTicks;

        /// <summary>Decisions in which a guard inside weapon range carried an attack order on something else.</summary>
        public int GuardDecisionsWithStandingOrder;

        /// <summary>Decisions in which a guard inside weapon range carried NO attack order at all.</summary>
        public int GuardDecisionsFree;

        public int HarvesterStartHealth;
        public int HarvesterEndHealth;
        public bool HarvesterDied;
        public int HarvesterDeathTick = -1;

        /// <summary>Did the harvester ever actually load cargo — otherwise it only stood there.</summary>
        public bool HarvesterHarvested;

        public int RaiderStartHealth;
        public int RaiderEndHealth;
        public bool RaiderDied;

        public int GuardsLost;
        public int RejectedOrders;

        public uint FinalTick;
        public ulong FinalStateHash;

        /// <summary>The reading, in the vocabulary of issue #101 — see <see cref="RaidScenarios"/>.</summary>
        public string Verdict = "";
    }

    /// <summary>
    /// The field raid: one enemy unit shoots the AI's harvester at one of its
    /// fields, a number of the AI's own combat units stand a settable distance
    /// away, and the run records whether anything comes back.
    /// <para>
    /// WHY IT EXISTS. Issue #101 ("KI verteidigt weder ihre Harvester noch den
    /// Sammelplatz") asks a question the canonical match cannot answer: was the
    /// nearest own unit OUT OF RANGE, or did it carry a STANDING ATTACK ORDER
    /// from an earlier wave? Both switch off the auto-acquisition in
    /// <c>CombatSystem</c>, and they need different fixes — the first needs
    /// movement, the second only a targeting rule. Measured on an ordinary
    /// AI-vs-AI match the situation barely occurs, and when it does the seat is
    /// already beaten: in the run that produced it (<c>hq-weight-1</c> against
    /// <c>ms1-canonical</c>) the defending seat had exactly ONE combat unit
    /// left when its harvesters came under fire. A sample of one, taken from a
    /// rout, is not an answer.
    /// </para>
    /// <para>
    /// So the situation is built instead of waited for, and the distance is a
    /// dial rather than an accident. Sweeping <see cref="RaidSpec.GuardDistanceCells"/>
    /// draws the boundary directly: the distance at which return fire stops IS
    /// the usable range, and if it stops well inside the weapon's nominal range
    /// the cause is not geometry.
    /// </para>
    /// <para>
    /// TWO SEATS, ONE OF THEM SCRIPTED. The raider is a scripted slot with one
    /// AttackTarget order, so nothing on the attacking side decides anything —
    /// every reaction in the result belongs to the AI under test. The order
    /// travels the sealed command path like any other, exactly as the movement
    /// scenarios do it.
    /// </para>
    /// <para>
    /// WHAT IT IS NOT. A lab scenario is diagnosis. It says what the simulation
    /// computes for a position somebody constructed; it does not say that this
    /// position occurs in a played match, and it never replaces having seen the
    /// thing in the running game.
    /// </para>
    /// </summary>
    public static class RaidScenarios
    {
        public static RaidResult Run(RaidSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.GuardDistanceCells < 1) throw new ArgumentException("guard distance must be at least one cell");

            var matchSpec = new MatchSpec
            {
                Seed = spec.Seed,
                TickBudget = spec.TickBudget,
                MapWidth = spec.MapWidth,
                MapHeight = spec.MapHeight,
                EntityCapacity = spec.EntityCapacity,
                CountIntents = true,
                Slots = BuildSlots(spec),
            };

            var recorder = new GoalRecorder();
            MultiSlotAiHost host = MultiSlotAiHost.BuildMatch(matchSpec, recorder);

            CanonicalOpening.SlotLayout seat = CanonicalOpening.LayoutOf(spec.AiSlot);
            int hqCellX = seat.HqOriginX + 1;   // 3x3 footprint, origin is the lower left
            int hqCellY = seat.HqOriginY + 1;

            ushort fieldId;
            int raidX, raidY;
            if (spec.Field == RaidField.Start)
            {
                fieldId = seat.FieldId;
                raidX = seat.FieldX;
                raidY = seat.FieldY;
            }
            else
            {
                fieldId = spec.ExpansionFieldId;
                raidX = spec.ExpansionX;
                raidY = spec.ExpansionY;
                if (!host.Economy.TryAddField(fieldId, new GridPos2D(raidX, raidY), CanonicalOpening.FieldReserveAE))
                {
                    throw new InvalidOperationException($"[AiLab] expansion field {fieldId} could not be registered");
                }
            }

            var result = new RaidResult
            {
                Field = spec.Field,
                GuardCount = spec.GuardCount,
                GuardDistanceCells = spec.GuardDistanceCells,
                GuardRole = spec.GuardRole,
                ProfileId = spec.ProfileId,
                RaidCellX = raidX,
                RaidCellY = raidY,
                HqCellX = hqCellX,
                HqCellY = hqCellY,
                RaidToHomeCells = Chebyshev(hqCellX, hqCellY, raidX, raidY),
                DefendHomeCells = spec.Profile.DefendHomeCells,
            };

            // The harvester ON the field, the guards on the line back to their
            // own base — where units waiting for the next wave would stand —
            // and the raider on the far side, so it does not have to walk
            // through the guards to reach its target.
            int towardHomeX = Math.Sign(hqCellX - raidX);
            int towardHomeY = Math.Sign(hqCellY - raidY);

            if (!SimDefinitions.TryGetUnit(spec.AiFaction, spec.GuardRole, out SimUnitDefinition guardDef))
            {
                throw new ArgumentException($"unknown unit definition ({spec.AiFaction}, {spec.GuardRole})");
            }
            result.GuardRangeCells = guardDef.AttackRangeTiles;
            result.RaidDelayTicks = spec.RaidDelayTicks;

            List<uint> guards = SpawnGroup(host, spec.AiSlot, spec.AiFaction, spec.GuardRole, spec.GuardCount,
                Clamp(raidX + towardHomeX * spec.GuardDistanceCells, spec.MapWidth),
                Clamp(raidY + towardHomeY * spec.GuardDistanceCells, spec.MapHeight));

            // The seat gets its head start BEFORE the harvester and the raider
            // exist: whatever it does with those units in this window — gather
            // them, march them at the enemy base — is the position the raid
            // then arrives in.
            for (int i = 0; i < spec.RaidDelayTicks; i++) host.Step();

            EntityId harvester = SpawnOne(host, spec.AiSlot, spec.AiFaction, UnitRole.Harvester, raidX, raidY);
            uint harvesterRaw = UnitCommandStateView.ToRawEntityId(harvester);

            EntityId raider = SpawnOne(host, spec.RaiderSlot, spec.RaiderFaction, spec.RaiderRole,
                Clamp(raidX - towardHomeX * 2, spec.MapWidth),
                Clamp(raidY - towardHomeY * 2, spec.MapHeight));
            uint raiderRaw = UnitCommandStateView.ToRawEntityId(raider);

            // The harvester is told to work, so "kept harvesting under fire" is
            // an observation rather than an assumption about a unit that was
            // standing still anyway.
            SlotPeer aiPeer = host.PeerOf(spec.AiSlot);
            aiPeer.Ingress.TrySubmitIntent(
                CommandIntent.Create(new HarvestPayload(new[] { harvesterRaw }, fieldId)), out _);

            SlotPeer raiderPeer = host.PeerOf(spec.RaiderSlot);
            raiderPeer.Ingress.TrySubmitIntent(
                CommandIntent.Create(new AttackTargetPayload(new[] { raiderRaw }, harvesterRaw)), out _);

            if (host.Entities.TryGetUnit(harvester, out UnitState h0)) result.HarvesterStartHealth = h0.CurrentHealth;
            if (host.Entities.TryGetUnit(raider, out UnitState r0)) result.RaiderStartHealth = r0.CurrentHealth;

            // Where the guard force stands at the moment the raid starts. With
            // no delay this is the spawn distance; with one it is the answer to
            // "were they still there".
            int raiderCellX = SimFixed.WorldToGrid(r0.Transform.PositionX);
            int raiderCellY = SimFixed.WorldToGrid(r0.Transform.PositionY);
            int atStart = int.MaxValue;
            foreach (uint raw in guards)
            {
                if (!TryReadUnit(host, raw, out UnitState guard)) continue;
                result.GuardsAliveAtRaidStart++;
                int distance = Chebyshev(
                    SimFixed.WorldToGrid(guard.Transform.PositionX),
                    SimFixed.WorldToGrid(guard.Transform.PositionY),
                    raiderCellX, raiderCellY);
                if (distance < atStart) atStart = distance;
            }
            result.GuardDistanceAtRaidStart = atStart == int.MaxValue ? -1 : atStart;

            // The weapon table is the authority on reach, not the definition row:
            // it is what CombatSystem asks.
            SimFixed guardRange = WeaponProfiles.Get(spec.AiFaction, spec.GuardRole).AttackRange;
            Watch(host, spec, recorder, guards, harvester, raider, guardRange, result);

            for (int i = 0; i < host.Peers.Length; i++)
            {
                if (host.Peers[i].IntentCounter != null) result.RejectedOrders += host.Peers[i].IntentCounter.Rejected;
            }
            result.FinalTick = host.Kernel.CurrentTick.Value;
            result.FinalStateHash = host.Kernel.CalculateStateHash();
            result.Verdict = Read(result);
            return result;
        }

        private static SlotSpec[] BuildSlots(RaidSpec spec)
        {
            var slots = new SlotSpec[2];
            for (byte i = 0; i < 2; i++)
            {
                bool isAi = i == spec.AiSlot;
                slots[i] = new SlotSpec
                {
                    Slot = i,
                    Faction = isAi ? spec.AiFaction : spec.RaiderFaction,
                    Controller = isAi ? SlotController.Ai : SlotController.Scripted,
                    Profile = new AiFactionProfile(
                        (isAi ? spec.AiFaction : spec.RaiderFaction).ToString(), spec.Profile),
                    ProfileId = isAi ? spec.ProfileId : SlotSpec.CanonicalProfileId,
                };
            }
            return slots;
        }

        /// <summary>
        /// One tick at a time, because every column here is an EDGE: the first
        /// return fire, the first DefendHome, the closest approach. Sampling
        /// them every n ticks would lose exactly the moment they exist for.
        /// </summary>
        private static void Watch(MultiSlotAiHost host, RaidSpec spec, GoalRecorder recorder,
            List<uint> guards, EntityId harvester, EntityId raider, SimFixed guardRange, RaidResult result)
        {
            int previousRaiderHealth = result.RaiderStartHealth;
            int seenFrames = 0;
            var guardSet = new HashSet<uint>(guards);

            for (int i = 0; i < spec.TickBudget; i++)
            {
                host.Step();
                uint tick = host.Kernel.CurrentTick.Value;

                bool raiderAlive = host.Entities.TryGetUnit(raider, out UnitState raiderState) && raiderState.IsActive;
                if (raiderAlive)
                {
                    result.RaiderEndHealth = raiderState.CurrentHealth;
                    if (raiderState.CurrentHealth < previousRaiderHealth && result.ReturnFireTick < 0)
                    {
                        result.ReturnFireTick = (int)tick;
                    }
                    previousRaiderHealth = raiderState.CurrentHealth;
                }
                else if (!result.RaiderDied)
                {
                    result.RaiderDied = true;
                    if (result.ReturnFireTick < 0) result.ReturnFireTick = (int)tick;
                }

                if (host.Entities.TryGetUnit(harvester, out UnitState harvesterState) && harvesterState.IsActive)
                {
                    result.HarvesterEndHealth = harvesterState.CurrentHealth;
                    if (harvesterState.CargoAE > 0 || harvesterState.IsReturningCargo) result.HarvesterHarvested = true;
                }
                else if (!result.HarvesterDied)
                {
                    result.HarvesterDied = true;
                    result.HarvesterDeathTick = (int)tick;
                    result.HarvesterEndHealth = 0;
                }

                // Distance is measured against the RAIDER, not the field: what
                // decides whether a weapon can answer is where the shooter is,
                // and the raider is the only thing worth shooting at.
                if (raiderAlive)
                {
                    int raiderCellX = SimFixed.WorldToGrid(raiderState.Transform.PositionX);
                    int raiderCellY = SimFixed.WorldToGrid(raiderState.Transform.PositionY);
                    if (host.FogOfWar.GetTeamView(spec.AiSlot).IsVisible(raiderCellX, raiderCellY))
                    {
                        result.RaiderVisibleTicks++;
                    }

                    int nearest = int.MaxValue;
                    bool anyInRange = false;
                    foreach (uint raw in guards)
                    {
                        if (!TryReadUnit(host, raw, out UnitState guard)) continue;
                        int distance = Chebyshev(
                            SimFixed.WorldToGrid(guard.Transform.PositionX),
                            SimFixed.WorldToGrid(guard.Transform.PositionY),
                            raiderCellX, raiderCellY);
                        if (distance < nearest) nearest = distance;
                        if (InWeaponRange(in guard, in raiderState, guardRange)) anyInRange = true;
                    }
                    if (nearest != int.MaxValue && nearest < result.ClosestGuardApproachCells)
                    {
                        result.ClosestGuardApproachCells = nearest;
                    }
                    if (anyInRange) result.GuardTicksInRange++;
                }

                // The goal frames the AI has produced since the last look. They
                // arrive on decision ticks only, so this is not per-tick work.
                for (; seenFrames < recorder.Frames.Count; seenFrames++)
                {
                    GoalFrame frame = recorder.Frames[seenFrames];
                    if (frame.Slot != spec.AiSlot) continue;
                    if (frame.Army.HomeThreatened) result.HomeThreatenedSeen = true;
                    if (frame.Army.Committed > result.MaxCommitted) result.MaxCommitted = frame.Army.Committed;

                    foreach (AiUnitGoal goal in frame.Units)
                    {
                        if (goal.Goal == GoalKind.DefendHome && result.DefendHomeTick < 0)
                        {
                            result.DefendHomeTick = (int)frame.Tick;
                        }
                        if (!guardSet.Contains(goal.EntityRaw)) continue;
                        if (!TryReadUnit(host, goal.EntityRaw, out UnitState guard)) continue;
                        if (!raiderAlive) continue;
                        if (!InWeaponRange(in guard, in raiderState, guardRange)) continue;

                        // In range and still silent — the two ways that happens
                        // are exactly the two cases the issue asks about.
                        if (goal.AttackTargetRaw != 0) result.GuardDecisionsWithStandingOrder++;
                        else result.GuardDecisionsFree++;
                    }
                }
            }

            int lost = 0;
            foreach (uint raw in guards)
            {
                if (!TryReadUnit(host, raw, out _)) lost++;
            }
            result.GuardsLost = lost;
            if (result.ClosestGuardApproachCells == int.MaxValue) result.ClosestGuardApproachCells = -1;
        }

        /// <summary>
        /// The result in the vocabulary of the issue. Deliberately conservative:
        /// where the columns do not decide it, it says so instead of picking the
        /// likeliest story.
        /// </summary>
        private static string Read(RaidResult r)
        {
            if (r.ReturnFireTick >= 0) return $"reagiert — Rueckfeuer ab Tick {r.ReturnFireTick}";
            if (r.GuardsAliveAtRaidStart == 0) return "keine eigene Einheit mehr am Leben — keine Messung";
            if (r.RaiderVisibleTicks == 0)
            {
                return "ungesehen — der Angreifer war der Verteidigerseite keinen Tick lang sichtbar";
            }
            if (r.GuardTicksInRange == 0)
            {
                return $"A: ausser Reichweite — naechster Wachposten {r.ClosestGuardApproachCells} Zellen, " +
                       $"Waffe reicht {r.GuardRangeCells}";
            }
            if (r.GuardDecisionsWithStandingOrder > 0 && r.GuardDecisionsFree == 0)
            {
                return $"B: stehender Angriffsbefehl — {r.GuardDecisionsWithStandingOrder} Entscheidungen " +
                       "in Reichweite, alle mit fremdem Ziel";
            }
            if (r.GuardDecisionsFree > 0 && r.HomeThreatenedSeen && r.MaxCommitted > 0)
            {
                return $"C: DefendHome schweigt — HomeThreatened lag an, Armee war mit {r.MaxCommitted} " +
                       "Einheiten in einer Welle gebunden";
            }
            if (r.GuardDecisionsFree > 0)
            {
                return $"offen — {r.GuardDecisionsFree} Entscheidungen in Reichweite und ohne Ziel, " +
                       "trotzdem kein Schuss";
            }
            return "offen";
        }

        // ================================================================

        private static EntityId SpawnOne(MultiSlotAiHost host, byte slot, FactionId faction, UnitRole role,
            int cellX, int cellY)
        {
            if (!SimDefinitions.TryGetUnit(faction, role, out SimUnitDefinition def))
            {
                throw new ArgumentException($"unknown unit definition ({faction}, {role})");
            }
            return host.Entities.SpawnUnit(
                slot, new Transform2D(SimFixed.FromInt(cellX), SimFixed.FromInt(cellY)),
                def.MoveSpeed, maxHealth: def.MaxHealth, role: def.Role);
        }

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
                    new Transform2D(SimFixed.FromInt(originX - i / 3), SimFixed.FromInt(originY - 1 + i % 3)),
                    def.MoveSpeed, maxHealth: def.MaxHealth, role: def.Role);
                raws.Add(UnitCommandStateView.ToRawEntityId(id));
            }
            raws.Sort();
            return raws;
        }

        /// <summary>
        /// <c>CombatSystem.IsInRange</c>, copied deliberately and for one run
        /// only: squared Euclidean distance against weapon range plus the
        /// target's radius. It is a copy because the original is private, and a
        /// copy of a rule is a liability — so it is one expression, beside a
        /// comment naming its source, rather than a re-derivation.
        /// </summary>
        private static bool InWeaponRange(in UnitState attacker, in UnitState target, SimFixed weaponRange)
        {
            long dx = (long)attacker.Transform.PositionX.RawValue - target.Transform.PositionX.RawValue;
            long dy = (long)attacker.Transform.PositionY.RawValue - target.Transform.PositionY.RawValue;
            SimFixed reach = weaponRange + target.Radius;
            return dx * dx + dy * dy <= (long)reach.RawValue * reach.RawValue;
        }

        private static bool TryReadUnit(MultiSlotAiHost host, uint raw, out UnitState unit)
        {
            EntityId id = UnitCommandStateView.ToEntityId(raw);
            return host.Entities.TryGetUnit(id, out unit) && unit.IsActive;
        }

        private static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        private static int Clamp(int cell, int size) => Math.Max(1, Math.Min(size - 2, cell));

        public static string ToNdjson(IReadOnlyList<RaidResult> results)
        {
            var output = new StringBuilder(results.Count * 320);
            foreach (RaidResult r in results)
            {
                output.Append("{\"field\":\"").Append(r.Field)
                      .Append("\",\"profile\":\"").Append(r.ProfileId)
                      .Append("\",\"guards\":").Append(r.GuardCount)
                      .Append(",\"guardDistanceCells\":").Append(r.GuardDistanceCells)
                      .Append(",\"guardRole\":\"").Append(r.GuardRole)
                      .Append("\",\"guardRangeCells\":").Append(r.GuardRangeCells)
                      .Append(",\"raidCell\":[").Append(r.RaidCellX).Append(',').Append(r.RaidCellY)
                      .Append("],\"hqCell\":[").Append(r.HqCellX).Append(',').Append(r.HqCellY)
                      .Append("],\"raidToHomeCells\":").Append(r.RaidToHomeCells)
                      .Append(",\"defendHomeCells\":").Append(r.DefendHomeCells)
                      .Append(",\"returnFireTick\":").Append(r.ReturnFireTick)
                      .Append(",\"defendHomeTick\":").Append(r.DefendHomeTick)
                      .Append(",\"homeThreatenedSeen\":").Append(r.HomeThreatenedSeen ? 1 : 0)
                      .Append(",\"maxCommitted\":").Append(r.MaxCommitted)
                      .Append(",\"raidDelayTicks\":").Append(r.RaidDelayTicks)
                      .Append(",\"guardDistanceAtRaidStart\":").Append(r.GuardDistanceAtRaidStart)
                      .Append(",\"guardsAliveAtRaidStart\":").Append(r.GuardsAliveAtRaidStart)
                      .Append(",\"closestGuardApproachCells\":").Append(r.ClosestGuardApproachCells)
                      .Append(",\"guardTicksInRange\":").Append(r.GuardTicksInRange)
                      .Append(",\"raiderVisibleTicks\":").Append(r.RaiderVisibleTicks)
                      .Append(",\"guardDecisionsWithStandingOrder\":").Append(r.GuardDecisionsWithStandingOrder)
                      .Append(",\"guardDecisionsFree\":").Append(r.GuardDecisionsFree)
                      .Append(",\"harvesterStartHealth\":").Append(r.HarvesterStartHealth)
                      .Append(",\"harvesterEndHealth\":").Append(r.HarvesterEndHealth)
                      .Append(",\"harvesterDied\":").Append(r.HarvesterDied ? 1 : 0)
                      .Append(",\"harvesterDeathTick\":").Append(r.HarvesterDeathTick)
                      .Append(",\"harvesterHarvested\":").Append(r.HarvesterHarvested ? 1 : 0)
                      .Append(",\"raiderStartHealth\":").Append(r.RaiderStartHealth)
                      .Append(",\"raiderEndHealth\":").Append(r.RaiderEndHealth)
                      .Append(",\"raiderDied\":").Append(r.RaiderDied ? 1 : 0)
                      .Append(",\"guardsLost\":").Append(r.GuardsLost)
                      .Append(",\"rejectedOrders\":").Append(r.RejectedOrders)
                      .Append(",\"finalTick\":").Append(r.FinalTick)
                      .Append(",\"finalStateHash\":\"0x").Append(r.FinalStateHash.ToString("X16"))
                      .Append("\",\"verdict\":\"").Append(r.Verdict.Replace("\"", "'"))
                      .Append("\"}\n");
            }
            return output.ToString();
        }
    }
}
