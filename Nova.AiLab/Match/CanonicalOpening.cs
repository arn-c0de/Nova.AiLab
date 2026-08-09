using System;
using Nova.Core;
using Nova.Simulation.Definitions;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// The D-077 opening position, mirrored from <c>MatchBootstrap.SetupSlot</c>
    /// (plan E1: "Startaufstellung exakt kanonisch"): per slot one Aetherium
    /// field, a completed HQ and ONE Builder — spawn order field, HQ, Builder,
    /// slot 0 first.
    /// <para>
    /// The spawn ORDER is load-bearing, not cosmetic: EntityManager hands out
    /// ids from a deterministic free list, so any reordering shifts every id
    /// and with it every state hash and snapshot.
    /// </para>
    /// <para>
    /// Units spawn WITH definition stats (move speed and maxHealth from
    /// <see cref="SimDefinitions"/>), matching MatchBootstrap's default
    /// <c>UseDefinitionStats = true</c> and SkirmishAiTests — NOT the
    /// definition-free spawn of Determinism10000Scenario, which stamps the
    /// SpawnUnit default maxHealth of 100. The lab measures the AI, so it
    /// starts from the game's opening, not the scenario's.
    /// </para>
    /// </summary>
    public static class CanonicalOpening
    {
        /// <summary>Aetherium reserve per field, in AE (MatchBootstrap / Determinism10000Scenario).</summary>
        public const long FieldReserveAE = 2000000L;

        /// <summary>Fixed opening layout of one slot, in grid cells.</summary>
        public sealed class SlotLayout
        {
            public ushort FieldId;
            public int FieldX, FieldY;
            public int HqOriginX, HqOriginY;
            public int BuilderX, BuilderY;
        }

        /// <summary>
        /// Corner seats on the canonical 128x128 map. Slots 0 and 1 are the
        /// byte-exact MatchBootstrap layouts (bottom-left and top-right); the
        /// HQ footprint is 3x3 (<see cref="SimDefinitions.BuildingFootprintCells"/>),
        /// so slot 0 spans 4..6 with its field on the outer corner at (7,7) and
        /// slot 1 spans 120..122 with its field at (119,119) — the same shape
        /// reflected through x' = 126 - x.
        /// <para>
        /// Slots 2 and 3 apply that same reflection per axis to reach the two
        /// remaining corners. They are lab seats, not canonical ones: only the
        /// two-slot match exists in the game today (MatchConfig forbids more
        /// than one AI slot), so nothing outside this file has an opinion about
        /// where a third base stands.
        /// </para>
        /// </summary>
        private static readonly SlotLayout[] CornerLayouts =
        {
            // slot 0 — bottom-left, MatchBootstrap.LocalLayout
            new SlotLayout { FieldId = 1, FieldX = 7,   FieldY = 7,   HqOriginX = 4,   HqOriginY = 4,   BuilderX = 13,  BuilderY = 7 },
            // slot 1 — top-right, MatchBootstrap.EnemyLayout
            new SlotLayout { FieldId = 2, FieldX = 119, FieldY = 119, HqOriginX = 120, HqOriginY = 120, BuilderX = 113, BuilderY = 119 },
            // slot 2 — top-left (slot 0 reflected in y)
            new SlotLayout { FieldId = 3, FieldX = 7,   FieldY = 119, HqOriginX = 4,   HqOriginY = 120, BuilderX = 13,  BuilderY = 119 },
            // slot 3 — bottom-right (slot 0 reflected in x)
            new SlotLayout { FieldId = 4, FieldX = 119, FieldY = 7,   HqOriginX = 120, HqOriginY = 4,   BuilderX = 113, BuilderY = 7 },
        };

        /// <summary>Highest slot count the canonical corner seating covers.</summary>
        public const int MaxSeatedSlots = 4;

        public static SlotLayout LayoutOf(byte slot)
        {
            if (slot >= MaxSeatedSlots)
            {
                throw new NotSupportedException(
                    $"no opening seat is defined for slot {slot}: the map has {MaxSeatedSlots} corner seats. " +
                    "More seats are map work (plan E11), not a harness change — the host itself carries up to " +
                    $"{MultiSlotAiHost.MaxSlots} slots.");
            }
            return CornerLayouts[slot];
        }

        /// <summary>
        /// Applies the opening for every slot of <paramref name="host"/> in
        /// ascending slot order.
        /// </summary>
        public static void Apply(MultiSlotAiHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            for (byte slot = 0; slot < host.SlotCount; slot++)
            {
                SlotLayout c = LayoutOf(slot);

                if (!host.Economy.TryAddField(c.FieldId, new GridPos2D(c.FieldX, c.FieldY), FieldReserveAE))
                {
                    throw new InvalidOperationException($"[AiLab] field {c.FieldId} could not be registered");
                }

                FactionId faction = host.Economy.GetSlotFaction(slot);
                ushort hqDefId = SimDefinitions.ToDefinitionId(faction, UnitRole.HQ);
                if (!host.Construction.PlaceCompletedBuilding(slot, hqDefId, c.HqOriginX, c.HqOriginY).IsValid)
                {
                    throw new InvalidOperationException($"[AiLab] HQ placement failed for slot {slot}");
                }

                if (!SimDefinitions.TryGetUnit(faction, UnitRole.Builder, out SimUnitDefinition builderDef))
                {
                    throw new InvalidOperationException($"[AiLab] unknown unit definition ({faction}, Builder)");
                }

                host.Entities.SpawnUnit(
                    slot,
                    new Transform2D(SimFixed.FromInt(c.BuilderX), SimFixed.FromInt(c.BuilderY)),
                    builderDef.MoveSpeed,
                    maxHealth: builderDef.MaxHealth,
                    role: builderDef.Role);
            }
        }
    }
}
