using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Economy;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Reads the committed state after <c>StepTick()</c> and turns it into the
    /// integer metrics of plan section 3.3.
    /// <para>
    /// PURE OBSERVER, the same hard condition the view recorder carries
    /// (section 3.4): it reads, it never writes back, it is not part of the
    /// tick order, the state hash or a snapshot. A run with and without the
    /// collector must produce the identical hash chain — asserted in
    /// <c>TraceCollectorTests</c>, not merely intended.
    /// </para>
    /// <para>
    /// Cost discipline: the expensive part is an O(capacity) entity scan, and
    /// it runs only on a metric tick. Per-tick work is the low-power counter
    /// alone, which reads one struct per slot.
    /// </para>
    /// </summary>
    public sealed class TraceCollector
    {
        private readonly MultiSlotAiHost _host;
        private readonly int _slotCount;

        private readonly int[] _lowPowerTicks;
        private readonly int[] _unitsLost;
        private readonly long[] _healthLost;

        /// <summary>
        /// Last seen state per entity slot, so a unit that vanished between two
        /// samples can be attributed to its owner. An entity slot reused by a
        /// new unit carries a new version, which is what makes the loss
        /// visible at all.
        /// </summary>
        private readonly bool[] _wasActive;
        private readonly byte[] _lastOwner;
        private readonly ushort[] _lastVersion;
        private readonly int[] _lastHealth;

        private readonly List<EntityId> _visibleScratch = new List<EntityId>(256);

        // ---- reaction latency, tracked per TICK and per ENTITY -----------
        //
        // The metric sample cannot carry this: it asks how many ticks pass
        // between a unit losing health and that unit being sent somewhere
        // else, and both events live between two samples. So these arrays
        // are a second, cheaper snapshot taken every tick — health, standing
        // move order, identity — and nothing else.
        private readonly ReactionTally[] _reactions;
        private readonly bool[] _reactActive;
        private readonly byte[] _reactOwner;
        private readonly ushort[] _reactVersion;
        private readonly int[] _reactHealth;
        private readonly long[] _reactOrder;

        /// <summary>Tick at which this entity last took damage without having been re-ordered since; -1 = nothing pending.</summary>
        private readonly long[] _reactPendingTick;

        public TraceCollector(MultiSlotAiHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _slotCount = host.SlotCount;

            _lowPowerTicks = new int[_slotCount];
            _unitsLost = new int[_slotCount];
            _healthLost = new long[_slotCount];

            int capacity = host.Entities.Capacity;
            _wasActive = new bool[capacity];
            _lastOwner = new byte[capacity];
            _lastVersion = new ushort[capacity];
            _lastHealth = new int[capacity];

            _reactions = new ReactionTally[_slotCount];
            for (int i = 0; i < _slotCount; i++) _reactions[i] = new ReactionTally();
            _reactActive = new bool[capacity];
            _reactOwner = new byte[capacity];
            _reactVersion = new ushort[capacity];
            _reactHealth = new int[capacity];
            _reactOrder = new long[capacity];
            _reactPendingTick = new long[capacity];

            SnapshotEntities();
            SnapshotReactions();
            for (int i = 0; i < capacity; i++) _reactPendingTick[i] = -1;
        }

        /// <summary>
        /// The reaction bookkeeping this run accumulated, one entry per slot.
        /// Call <see cref="FinishReactions"/> before reading it at the end of a
        /// match, or the still-pending damage events go uncounted.
        /// </summary>
        public ReactionTally[] Reactions => _reactions;

        /// <summary>Per-tick accumulation. One struct read per slot, plus the reaction pass.</summary>
        public void OnTick(uint tick)
        {
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                if (_host.Economy.GetPlayerEconomy(slot).IsLowPower) _lowPowerTicks[slot]++;
            }

            TrackReactions(tick);
        }

        // ----------------------------------------------------------------
        // Reaction latency: damage -> a NEW movement order for that unit
        // ----------------------------------------------------------------

        /// <summary>
        /// Pairs "this entity lost health at tick T" with "this entity got a
        /// different valid <c>TargetGridPos</c> at tick T'". T' - T is the
        /// latency; a unit that dies or reaches the end of the match with an
        /// open pair counts as UNANSWERED.
        /// <para>
        /// Only the move target is watched, and deliberately so. Attack
        /// targets are written by the D-087 auto-acquisition without any AI
        /// involvement, so counting them would credit the AI with the combat
        /// system's reflexes. Arrival is not a re-order either: it clears the
        /// target through <c>UnitState.Stop()</c>, which lands on the invalid
        /// value and is skipped here.
        /// </para>
        /// <para>
        /// Order matters inside the loop: a re-order is matched against
        /// EARLIER damage before this tick's damage is recorded, so an order
        /// and a hit in the same tick never produce a latency of 0 — an intent
        /// is sealed a tick after it is submitted and cannot answer damage
        /// that has not happened yet.
        /// </para>
        /// </summary>
        private void TrackReactions(uint tick)
        {
            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                bool sameUnit = _reactActive[i] && u.IsActive && u.Id.Version == _reactVersion[i];

                if (!sameUnit)
                {
                    if (_reactActive[i] && _reactPendingTick[i] >= 0 && _reactOwner[i] < _slotCount)
                    {
                        _reactions[_reactOwner[i]].Unanswered++;
                    }
                    _reactPendingTick[i] = -1;
                }
                else if (u.PlayerId < _slotCount)
                {
                    long order = OrderKeyOf(in u);
                    if (order >= 0 && order != _reactOrder[i] && _reactPendingTick[i] >= 0)
                    {
                        ReactionTally tally = _reactions[u.PlayerId];
                        tally.Events++;
                        tally.LatencySumTicks += tick - _reactPendingTick[i];
                        _reactPendingTick[i] = -1;
                    }
                    else if (u.CurrentHealth < _reactHealth[i] && _reactPendingTick[i] < 0)
                    {
                        _reactPendingTick[i] = tick;
                    }
                }

                _reactActive[i] = u.IsActive;
                _reactOwner[i] = u.PlayerId;
                _reactVersion[i] = u.Id.Version;
                _reactHealth[i] = u.CurrentHealth;
                _reactOrder[i] = OrderKeyOf(in u);
            }
        }

        /// <summary>
        /// Damage still waiting for an answer when the match ends is
        /// unanswered — leaving it out would flatter a slot that simply ran
        /// out of ticks before reacting.
        /// </summary>
        public void FinishReactions()
        {
            for (int i = 0; i < _reactPendingTick.Length; i++)
            {
                if (_reactPendingTick[i] < 0 || !_reactActive[i] || _reactOwner[i] >= _slotCount) continue;
                _reactions[_reactOwner[i]].Unanswered++;
                _reactPendingTick[i] = -1;
            }
        }

        /// <summary>The standing move order as one comparable integer; -1 when the unit has none.</summary>
        private static long OrderKeyOf(in UnitState unit)
        {
            return unit.TargetGridPos.IsValid
                ? ((long)unit.TargetGridPos.Y << 16) | unit.TargetGridPos.X
                : -1L;
        }

        private void SnapshotReactions()
        {
            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                _reactActive[i] = u.IsActive;
                _reactOwner[i] = u.PlayerId;
                _reactVersion[i] = u.Id.Version;
                _reactHealth[i] = u.CurrentHealth;
                _reactOrder[i] = OrderKeyOf(in u);
            }
        }

        /// <summary>Full sample of every slot at <paramref name="tick"/>.</summary>
        public MetricSample Sample(uint tick)
        {
            var slots = new SlotMetrics[_slotCount];
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                slots[slot] = new SlotMetrics { Slot = slot };
            }

            AccumulateLosses();
            ScanEntities(slots);
            ReadEconomy(slots);
            ReadSight(slots);
            ReadIntents(slots);

            for (byte slot = 0; slot < _slotCount; slot++)
            {
                slots[slot].LowPowerTicks = _lowPowerTicks[slot];
                slots[slot].UnitsLost = _unitsLost[slot];
                slots[slot].HealthLost = _healthLost[slot];
            }

            return new MetricSample { Tick = tick, Slots = slots };
        }

        // ----------------------------------------------------------------
        // Losses: what vanished or bled since the previous sample
        // ----------------------------------------------------------------

        /// <summary>
        /// Attributes disappearances and health drops to their owner, then
        /// re-snapshots. The totals it feeds are CUMULATIVE over the match —
        /// each sample carries the value since tick 0, not since the previous
        /// sample.
        /// <para>
        /// KNOWN BLIND SPOT: a unit born and killed between two samples is
        /// invisible here. At the default interval of 10 ticks that is a second
        /// of simulated time, well below any unit's build time — but it is a
        /// sampling result, not a ledger, and reads as one.
        /// </para>
        /// <para>
        /// SECOND READING NOTE: this walks every entity the slot owns, so a
        /// destroyed building and a cancelled construction site count exactly
        /// like a dead soldier. The field is named <c>UnitsLost</c> for
        /// archive compatibility; what it measures is entities.
        /// </para>
        /// </summary>
        private void AccumulateLosses()
        {
            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                bool sameUnit = _wasActive[i] && u.IsActive && u.Id.Version == _lastVersion[i];

                if (_wasActive[i] && !sameUnit && _lastOwner[i] < _slotCount)
                {
                    // Gone: despawned, or the slot was reused by a new unit.
                    _unitsLost[_lastOwner[i]]++;
                    _healthLost[_lastOwner[i]] += _lastHealth[i];
                }
                else if (sameUnit && u.CurrentHealth < _lastHealth[i] && u.PlayerId < _slotCount)
                {
                    _healthLost[u.PlayerId] += _lastHealth[i] - u.CurrentHealth;
                }
            }

            SnapshotEntities();
        }

        private void SnapshotEntities()
        {
            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                _wasActive[i] = u.IsActive;
                _lastOwner[i] = u.PlayerId;
                _lastVersion[i] = u.Id.Version;
                _lastHealth[i] = u.CurrentHealth;
            }
        }

        // ----------------------------------------------------------------
        // The one expensive pass: ascending index scan, no dictionary order
        // ----------------------------------------------------------------

        private void ScanEntities(SlotMetrics[] slots)
        {
            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                if (!u.IsActive || u.PlayerId >= _slotCount) continue;

                SlotMetrics m = slots[u.PlayerId];
                uint raw = UnitCommandStateView.ToRawEntityId(u.Id);

                // A SITE MUST BE TESTED BEFORE THE ROLE, not after: an
                // unfinished site carries UnitRole.Unit and 1 HP and only
                // takes its definition role on completion
                // (ConstructionSystem.SpawnBuildingEntity). Asking
                // IsBuildingRole first makes the site branch unreachable and
                // sitesOpen a permanent zero.
                if (_host.Construction.TryGetSite(raw, out _, out _, out _))
                {
                    m.SitesOpen++;
                    continue;
                }

                if (SimDefinitions.IsBuildingRole(u.Role))
                {
                    m.BuildingsByRole[(int)u.Role - SlotMetrics.FirstBuildingRole]++;
                    if (_host.Production.TryGetProducer(raw, out int entryCount, out _, out _))
                    {
                        m.Producers++;
                        for (int e = 0; e < entryCount; e++)
                        {
                            if (_host.Production.TryGetQueueEntry(raw, e, out _, out ushort remaining, out _))
                            {
                                m.QueuedUnits += remaining;
                            }
                        }
                    }
                    continue;
                }

                if (u.Role == UnitRole.Harvester)
                {
                    m.Harvesters++;
                    if (u.HarvestFieldId == 0 && !u.IsReturningCargo) m.IdleHarvesters++;
                    if (u.IsReturningCargo) m.CargoInTransitAE += u.CargoAE;
                    continue;
                }

                if (u.Role >= UnitRole.BasicInfantry && u.Role <= UnitRole.Artillery)
                {
                    m.ArmySize++;
                    m.ArmyHealthSum += u.CurrentHealth;
                }
            }
        }

        private void ReadEconomy(SlotMetrics[] slots)
        {
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                ref PlayerEconomyState economy = ref _host.Economy.GetPlayerEconomy(slot);
                SlotMetrics m = slots[slot];
                m.Credits = economy.AetheriumCredits;
                m.PowerProvided = economy.PowerProvided;
                m.PowerRequired = economy.PowerRequired;
                m.IsLowPower = economy.IsLowPower ? 1 : 0;

                // The slot's own field carries the same id the opening assigned.
                ushort fieldId = CanonicalOpening.LayoutOf(slot).FieldId;
                m.FieldReserveAE = _host.Economy.TryGetField(fieldId, out AetheriumField field)
                    ? field.RemainingAE
                    : 0;
            }
        }

        /// <summary>
        /// Enemy entities as the slot's COMMITTED team view sees them — the
        /// single legal sight (AIArchitecture.md sections 1 and 6). Team ==
        /// PlayerSlot is the MS-1 simplification in FogOfWarSystem, so with no
        /// team notion every other slot is an enemy. The most common
        /// explanation for "the AI did not react" is that it could not see.
        /// </summary>
        private void ReadSight(SlotMetrics[] slots)
        {
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                _visibleScratch.Clear();
                _host.FogOfWar.GetVisibleEntities(slot, _visibleScratch);

                SlotMetrics m = slots[slot];
                for (int i = 0; i < _visibleScratch.Count; i++)
                {
                    if (!_host.Entities.TryGetUnit(_visibleScratch[i], out UnitState u)) continue;
                    if (u.PlayerId == slot) continue;

                    if (SimDefinitions.IsBuildingRole(u.Role)) m.VisibleEnemyBuildings++;
                    else m.VisibleEnemyUnits++;
                }
            }
        }

        /// <summary>
        /// Intent verdicts, counted where they happen — at the host intake.
        /// <para>
        /// Only available when the run bound the counting transport
        /// (<see cref="MatchSpec.CountIntents"/>); the fields stay at zero
        /// otherwise rather than carrying a derived guess. The derivation that
        /// suggests itself — submitted sequences minus the sealed watermark —
        /// is wrong, because the watermark is a high-water mark: a rejection
        /// mid-stream leaves a gap that later records seal straight past, and
        /// the rejection vanishes from the arithmetic.
        /// </para>
        /// <para>
        /// One thing this still cannot see: an intent the peer ingress refused
        /// BEFORE handing it to the transport (a malformed payload) never
        /// reaches a verdict here. The AI does not produce those — every
        /// payload it builds is validated by the same code the executor uses —
        /// but a future goal system might, and then this number would understate.
        /// </para>
        /// </summary>
        private void ReadIntents(SlotMetrics[] slots)
        {
            for (int i = 0; i < _host.Peers.Length; i++)
            {
                SlotPeer peer = _host.Peers[i];
                if (peer.IntentCounter == null) continue;

                SlotMetrics m = slots[peer.Slot];
                m.IntentsSubmitted = peer.IntentCounter.Submitted;
                m.IntentsAccepted = peer.IntentCounter.Accepted;
                m.IntentsRejected = peer.IntentCounter.Rejected;
            }
        }
    }
}
