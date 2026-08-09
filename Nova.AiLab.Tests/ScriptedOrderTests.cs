using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Definitions;
using Nova.Simulation.State;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// Does a scripted slot's order actually arrive and move a unit?
    /// <para>
    /// This exists because the duel arena's long echelon reported zero
    /// rejections and zero movement — a combination that means the setup is
    /// broken somewhere between the intent and the mover, and no aggregate
    /// number can tell you where.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ScriptedOrderTests
    {
        private static MultiSlotAiHost BuildScriptedHost()
        {
            var spec = new MatchSpec
            {
                Seed = 0xA17E57DE57UL,
                TickBudget = 1000,
                EntityCapacity = 256,
                CountIntents = true,
                Slots = new[]
                {
                    new SlotSpec { Slot = 0, Faction = FactionId.Alliance, Controller = SlotController.Scripted },
                    new SlotSpec { Slot = 1, Faction = FactionId.Legion, Controller = SlotController.Scripted },
                },
            };
            return MultiSlotAiHost.Build(spec);
        }

        [Test]
        public void AScriptedMoveOrderReachesTheExecutorAndMovesTheUnit()
        {
            MultiSlotAiHost host = BuildScriptedHost();

            Assert.That(SimDefinitions.TryGetUnit(FactionId.Alliance, UnitRole.BasicInfantry,
                out SimUnitDefinition def), Is.True);

            EntityId id = host.Entities.SpawnUnit(
                0,
                new Transform2D(SimFixed.FromInt(40), SimFixed.FromInt(64)),
                def.MoveSpeed,
                maxHealth: def.MaxHealth,
                role: def.Role);

            int startX = SimFixed.WorldToGrid(host.Entities.RawUnits[id.Index].Transform.PositionX);

            SlotPeer peer = host.PeerOf(0);
            Assert.That(peer, Is.Not.Null, "a scripted slot must own a command seat");

            var ids = new[] { UnitCommandStateView.ToRawEntityId(id) };
            peer.Ingress.TrySubmitIntent(
                CommandIntent.Create(new MovePayload(ids, SimFixed.FromInt(80), SimFixed.FromInt(64))),
                out _);

            host.Run(200);

            Assert.That(peer.IntentCounter.Submitted, Is.EqualTo(1), "the order must reach the host intake");
            Assert.That(peer.IntentCounter.Rejected, Is.EqualTo(0),
                $"the host refused the move order ({peer.IntentCounter.LastRejectReason})");

            ref readonly UnitState unit = ref host.Entities.RawUnits[id.Index];
            int endX = SimFixed.WorldToGrid(unit.Transform.PositionX);

            TestContext.Out.WriteLine(
                $"[scripted] x {startX} -> {endX}, IsMoving {unit.IsMoving}, goal {unit.GoalGridPos.X}/{unit.GoalGridPos.Y}");

            Assert.That(endX, Is.GreaterThan(startX),
                "a scripted move order must actually move the unit — orders travel the canonical sealed path");
        }

        [Test]
        public void TwoScriptedSidesWalkIntoEachOtherAndFight()
        {
            MultiSlotAiHost host = BuildScriptedHost();

            var raws = new List<uint>[2] { new List<uint>(), new List<uint>() };
            for (byte slot = 0; slot < 2; slot++)
            {
                FactionId faction = slot == 0 ? FactionId.Alliance : FactionId.Legion;
                SimDefinitions.TryGetUnit(faction, UnitRole.BasicInfantry, out SimUnitDefinition def);
                for (int i = 0; i < 4; i++)
                {
                    EntityId id = host.Entities.SpawnUnit(
                        slot,
                        new Transform2D(SimFixed.FromInt(slot == 0 ? 47 : 81), SimFixed.FromInt(62 + i)),
                        def.MoveSpeed, maxHealth: def.MaxHealth, role: def.Role);
                    raws[slot].Add(UnitCommandStateView.ToRawEntityId(id));
                }
            }

            for (byte slot = 0; slot < 2; slot++)
            {
                // Both sides walk to the MIDPOINT between the formations, not
                // onto the other's start cell: sent to each other's position
                // they simply swap places and end up as far apart as they
                // began, which is what the first duel table actually measured.
                host.PeerOf(slot).Ingress.TrySubmitIntent(
                    CommandIntent.Create(new MovePayload(
                        raws[slot].ToArray(), SimFixed.FromInt(64), SimFixed.FromInt(64))),
                    out _);
            }

            long healthBefore = TotalHealth(host);
            host.Run(1200);
            long healthAfter = TotalHealth(host);

            TestContext.Out.WriteLine($"[scripted] total health {healthBefore} -> {healthAfter}");
            Assert.That(healthAfter, Is.LessThan(healthBefore),
                "two sides ordered into each other must actually fight — auto-acquisition (D-087) " +
                "takes over once they see each other in range");
        }

        private static long TotalHealth(MultiSlotAiHost host)
        {
            long total = 0;
            UnitState[] units = host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].IsActive) total += units[i].CurrentHealth;
            }
            return total;
        }
    }
}
