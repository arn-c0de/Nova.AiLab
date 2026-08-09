using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.Economy;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;
using Nova.Simulation.Vision;

namespace Nova.AiLab
{
    /// <summary>
    /// Records view frames from the committed state (plan section 3.4).
    /// <para>
    /// WHY A PICTURE AT ALL. Numbers say THAT something went wrong, not WHAT: a
    /// win rate of 40% does not explain that half the army is stuck on a
    /// building corner. That is also why this comes before the sweep —
    /// evaluating a thousand runs helps little while a single one is
    /// unreadable.
    /// </para>
    /// <para>
    /// HARD CONDITION: pure observer. It reads the committed state after
    /// <c>StepTick()</c>, never writes back, and is not part of the tick order,
    /// the state hash or a snapshot. A run with and without the recorder must
    /// produce the identical hash chain — asserted in <c>ViewRecorderTests</c>.
    /// </para>
    /// <para>
    /// Encoded is ACTIVITY, not just position: what a unit is doing is the part
    /// that explains a bad match, and it is exactly the part a position-only
    /// dump throws away.
    /// </para>
    /// </summary>
    public sealed class ViewRecorder
    {
        /// <summary>
        /// Health share below which a unit gets a rim marker. E7 will make the
        /// retreat threshold a profile value; until then this is a drawing
        /// hint, not a rule — it marks what a retreat rule WOULD act on.
        /// </summary>
        public const int RetreatMarkerHealthPercent = 25;

        private readonly MultiSlotAiHost _host;
        private readonly int _slotCount;
        private readonly bool _recordFog;
        private readonly List<EntityId> _visibleScratch = new List<EntityId>(256);

        public ViewRecorder(MultiSlotAiHost host, bool recordFog)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _slotCount = host.SlotCount;
            _recordFog = recordFog;
        }

        public ViewFrame Capture(uint tick)
        {
            var frame = new ViewFrame { Tick = tick, Headers = new ViewSlotHeader[_slotCount] };

            var visibleEnemies = new int[_slotCount];
            var armySize = new int[_slotCount];
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                _visibleScratch.Clear();
                _host.FogOfWar.GetVisibleEntities(slot, _visibleScratch);
                for (int i = 0; i < _visibleScratch.Count; i++)
                {
                    if (_host.Entities.TryGetUnit(_visibleScratch[i], out UnitState seen) && seen.PlayerId != slot)
                    {
                        visibleEnemies[slot]++;
                    }
                }
            }

            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                if (!u.IsActive || u.PlayerId >= _slotCount) continue;

                frame.Entities.Add(BuildEntity(in u));
                if (u.Role >= UnitRole.BasicInfantry && u.Role <= UnitRole.Artillery) armySize[u.PlayerId]++;
            }

            for (byte slot = 0; slot < _slotCount; slot++)
            {
                ref PlayerEconomyState economy = ref _host.Economy.GetPlayerEconomy(slot);
                frame.Headers[slot] = new ViewSlotHeader
                {
                    Slot = slot,
                    Credits = economy.AetheriumCredits,
                    PowerMargin = economy.PowerProvided - economy.PowerRequired,
                    ArmySize = armySize[slot],
                    VisibleEnemies = visibleEnemies[slot],
                };
            }

            if (_recordFog) frame.FogRle = CaptureFog();
            return frame;
        }

        private ViewEntity BuildEntity(in UnitState u)
        {
            uint raw = UnitCommandStateView.ToRawEntityId(u.Id);
            // Site before role: an unfinished site carries UnitRole.Unit and
            // 1 HP until it completes (ConstructionSystem.SpawnBuildingEntity).
            // Testing the role first would draw every site as a combat unit.
            bool isSite = _host.Construction.TryGetSite(raw, out ushort siteDefId, out int progressRaw, out _);

            ViewShape shape;
            if (isSite) shape = ViewShape.ConstructionSite;
            else if (SimDefinitions.IsBuildingRole(u.Role)) shape = ViewShape.Building;
            else if (u.Role == UnitRole.Builder) shape = ViewShape.Builder;
            else if (u.Role == UnitRole.Harvester) shape = ViewShape.Harvester;
            else shape = ViewShape.Combat;

            // Brightness is health, EXCEPT on a site: a site sits at 1 HP for
            // its whole life, so health there would encode nothing. Build
            // progress is the number that answers "is this thing coming up or
            // stalled?" — the same channel, the meaningful quantity.
            int healthPercent;
            if (isSite)
            {
                healthPercent = SimDefinitions.TryGetBuilding(siteDefId, out SimBuildingDefinition siteDef)
                                && siteDef.BuildTicks > 0
                    ? Math.Min(100, (int)((long)(progressRaw >> 16) * 100 / siteDef.BuildTicks))
                    : 0;
            }
            else
            {
                healthPercent = u.MaxHealth > 0 ? u.CurrentHealth * 100 / u.MaxHealth : 0;
            }

            int flags = 0;
            if (u.IsReturningCargo) flags |= ViewFlags.ReturningCargo;
            if (u.IsMoving) flags |= ViewFlags.Moving;
            if (shape == ViewShape.Combat && healthPercent < RetreatMarkerHealthPercent)
            {
                flags |= ViewFlags.BelowRetreatThreshold;
            }

            var entity = new ViewEntity
            {
                Slot = u.PlayerId,
                Shape = shape,
                XRaw = u.Transform.PositionX.RawValue,
                YRaw = u.Transform.PositionY.RawValue,
                HealthPercent = healthPercent,
                Flags = flags,
                Line = ViewLine.None,
            };

            // Priority is the order the plan lists: an attack order says more
            // about what a unit is doing than the move that carries it there.
            if (u.AttackTarget.IsValid && _host.Entities.TryGetUnit(u.AttackTarget, out UnitState target))
            {
                entity.Line = ViewLine.Attack;
                entity.LineXRaw = target.Transform.PositionX.RawValue;
                entity.LineYRaw = target.Transform.PositionY.RawValue;
            }
            else if (u.HarvestFieldId != 0 && _host.Economy.TryGetField(u.HarvestFieldId, out AetheriumField field))
            {
                entity.Line = ViewLine.Harvest;
                entity.LineXRaw = SimFixed.FromInt(field.GridPos.X).RawValue;
                entity.LineYRaw = SimFixed.FromInt(field.GridPos.Y).RawValue;
            }
            else if (u.IsMoving)
            {
                entity.Line = ViewLine.Move;
                entity.LineXRaw = SimFixed.FromInt(u.GoalGridPos.X).RawValue;
                entity.LineYRaw = SimFixed.FromInt(u.GoalGridPos.Y).RawValue;
            }

            return entity;
        }

        /// <summary>
        /// Fog per slot, run-length encoded. The single most common
        /// explanation for "the AI did not react" is that it could not see —
        /// which is invisible in every other artifact the lab writes.
        /// </summary>
        private int[][] CaptureFog()
        {
            var perSlot = new int[_slotCount][];
            for (byte slot = 0; slot < _slotCount; slot++)
            {
                TeamView view = _host.FogOfWar.GetTeamView(slot);
                var runs = new List<int>(256);

                int currentState = -1;
                int runLength = 0;
                for (int y = 0; y < view.Height; y++)
                {
                    for (int x = 0; x < view.Width; x++)
                    {
                        int state = (int)view.GetCellState(x, y);
                        if (state == currentState)
                        {
                            runLength++;
                            continue;
                        }
                        if (currentState >= 0)
                        {
                            runs.Add(runLength);
                            runs.Add(currentState);
                        }
                        currentState = state;
                        runLength = 1;
                    }
                }
                if (currentState >= 0)
                {
                    runs.Add(runLength);
                    runs.Add(currentState);
                }

                perSlot[slot] = runs.ToArray();
            }
            return perSlot;
        }
    }
}
