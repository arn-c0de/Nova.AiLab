using System;
using System.Collections.Generic;
using System.Text;
using Nova.Core;
using Nova.Simulation.Combat;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Economy;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>What happened to one entity in one tick.</summary>
    public enum DebugEventKind
    {
        Spawn = 0,
        Death = 1,
        Damage = 2,
        Heal = 3,
        Order = 4,
        Goal = 5,
        MoveStart = 6,
        MoveStop = 7,
        AttackStart = 8,
        AttackSwitch = 9,
        AttackStop = 10,
        HarvestStart = 11,
        HarvestStop = 12,
        CargoFull = 13,
        CargoDelivered = 14,
        SiteOpen = 15,
        SiteDone = 16,
        Stuck = 17,
        Unstuck = 18,
        RetreatBelow = 19,
        RetreatAbove = 20,
    }

    /// <summary>
    /// One entry of <c>events.ndjson</c>: an entity, a tick, a kind, and the
    /// few numbers that kind carries.
    /// <para>
    /// NAMED KEYS, against the machine of <see cref="ViewFrame"/>. The view
    /// frame is terse because it is dense — hundreds of frames times dozens of
    /// entities. Events are sparse, so size buys nothing here, and the file is
    /// meant to be read with <c>grep</c> at three in the morning.
    /// </para>
    /// </summary>
    public sealed class DebugEvent : INdjsonLine
    {
        /// <summary>Value of a field this kind does not carry.</summary>
        public const int Absent = int.MinValue;

        public uint Tick;

        /// <summary>Raw entity id — the same identity <c>view.ndjson</c> and <c>tracks.ndjson</c> carry.</summary>
        public uint Id;

        public byte Slot;
        public UnitRole Role;
        public DebugEventKind Kind;

        /// <summary>Related entity or id: attack target, harvest field, building definition.</summary>
        public uint Ref;

        public int A = Absent;
        public int B = Absent;
        public int C = Absent;
        public int D = Absent;

        /// <summary>
        /// Where the victim stood when it was hit, Q16.16 raw. Carried for the
        /// attacker reconstruction, not for the file.
        /// <para>
        /// IT CANNOT BE READ OFF <see cref="A"/>/<see cref="B"/>, and that is
        /// the whole reason it exists: those two mean a different thing per
        /// kind. A death event carries the position there, a DAMAGE event
        /// carries the health it went from and to. Reading them as coordinates
        /// measured the distance from the attacker to the map ORIGIN — which
        /// sits next to one of the two corner bases and nowhere near the other,
        /// so the wide path could name a unit of slot 0 and never one of slot 1,
        /// wherever the victim actually stood.
        /// </para>
        /// </summary>
        public int VictimX = Absent;
        public int VictimY = Absent;

        /// <summary>
        /// DERIVED attackers, never observed — null when the kind carries
        /// none. See <see cref="DebugEventLog"/> and <c>notes/schadensquelle.md</c>
        /// for how it is derived and where the derivation fails.
        /// </summary>
        public List<uint> By;

        /// <summary>True only when the derivation named EXACTLY ONE candidate on the strict path.</summary>
        public bool BySure;

        public static string NameOf(DebugEventKind kind)
        {
            switch (kind)
            {
                case DebugEventKind.Spawn: return "spawn";
                case DebugEventKind.Death: return "death";
                case DebugEventKind.Damage: return "damage";
                case DebugEventKind.Heal: return "heal";
                case DebugEventKind.Order: return "order";
                case DebugEventKind.Goal: return "goal";
                case DebugEventKind.MoveStart: return "moveStart";
                case DebugEventKind.MoveStop: return "moveStop";
                case DebugEventKind.AttackStart: return "attackStart";
                case DebugEventKind.AttackSwitch: return "attackSwitch";
                case DebugEventKind.AttackStop: return "attackStop";
                case DebugEventKind.HarvestStart: return "harvestStart";
                case DebugEventKind.HarvestStop: return "harvestStop";
                case DebugEventKind.CargoFull: return "cargoFull";
                case DebugEventKind.CargoDelivered: return "cargoDelivered";
                case DebugEventKind.SiteOpen: return "siteOpen";
                case DebugEventKind.SiteDone: return "siteDone";
                case DebugEventKind.Stuck: return "stuck";
                case DebugEventKind.Unstuck: return "unstuck";
                case DebugEventKind.RetreatBelow: return "retreatBelow";
                case DebugEventKind.RetreatAbove: return "retreatAbove";
                default: return "unknown";
            }
        }

        public string ToJsonLine()
        {
            var json = new StringBuilder(160);
            json.Append("{\"t\":").Append(Tick)
                .Append(",\"id\":").Append(Id)
                .Append(",\"slot\":").Append(Slot)
                .Append(",\"role\":").Append((int)Role)
                .Append(",\"k\":\"").Append(NameOf(Kind)).Append('"');

            switch (Kind)
            {
                case DebugEventKind.Spawn:
                    Field(json, "x", A); Field(json, "y", B); Field(json, "hp", C);
                    // The ceiling travels with the birth, so a reader can turn
                    // every later health value into a percentage without
                    // waiting for the next view frame to tell it the maximum.
                    Field(json, "hpMax", D);
                    break;
                case DebugEventKind.Death:
                    Field(json, "x", A); Field(json, "y", B); Field(json, "hp", C);
                    AppendBy(json);
                    break;
                case DebugEventKind.Damage:
                    Field(json, "from", A); Field(json, "to", B);
                    AppendBy(json);
                    break;
                case DebugEventKind.Heal:
                    Field(json, "from", A); Field(json, "to", B);
                    break;
                case DebugEventKind.Order:
                case DebugEventKind.Goal:
                    Field(json, "fx", A); Field(json, "fy", B);
                    Field(json, "tx", C); Field(json, "ty", D);
                    break;
                case DebugEventKind.MoveStart:
                case DebugEventKind.MoveStop:
                case DebugEventKind.Stuck:
                    Field(json, "x", A); Field(json, "y", B);
                    break;
                case DebugEventKind.Unstuck:
                    Field(json, "ticks", A); Field(json, "x", B); Field(json, "y", C);
                    break;
                case DebugEventKind.AttackStart:
                case DebugEventKind.AttackSwitch:
                case DebugEventKind.AttackStop:
                    json.Append(",\"target\":").Append(Ref);
                    break;
                case DebugEventKind.HarvestStart:
                case DebugEventKind.HarvestStop:
                    // The field's cell rides along: without it a reader knows
                    // WHICH field but not where to draw the line to.
                    json.Append(",\"field\":").Append(Ref);
                    Field(json, "x", A); Field(json, "y", B);
                    break;
                case DebugEventKind.CargoFull:
                case DebugEventKind.CargoDelivered:
                    Field(json, "cargo", A);
                    break;
                case DebugEventKind.SiteOpen:
                case DebugEventKind.SiteDone:
                    json.Append(",\"def\":").Append(Ref);
                    break;
                case DebugEventKind.RetreatBelow:
                case DebugEventKind.RetreatAbove:
                    Field(json, "hp", A);
                    break;
            }

            json.Append('}');
            return json.ToString();
        }

        private static void Field(StringBuilder json, string key, int value)
        {
            if (value == Absent) return;
            json.Append(",\"").Append(key).Append("\":").Append(value);
        }

        private void AppendBy(StringBuilder json)
        {
            if (By == null || By.Count == 0) return;
            json.Append(",\"by\":[");
            for (int i = 0; i < By.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(By[i]);
            }
            json.Append("],\"bySure\":").Append(BySure ? 1 : 0);
        }
    }

    /// <summary>
    /// Everything one entity accumulated over its life — the per-unit
    /// counterpart to <see cref="SlotMetrics"/>. Filled by
    /// <see cref="DebugEventLog"/> while it walks the ticks, read by
    /// <see cref="RouteMetrics"/> afterwards.
    /// </summary>
    public sealed class UnitTally
    {
        public uint Id;
        public byte Slot;
        public UnitRole Role;

        public uint FirstTick;
        public uint LastTick;
        public bool Died;

        /// <summary>Ticks with <c>IsMoving</c> set.</summary>
        public int MovingTicks;

        /// <summary>
        /// Ticks with <c>IsMoving</c> set and the position UNCHANGED. This is
        /// the number "no mutual blocking" (CLAUDE.md section 1) never had:
        /// a unit that wants to move and does not is measured here instead of
        /// suspected.
        /// </summary>
        public int BlockedTicks;

        public int OrderChanges;
        public int GoalChanges;
        public int AttackStarts;
        public int DamageTaken;

        /// <summary>Damage this unit is DERIVED to have dealt — only from unambiguous attributions.</summary>
        public int DamageDealtDerived;

        /// <summary>Kills this unit is DERIVED to have made — only from unambiguous attributions.</summary>
        public int KillsDerived;
    }

    /// <summary>
    /// Turns the committed state into a per-entity event stream: what every
    /// unit did, at which exact tick.
    /// <para>
    /// WHY IT IS NOT A SAMPLE. <see cref="TraceCollector"/> samples every n
    /// ticks and says so; that is right for counting an army. It cannot answer
    /// "why did THIS unit stand there for four seconds" — an edge that falls
    /// between two samples is gone. So this walks EVERY tick and compares
    /// against shadow arrays of the previous one, exactly the machine
    /// <c>TrackReactions</c> already uses (TraceCollector.cs:138): ascending
    /// index scan, no dictionary order, one struct read per entity.
    /// </para>
    /// <para>
    /// PURE OBSERVER, the same hard condition as the view recorder and the
    /// trace collector: reads after <c>StepTick()</c>, never writes back, no
    /// part of the tick order, the state hash or a snapshot. Asserted in
    /// <c>DebugEventTests</c>.
    /// </para>
    /// <para>
    /// THE ONE DERIVED FIELD is <c>by</c> on damage and death. The simulation
    /// reports no damage source — <c>CombatSystem</c> applies the hit and
    /// moves on (CombatSystem.cs:246-252) — so the attacker is reconstructed
    /// from state and MARKED as reconstructed. It is never printed as an
    /// observation, and ambiguity stays visible as a list instead of
    /// collapsing into a guess. <c>notes/schadensquelle.md</c> writes down
    /// where the reconstruction fails and what a proper hook in the game would
    /// look like.
    /// </para>
    /// </summary>
    public sealed class DebugEventLog
    {
        /// <summary>
        /// Ticks a unit may stand still with <c>IsMoving</c> set before it
        /// counts as stuck. 20 ticks = 2 s on the canonical 10 Hz clock —
        /// long enough that a normal path recompute does not trip it, short
        /// enough that a unit wedged on a building corner does.
        /// </summary>
        public const int StuckThresholdTicks = 20;

        private readonly MultiSlotAiHost _host;
        private readonly int _slotCount;
        private readonly List<DebugEvent> _events = new List<DebugEvent>(4096);
        private readonly Dictionary<uint, UnitTally> _tallies = new Dictionary<uint, UnitTally>(256);

        /// <summary>
        /// The previous tick of ONE entity pool slot.
        /// <para>
        /// This used to be twenty-one parallel arrays. They were allocated in
        /// twenty-one lines, written in twenty-one assignments, and read as
        /// <c>_health[i]</c> beside <c>_x[i]</c> beside <c>_stuck[i]</c> — with
        /// nothing but discipline holding them at the same index. Adding an
        /// observed field meant remembering four places, and forgetting one
        /// produced a shadow that disagreed with itself: an edge reported
        /// against a stale value, which reads like a finding about the
        /// simulation and is a defect in the observer.
        /// </para>
        /// <para>
        /// A struct is the same memory in the same order — one array of
        /// contiguous records rather than twenty-one arrays walked in lockstep
        /// — and the per-tick scan touches each record once instead of jumping
        /// between twenty-one places.
        /// </para>
        /// </summary>
        private struct EntityShadow
        {
            public bool Active;
            public ushort Version;
            public uint Raw;
            public byte Owner;
            public UnitRole Role;
            public int Health;
            public long OrderKey;
            public long GoalKey;
            public bool Moving;
            public uint Attack;

            /// <summary>
            /// The attack target of the tick BEFORE this one. A killer's current
            /// target is already cleared when the kill is observed —
            /// <c>KillUnit</c> resolves every order on the dead id in the same
            /// tick — so the attribution needs the value it had a tick earlier.
            /// </summary>
            public uint AttackPrev;

            public ushort Field;
            public ushort SiteDef;
            public int Cargo;
            public bool Returning;
            public bool Site;
            public int X;
            public int Y;
            public int StillTicks;
            public bool Stuck;
            public bool Below;
        }

        /// <summary>Shadow state of the previous tick, per entity POOL SLOT.</summary>
        private readonly EntityShadow[] _shadow;

        /// <summary>Victims of this tick, paired with the event that has to name an attacker.</summary>
        private readonly List<DebugEvent> _needAttacker = new List<DebugEvent>(16);

        public DebugEventLog(MultiSlotAiHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _slotCount = host.SlotCount;

            _shadow = new EntityShadow[host.Entities.Capacity];
        }

        public IReadOnlyList<DebugEvent> Events => _events;

        /// <summary>Per-entity totals, keyed by raw entity id.</summary>
        public IReadOnlyDictionary<uint, UnitTally> Tallies => _tallies;

        /// <summary>
        /// One tick's edges. The shadow arrays start empty, so the very first
        /// call reports the canonical opening as <c>spawn</c> — which is what
        /// it is: the HQ and the builder appear at tick 0.
        /// </summary>
        public void Collect(uint tick)
        {
            _needAttacker.Clear();

            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                ref EntityShadow shadow = ref _shadow[i];

                // The attack target of the tick before this one, carried over
                // before anything can overwrite it. This used to be a full
                // Array.Copy of the entity capacity at the top of every tick —
                // 1024 entries times 27.000 ticks, for a value only the
                // attribution below reads. Folded into the pass that walks
                // these records anyway, it is one assignment.
                shadow.AttackPrev = shadow.Attack;

                bool sameUnit = shadow.Active && u.IsActive && u.Id.Version == shadow.Version;

                if (shadow.Active && !sameUnit) EmitDeath(tick, i);

                if (!u.IsActive)
                {
                    shadow.Active = false;
                    continue;
                }
                if (u.PlayerId >= _slotCount)
                {
                    Snapshot(i, in u, sameUnit);
                    continue;
                }

                uint raw = UnitCommandStateView.ToRawEntityId(u.Id);
                bool isSite = _host.Construction.TryGetSite(raw, out ushort siteDefId, out _, out _);

                if (!sameUnit)
                {
                    EmitSpawn(tick, in u, raw, isSite, siteDefId);
                }
                else
                {
                    EmitEdges(tick, i, in u, raw, isSite);
                }

                Snapshot(i, in u, sameUnit);
            }

            if (_needAttacker.Count > 0) AttributeAttackers();
        }

        // ----------------------------------------------------------------
        // The edges
        // ----------------------------------------------------------------

        private void EmitSpawn(uint tick, in UnitState u, uint raw, bool isSite, ushort siteDefId)
        {
            UnitTally tally = TallyOf(raw, u.PlayerId, u.Role);
            tally.FirstTick = tick;
            tally.LastTick = tick;

            // A site gets BOTH: every entity is born with a spawn, so
            // "every death has exactly one spawn before it" holds without an
            // exception clause, and the site additionally says what it is
            // going to become.
            Add(new DebugEvent
            {
                Tick = tick,
                Id = raw,
                Slot = u.PlayerId,
                Role = u.Role,
                Kind = DebugEventKind.Spawn,
                A = u.Transform.PositionX.RawValue,
                B = u.Transform.PositionY.RawValue,
                C = u.CurrentHealth,
                D = u.MaxHealth,
            });

            if (!isSite) return;
            Add(new DebugEvent
            {
                Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                Kind = DebugEventKind.SiteOpen, Ref = siteDefId,
            });
        }

        private void EmitDeath(uint tick, int i)
        {
            ref EntityShadow shadow = ref _shadow[i];
            if (shadow.Owner >= _slotCount) return;

            UnitTally tally = TallyOf(shadow.Raw, shadow.Owner, shadow.Role);
            tally.LastTick = tick;
            tally.Died = true;

            var death = new DebugEvent
            {
                Tick = tick,
                Id = shadow.Raw,
                Slot = shadow.Owner,
                Role = shadow.Role,
                Kind = DebugEventKind.Death,
                A = shadow.X,
                B = shadow.Y,
                C = shadow.Health,
                // The same position A/B carry for this kind — named, so the
                // reconstruction never has to know which kind it is holding.
                VictimX = shadow.X,
                VictimY = shadow.Y,
            };
            Add(death);
            _needAttacker.Add(death);
        }

        private void EmitEdges(uint tick, int i, in UnitState u, uint raw, bool isSite)
        {
            ref EntityShadow shadow = ref _shadow[i];

            UnitTally tally = TallyOf(raw, u.PlayerId, u.Role);
            tally.LastTick = tick;
            tally.Role = u.Role;

            // ---- health -------------------------------------------------
            if (u.CurrentHealth < shadow.Health)
            {
                tally.DamageTaken += shadow.Health - u.CurrentHealth;
                var damage = new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = DebugEventKind.Damage, A = shadow.Health, B = u.CurrentHealth,
                    // A/B are HEALTH here. The reconstruction needs a position
                    // and has to be handed one.
                    VictimX = u.Transform.PositionX.RawValue,
                    VictimY = u.Transform.PositionY.RawValue,
                };
                Add(damage);
                _needAttacker.Add(damage);
            }
            else if (u.CurrentHealth > shadow.Health)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = DebugEventKind.Heal, A = shadow.Health, B = u.CurrentHealth,
                });
            }

            // ---- the site becoming a building ---------------------------
            // The definition id has to come from the tick the entity WAS a
            // site: TryGetSite says nothing once the site is gone.
            if (shadow.Site && !isSite)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = DebugEventKind.SiteDone, Ref = shadow.SiteDef,
                });
            }

            // ---- the standing move order --------------------------------
            //
            // Same definition TraceCollector calls a reaction (OrderKeyOf,
            // TraceCollector.cs:194): a change to a different VALID cell.
            // Arrival clears the target through Stop() and lands on the
            // invalid value, which is not an order and is not counted here.
            long orderKey = OrderKeyOf(u.TargetGridPos);
            if (orderKey >= 0 && orderKey != shadow.OrderKey)
            {
                tally.OrderChanges++;
                Add(GridChange(tick, raw, u, DebugEventKind.Order, shadow.OrderKey, orderKey));
            }

            long goalKey = OrderKeyOf(u.GoalGridPos);
            if (goalKey != shadow.GoalKey)
            {
                tally.GoalChanges++;
                Add(GridChange(tick, raw, u, DebugEventKind.Goal, shadow.GoalKey, goalKey));
            }

            // ---- moving, and standing still while doing it ---------------
            if (u.IsMoving != shadow.Moving)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = u.IsMoving ? DebugEventKind.MoveStart : DebugEventKind.MoveStop,
                    A = u.Transform.PositionX.RawValue,
                    B = u.Transform.PositionY.RawValue,
                });
            }

            bool stood = u.Transform.PositionX.RawValue == shadow.X && u.Transform.PositionY.RawValue == shadow.Y;
            if (u.IsMoving)
            {
                tally.MovingTicks++;
                if (stood)
                {
                    tally.BlockedTicks++;
                    shadow.StillTicks++;
                    if (shadow.StillTicks >= StuckThresholdTicks && !shadow.Stuck)
                    {
                        shadow.Stuck = true;
                        Add(new DebugEvent
                        {
                            Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                            Kind = DebugEventKind.Stuck, A = shadow.X, B = shadow.Y,
                        });
                    }
                }
            }

            if ((!u.IsMoving || !stood) && shadow.Stuck)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = DebugEventKind.Unstuck, A = shadow.StillTicks, B = shadow.X, C = shadow.Y,
                });
                shadow.Stuck = false;
            }
            if (!u.IsMoving || !stood) shadow.StillTicks = 0;

            // ---- attack target ------------------------------------------
            uint attack = u.AttackTarget.IsValid ? UnitCommandStateView.ToRawEntityId(u.AttackTarget) : 0u;
            if (attack != shadow.Attack)
            {
                DebugEventKind kind = attack == 0
                    ? DebugEventKind.AttackStop
                    : shadow.Attack == 0 ? DebugEventKind.AttackStart : DebugEventKind.AttackSwitch;
                if (kind != DebugEventKind.AttackStop) tally.AttackStarts++;
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = kind, Ref = attack == 0 ? shadow.Attack : attack,
                });
            }

            // ---- harvesting and cargo -----------------------------------
            if (u.HarvestFieldId != shadow.Field)
            {
                ushort fieldId = u.HarvestFieldId != 0 ? u.HarvestFieldId : shadow.Field;
                bool known = _host.Economy.TryGetField(fieldId, out AetheriumField field);
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = u.HarvestFieldId != 0 ? DebugEventKind.HarvestStart : DebugEventKind.HarvestStop,
                    Ref = fieldId,
                    A = known ? SimFixed.FromInt(field.GridPos.X).RawValue : DebugEvent.Absent,
                    B = known ? SimFixed.FromInt(field.GridPos.Y).RawValue : DebugEvent.Absent,
                });
            }
            if (u.IsReturningCargo != shadow.Returning)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = u.IsReturningCargo ? DebugEventKind.CargoFull : DebugEventKind.CargoDelivered,
                    A = u.IsReturningCargo ? u.CargoAE : shadow.Cargo,
                });
            }

            // ---- the retreat marker -------------------------------------
            // The frame's definition, not a second one beside it: a damaged
            // BUILDER used to be "below the retreat mark" here and not there,
            // and the same run said two things about the same unit.
            int healthPercent = u.MaxHealth > 0 ? u.CurrentHealth * 100 / u.MaxHealth : 0;
            bool below = ViewRecorder.IsBelowRetreatMarker(u.Role, isSite, healthPercent);
            if (below != shadow.Below)
            {
                Add(new DebugEvent
                {
                    Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role,
                    Kind = below ? DebugEventKind.RetreatBelow : DebugEventKind.RetreatAbove,
                    A = healthPercent,
                });
            }
        }

        private static DebugEvent GridChange(uint tick, uint raw, in UnitState u, DebugEventKind kind, long from, long to)
        {
            return new DebugEvent
            {
                Tick = tick, Id = raw, Slot = u.PlayerId, Role = u.Role, Kind = kind,
                A = from < 0 ? -1 : (int)(from & 0xFFFF),
                B = from < 0 ? -1 : (int)(from >> 16),
                C = to < 0 ? -1 : (int)(to & 0xFFFF),
                D = to < 0 ? -1 : (int)(to >> 16),
            };
        }

        /// <summary>The same comparable integer <c>TraceCollector.OrderKeyOf</c> builds; -1 when the cell is invalid.</summary>
        private static long OrderKeyOf(GridPos2D pos)
        {
            return pos.IsValid ? ((long)pos.Y << 16) | pos.X : -1L;
        }

        // ----------------------------------------------------------------
        // Who fired — DERIVED, and marked as derived
        // ----------------------------------------------------------------

        /// <summary>
        /// Reconstructs the attacker of every damage and death of this tick.
        /// <para>
        /// STRICT PATH: an enemy unit whose weapon cooldown sits at the MAXIMUM
        /// of its own profile — <c>CombatSystem</c> sets it to exactly that in
        /// the same statement that applies the damage (CombatSystem.cs:251-252)
        /// — and which either still names the victim as its attack target, or
        /// named it in the PREVIOUS tick. The previous tick matters for a kill:
        /// <c>KillUnit</c> clears every attack order on the dead id in the same
        /// tick (CombatSystem.cs:311-322), so a killer's current target is
        /// already gone.
        /// </para>
        /// <para>
        /// WIDE PATH, only when the strict one names nobody: any enemy with a
        /// full cooldown whose weapon could reach the victim's last position.
        /// It catches the shot from a unit that acquired its target and fired
        /// in the same tick, and it is never marked sure — the reach test has
        /// no target radius to work with once the target is despawned.
        /// </para>
        /// <para>
        /// Ambiguity stays visible: two candidates are written as two, never
        /// collapsed into the first one.
        /// </para>
        /// </summary>
        private void AttributeAttackers()
        {
            UnitState[] units = _host.Entities.RawUnits;

            for (int e = 0; e < _needAttacker.Count; e++)
            {
                DebugEvent victim = _needAttacker[e];
                var strict = new List<uint>(2);
                var wide = new List<uint>(2);

                for (int i = 0; i < units.Length; i++)
                {
                    ref readonly UnitState a = ref units[i];
                    if (!a.IsActive || a.PlayerId >= _slotCount || a.PlayerId == victim.Slot) continue;

                    WeaponProfile weapon = WeaponProfiles.Get(_host.Economy.GetSlotFaction(a.PlayerId), a.Role);
                    if (!weapon.IsArmed || a.WeaponCooldownTicks != weapon.AttackCooldownTicks) continue;

                    uint attackerRaw = UnitCommandStateView.ToRawEntityId(a.Id);
                    uint nowTarget = a.AttackTarget.IsValid ? UnitCommandStateView.ToRawEntityId(a.AttackTarget) : 0u;
                    uint thenTarget = _shadow[a.Id.Index].AttackPrev;

                    if (nowTarget == victim.Id || thenTarget == victim.Id)
                    {
                        strict.Add(attackerRaw);
                    }
                    else if (CouldReach(in a, weapon, victim))
                    {
                        wide.Add(attackerRaw);
                    }
                }

                victim.By = strict.Count > 0 ? strict : wide;
                victim.BySure = strict.Count == 1;
                if (victim.By.Count == 0) victim.By = null;

                if (!victim.BySure) continue;

                // Only an unambiguous attribution is counted. A tally that
                // includes maybes reads like a measurement and is not one.
                UnitTally shooter = _tallies.TryGetValue(victim.By[0], out UnitTally found) ? found : null;
                if (shooter == null) continue;
                if (victim.Kind == DebugEventKind.Damage) shooter.DamageDealtDerived += victim.A - victim.B;
                else shooter.KillsDerived++;
            }
        }

        /// <summary>
        /// Whether this attacker's weapon could have reached the victim's last
        /// position. One cell of slack stands in for the target radius, which
        /// a despawned victim no longer has — so this is a NET, not a proof.
        /// </summary>
        private static bool CouldReach(in UnitState attacker, WeaponProfile weapon, DebugEvent victim)
        {
            if (victim.VictimX == DebugEvent.Absent || victim.VictimY == DebugEvent.Absent) return false;

            long dx = (long)attacker.Transform.PositionX.RawValue - victim.VictimX;
            long dy = (long)attacker.Transform.PositionY.RawValue - victim.VictimY;
            SimFixed reach = weapon.AttackRange + SimFixed.FromInt(1);
            long reachSquared = (long)reach.RawValue * reach.RawValue;
            return dx * dx + dy * dy <= reachSquared;
        }

        // ----------------------------------------------------------------

        private void Add(DebugEvent debugEvent) => _events.Add(debugEvent);

        private UnitTally TallyOf(uint raw, byte slot, UnitRole role)
        {
            if (_tallies.TryGetValue(raw, out UnitTally tally)) return tally;
            tally = new UnitTally { Id = raw, Slot = slot, Role = role };
            _tallies.Add(raw, tally);
            return tally;
        }

        /// <summary>
        /// This tick becomes the previous one. <c>AttackPrev</c> is deliberately
        /// NOT written here: <see cref="Collect"/> carries it over at the top of
        /// the pass, before anything can overwrite the value the attribution
        /// needs.
        /// </summary>
        private void Snapshot(int i, in UnitState u, bool sameUnit)
        {
            ref EntityShadow shadow = ref _shadow[i];

            shadow.Active = true;
            shadow.Version = u.Id.Version;
            shadow.Raw = UnitCommandStateView.ToRawEntityId(u.Id);
            shadow.Owner = u.PlayerId;
            shadow.Role = u.Role;
            shadow.Health = u.CurrentHealth;
            shadow.OrderKey = OrderKeyOf(u.TargetGridPos);
            shadow.GoalKey = OrderKeyOf(u.GoalGridPos);
            shadow.Moving = u.IsMoving;
            shadow.Attack = u.AttackTarget.IsValid ? UnitCommandStateView.ToRawEntityId(u.AttackTarget) : 0u;
            shadow.Field = u.HarvestFieldId;
            shadow.Cargo = u.CargoAE;
            shadow.Returning = u.IsReturningCargo;
            shadow.Site = _host.Construction.TryGetSite(shadow.Raw, out ushort siteDefId, out _, out _);
            shadow.SiteDef = shadow.Site ? siteDefId : (ushort)0;
            shadow.X = u.Transform.PositionX.RawValue;
            shadow.Y = u.Transform.PositionY.RawValue;

            int healthPercent = u.MaxHealth > 0 ? u.CurrentHealth * 100 / u.MaxHealth : 0;
            shadow.Below = ViewRecorder.IsBelowRetreatMarker(u.Role, shadow.Site, healthPercent);

            if (sameUnit) return;

            shadow.StillTicks = 0;
            shadow.Stuck = false;
        }
    }
}
