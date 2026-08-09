using System.Collections.Generic;
using NUnit.Framework;
using Nova.AI;
using Nova.Core;
using Nova.Simulation;
using Nova.Simulation.Combat;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Construction;
using Nova.Simulation.Definitions;
using Nova.Simulation.Economy;
using Nova.Simulation.Movement;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.Production;
using Nova.Simulation.State;
using Nova.Simulation.Victory;
using Nova.Simulation.Vision;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// E1 acceptance suite of the AI lab. It answers exactly one question:
    /// <b>is <see cref="MultiSlotAiHost"/> still the match the game plays?</b>
    /// A lab whose harness has drifted measures something that does not exist,
    /// and it would keep reporting clean numbers while doing it.
    /// <para>
    /// The proof does not lean on any recorded constant. A
    /// <see cref="ReferenceAiHost"/> below is a hand-mirrored copy of the
    /// <c>AiHost</c> in <c>tools/Nova.SimRunner.Tests/SkirmishAiTests.cs</c>,
    /// which documents itself as a "byte-exact wiring mirror of
    /// MatchRunner.InitializeMatch" — the same hand-mirror technique
    /// <c>CanonicalMatchSetupTests</c> uses to make two lanes meet in the
    /// middle. The lab host must produce the same state hash as that
    /// reference at tick 0, mid-match and at the decision. Any wiring drift —
    /// a reordered system, a changed session, a different opening — moves the
    /// hash and fails here.
    /// </para>
    /// <para>
    /// LOCAL LAB SUITE, not a repository contribution: it lives beside the lab
    /// and is deleted with it. PR tests follow the SkirmishAiTests pattern and
    /// never depend on this project (plan section 0, rule 2).
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MultiSlotAiHostTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const byte PassiveSlot = 0;
        private const byte AiSlot = 1;
        private const ushort MapWidth = 128;
        private const ushort MapHeight = 128;
        private const int EntityCapacity = 1024;
        private const long FieldReserveAE = 2000000L;
        private const int EndToEndBudgetTicks = 6000;

        /// <summary>
        /// The canonical tick order without the AI (SimulationCore.md section 2)
        /// — the identical list <c>CanonicalMatchSetupTests.CanonicalTickOrder</c>
        /// pins the game and the determinism harness against. Runtime type full
        /// names, so a wrapper subclass is rejected rather than silently
        /// accepted.
        /// </summary>
        private static readonly string[] CanonicalG1Order =
        {
            "Nova.Simulation.Economy.EconomySystem",
            "Nova.Simulation.Construction.ConstructionSystem",
            "Nova.Simulation.Production.ProductionSystem",
            "Nova.Simulation.Pathfinding.PathfindingSystem",
            "Nova.Simulation.Movement.MovementSystem",
            "Nova.Simulation.Vision.FogOfWarSystem",
            "Nova.Simulation.Combat.CombatSystem",
            "Nova.Simulation.Victory.VictorySystem",
        };

        private const string AiSystemName = "Nova.AI.SkirmishAiSystem";

        // ================================================================
        // The reference: SkirmishAiTests.AiHost, hand-mirrored
        // ================================================================

        private sealed class ReferenceAiHost
        {
            public SimulationKernel Kernel;
            public EntityManager Entities;
            public EconomySystem Economy;
            public ConstructionSystem Construction;
            public VictorySystem Victory;
            public MatchSession Session;
            public CommandIngress Ingress;
            public MatchSession AiSession;

            public void Step()
            {
                uint nextTick = Kernel.CurrentTick.Value + 1;
                CommandBatch batch = Ingress.SealTickBatch(nextTick);
                if (batch.Count > 0)
                {
                    Assert.That(Kernel.SubmitBatch(batch), Is.True,
                        $"kernel refused the sealed batch of tick {nextTick}");
                }
                AiSession.AdvanceTick();
                Kernel.StepTick();
                Session.AdvanceTick();
            }

            public void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++) Step();
            }

            public uint RunUntilDecided(int budgetTicks)
            {
                for (int i = 0; i < budgetTicks && !Victory.IsDecided; i++) Step();
                return Victory.DecidedTick;
            }
        }

        private static ReferenceAiHost BuildReferenceHost(ulong seed)
        {
            var kernel = new SimulationKernel(new SimRandom(seed));

            var entities = new EntityManager(EntityCapacity);
            var pathfinding = new PathfindingSystem(MapWidth, MapHeight);
            var movement = new MovementSystem(entities, pathfinding);
            var economy = new EconomySystem(entities, EconomySystem.CanonicalMatchStartingCreditsAE);
            var construction = new ConstructionSystem(entities, economy, pathfinding.CostField);
            var production = new ProductionSystem(entities, economy, construction);
            var fogOfWar = new FogOfWarSystem(entities, construction, teamCount: 2, MapWidth, MapHeight);
            var combat = new CombatSystem(entities, fogOfWar, economy);
            var victory = new VictorySystem(entities, construction);

            var session = new MatchSession(PassiveSlot, activeSlots: new byte[] { PassiveSlot, AiSlot }, inputDelayTicks: 1);
            var ingress = new CommandIngress(session);
            _ = new LocalLoopbackTransport(ingress);

            var aiSession = new MatchSession(AiSlot, activeSlots: new byte[] { PassiveSlot, AiSlot }, inputDelayTicks: 1);
            var aiIngress = new CommandIngress(aiSession);
            _ = new AiPeerCommandTransport(aiIngress, ingress);
            var ai = new SkirmishAiSystem(
                AiSlot,
                new AiFactionProfile("Legion",
                    targetPowerMargin: 0, targetArmySize: 12, attackSquadThreshold: 6, targetHarvesterCount: 2),
                aiIngress, entities, economy, construction, production, fogOfWar, victory);

            kernel.RegisterSystem(economy);
            kernel.RegisterSystem(construction);
            kernel.RegisterSystem(production);
            kernel.RegisterSystem(pathfinding);
            kernel.RegisterSystem(movement);
            kernel.RegisterSystem(fogOfWar);
            kernel.RegisterSystem(combat);
            kernel.RegisterSystem(ai);
            kernel.RegisterSystem(victory);
            kernel.BindCommands(
                new UnitCommandStateView(entities, pathfinding, economy, construction, production), ingress);

            economy.SetSlotFaction(PassiveSlot, FactionId.Alliance);
            economy.SetSlotFaction(AiSlot, FactionId.Legion);

            kernel.Start();
            return new ReferenceAiHost
            {
                Kernel = kernel,
                Entities = entities,
                Economy = economy,
                Construction = construction,
                Victory = victory,
                Session = session,
                Ingress = ingress,
                AiSession = aiSession,
            };
        }

        private static void ApplyReferenceOpening(ReferenceAiHost host)
        {
            for (byte slot = 0; slot < 2; slot++)
            {
                ushort fieldId = (ushort)(slot + 1);
                int fieldCell = slot == PassiveSlot ? 7 : 119;
                int hqOrigin = slot == PassiveSlot ? 4 : 120;
                int builderX = slot == PassiveSlot ? 13 : 113;
                int builderY = slot == PassiveSlot ? 7 : 119;

                Assert.That(host.Economy.TryAddField(fieldId, new GridPos2D(fieldCell, fieldCell), FieldReserveAE),
                    Is.True, $"field {fieldId} could not be registered");

                FactionId faction = host.Economy.GetSlotFaction(slot);
                ushort hqDefId = SimDefinitions.ToDefinitionId(faction, UnitRole.HQ);
                Assert.That(host.Construction.PlaceCompletedBuilding(slot, hqDefId, hqOrigin, hqOrigin).IsValid,
                    Is.True, $"HQ placement failed for slot {slot}");

                Assert.That(SimDefinitions.TryGetUnit(faction, UnitRole.Builder, out SimUnitDefinition builderDef), Is.True);
                host.Entities.SpawnUnit(
                    slot,
                    new Transform2D(SimFixed.FromInt(builderX), SimFixed.FromInt(builderY)),
                    builderDef.MoveSpeed,
                    maxHealth: builderDef.MaxHealth,
                    role: UnitRole.Builder);
            }
        }

        private static ReferenceAiHost BuildReferenceMatch(ulong seed)
        {
            ReferenceAiHost host = BuildReferenceHost(seed);
            ApplyReferenceOpening(host);
            return host;
        }

        // ================================================================
        // Lab-side helpers
        // ================================================================

        /// <summary>
        /// The lab spec that reproduces SkirmishAiTests: slot 0 Alliance and
        /// PASSIVE (the fixture nobody commands), slot 1 Legion played by the
        /// skirmish AI.
        /// </summary>
        private static MatchSpec ReferenceSpec(ulong seed)
        {
            MatchSpec spec = new MatchSpec { Seed = seed };
            spec.Slots = MatchSpec.DefaultSlots(2);
            spec.Slots[PassiveSlot].Controller = SlotController.Passive;
            return spec;
        }

        private static string[] SystemTypeNames(SimulationKernel kernel)
        {
            var names = new List<string>(kernel.Systems.Count);
            for (int i = 0; i < kernel.Systems.Count; i++)
            {
                names.Add(kernel.Systems[i].GetType().FullName);
            }
            return names.ToArray();
        }

        // ================================================================
        // (a) TICK ORDER — the contract that determinism hangs on
        // ================================================================

        [Test]
        public void TwoSlotHost_RegistersTheCanonicalOrderWithTheAiBetweenCombatAndVictory()
        {
            MultiSlotAiHost host = MultiSlotAiHost.Build(ReferenceSpec(Seed));

            var expected = new List<string>(CanonicalG1Order);
            expected.Insert(expected.Count - 1, AiSystemName);

            Assert.That(SystemTypeNames(host.Kernel), Is.EqualTo(expected.ToArray()),
                "the lab must register the canonical G1 order with the AI between combat and victory — " +
                "MatchRunner's order, and the order CanonicalMatchSetupTests pins the game against. " +
                "New systems are INSERTED by agreement, never appended silently.");
        }

        [Test]
        public void FourSlotHost_RegistersOneAiPerSlotInAscendingOrder_StillBetweenCombatAndVictory()
        {
            MultiSlotAiHost host = MultiSlotAiHost.Build(new MatchSpec { Seed = Seed, Slots = MatchSpec.DefaultSlots(4) });

            var expected = new List<string>(CanonicalG1Order);
            for (int i = 0; i < 4; i++) expected.Insert(expected.Count - 1, AiSystemName);

            Assert.That(SystemTypeNames(host.Kernel), Is.EqualTo(expected.ToArray()),
                "N AI slots must occupy the single AI position of the canonical order, not scatter through it");

            for (int i = 0; i < host.Peers.Length; i++)
            {
                Assert.That(host.Peers[i].Slot, Is.EqualTo((byte)i),
                    "AI peers must tick in ascending slot order: the order fixes which intents reach a tick first");
            }
        }

        [Test]
        public void LabHost_MatchesTheReferenceWiringExactly()
        {
            MultiSlotAiHost lab = MultiSlotAiHost.Build(ReferenceSpec(Seed));
            ReferenceAiHost reference = BuildReferenceHost(Seed);

            Assert.That(SystemTypeNames(lab.Kernel), Is.EqualTo(SystemTypeNames(reference.Kernel)),
                "the lab host and the hand-mirrored MatchRunner reference must register identically");
        }

        // ================================================================
        // (b) OPENING POSITION — same match, byte for byte
        // ================================================================

        [Test]
        public void LabOpeningPosition_HashesIdenticalToTheReferenceOpening()
        {
            MultiSlotAiHost lab = MultiSlotAiHost.BuildMatch(ReferenceSpec(Seed));
            ReferenceAiHost reference = BuildReferenceMatch(Seed);

            ulong labHash = lab.Kernel.CalculateStateHash();
            ulong referenceHash = reference.Kernel.CalculateStateHash();

            Assert.That(labHash, Is.EqualTo(referenceHash),
                $"the lab opening drifted from the canonical one " +
                $"(lab 0x{labHash:X16}, reference 0x{referenceHash:X16}). " +
                "Spawn ORDER is load-bearing: entity ids come from a deterministic free list, " +
                "so any reordering shifts every id and every hash.");
        }

        [Test]
        public void LabOpeningPosition_IsSeedSensitive()
        {
            // Sanity: the hash actually covers the PRNG words, so the equality
            // above is not trivially true for any seed.
            MultiSlotAiHost canonical = MultiSlotAiHost.BuildMatch(ReferenceSpec(Seed));
            MultiSlotAiHost other = MultiSlotAiHost.BuildMatch(ReferenceSpec(Seed ^ 1UL));

            Assert.That(other.Kernel.CalculateStateHash(),
                Is.Not.EqualTo(canonical.Kernel.CalculateStateHash()));
        }

        // ================================================================
        // (c) THE PLAYED MATCH — identical, tick for tick
        // ================================================================

        [Test]
        public void LabHost_ReproducesTheReferenceMatchTickForTick()
        {
            MultiSlotAiHost lab = MultiSlotAiHost.BuildMatch(ReferenceSpec(Seed));
            ReferenceAiHost reference = BuildReferenceMatch(Seed);

            // Every 100 ticks, not just at the end: a chain that diverges and
            // reconverges would slip through an end-state comparison, and that
            // is exactly the shape a wiring bug takes.
            for (int block = 0; block < 20; block++)
            {
                lab.Run(100);
                reference.Run(100);

                Assert.That(lab.Kernel.CalculateStateHash(), Is.EqualTo(reference.Kernel.CalculateStateHash()),
                    $"lab and reference diverged at tick {lab.Kernel.CurrentTick.Value}");
            }
        }

        [Test]
        public void LabHost_DecidesTheReferenceMatchIdentically()
        {
            MultiSlotAiHost lab = MultiSlotAiHost.BuildMatch(ReferenceSpec(Seed));
            ReferenceAiHost reference = BuildReferenceMatch(Seed);

            lab.RunUntilDecided(EndToEndBudgetTicks);
            uint referenceDecided = reference.RunUntilDecided(EndToEndBudgetTicks);

            Assert.That(lab.Victory.IsDecided, Is.True,
                $"the AI must finish the reference match within {EndToEndBudgetTicks} ticks");
            Assert.That(lab.Victory.Outcome, Is.EqualTo(MatchOutcome.VictoryElimination));
            Assert.That(lab.Victory.WinnerSlot, Is.EqualTo(AiSlot),
                "the AI on slot 1 must beat the passive fixture on slot 0");
            Assert.That(lab.Victory.DecidedTick, Is.EqualTo(referenceDecided),
                "lab and reference must decide on the same tick");
            Assert.That(lab.Kernel.CalculateStateHash(), Is.EqualTo(reference.Kernel.CalculateStateHash()),
                "and reach the identical end state");

            TestContext.Out.WriteLine(
                $"[AiLab] reference match decided at tick {lab.Victory.DecidedTick}, " +
                $"end state 0x{lab.Kernel.CalculateStateHash():X16}");
        }

        // ================================================================
        // (d) AI vs AI — the reason the lab exists
        // ================================================================

        [Test]
        public void AiVersusAi_PlaysAFullMatchAndDecidesIt()
        {
            MatchRunResult result = MatchRun.Execute(new MatchSpec { Seed = Seed });

            Assert.That(result.AiSlotCount, Is.EqualTo(2),
                "both slots must be played by a skirmish AI — this is what the game itself forbids and the lab allows");
            Assert.That(result.IsDecided, Is.True,
                $"an AI-vs-AI match must reach a decision inside the canonical budget of {result.TickBudget} ticks");
            Assert.That(result.DecidedTick, Is.GreaterThan(0u));

            TestContext.Out.WriteLine(
                $"[AiLab] AI vs AI: {result.Outcome}, winner slot {result.WinnerSlot}, " +
                $"decided at tick {result.DecidedTick}, end state 0x{result.FinalStateHash:X16}");
        }

        // ================================================================
        // (e) DETERMINISM — two runs of one spec are one run
        // ================================================================

        [Test]
        public void SameSpec_ProducesIdenticalHashChains()
        {
            var spec = new MatchSpec { Seed = Seed, HashIntervalTicks = 100 };

            MatchRunResult first = MatchRun.Execute(spec);
            MatchRunResult second = MatchRun.Execute(spec);

            Assert.That(second.HashChain.Count, Is.EqualTo(first.HashChain.Count));
            Assert.That(first.HashChain.Count, Is.GreaterThan(1),
                "the chain must actually contain entries, otherwise the comparison proves nothing");

            for (int i = 0; i < first.HashChain.Count; i++)
            {
                Assert.That(second.HashChain[i].StateHash, Is.EqualTo(first.HashChain[i].StateHash),
                    $"the hash chains diverge at tick {first.HashChain[i].Tick}");
            }

            Assert.That(second.FinalStateHash, Is.EqualTo(first.FinalStateHash));
            Assert.That(second.DecidedTick, Is.EqualTo(first.DecidedTick));
        }

        [Test]
        public void FourSlotFreeForAll_RunsAndIsDeterministic()
        {
            // N-slot capability is a build property, exercised here so it does
            // not rot until E11 needs it. The canonical map seats four bases;
            // the AI's "farthest field is the enemy base" assumption is written
            // for two, so the OUTCOME of a four-slot match is not evidence of
            // anything (plan decision 13) — the reproducibility is.
            var spec = new MatchSpec { Seed = Seed, Slots = MatchSpec.DefaultSlots(4), HashIntervalTicks = 500 };

            MatchRunResult first = MatchRun.Execute(spec);
            MatchRunResult second = MatchRun.Execute(spec);

            Assert.That(first.AiSlotCount, Is.EqualTo(4));
            Assert.That(second.FinalStateHash, Is.EqualTo(first.FinalStateHash));
            Assert.That(second.FinalTick, Is.EqualTo(first.FinalTick));

            TestContext.Out.WriteLine(
                $"[AiLab] 4-slot free-for-all: {first.Outcome} at tick {first.FinalTick}, " +
                $"end state 0x{first.FinalStateHash:X16}");
        }

        // ================================================================
        // (f) GUARDS on the harness contract
        // ================================================================

        [Test]
        public void Host_RefusesASlotListThatIsNotDenseAndAscending()
        {
            var spec = new MatchSpec { Slots = MatchSpec.DefaultSlots(2) };
            spec.Slots[0].Slot = 1;
            spec.Slots[1].Slot = 0;

            Assert.Throws<System.ArgumentException>(() => MultiSlotAiHost.Build(spec),
                "slot order fixes entity ids and every hash; a shuffled list must be refused, not silently sorted");
        }

        [Test]
        public void Opening_RefusesASlotWithoutASeatOnTheMap()
        {
            Assert.Throws<System.NotSupportedException>(
                () => CanonicalOpening.LayoutOf(CanonicalOpening.MaxSeatedSlots),
                "the harness carries eight slots, the canonical map seats four — the gap must be loud");
        }
    }
}
