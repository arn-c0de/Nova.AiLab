using System;
using System.Collections.Generic;
using Nova.AI;
using Nova.Core;
using Nova.Simulation;
using Nova.Simulation.Combat;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Construction;
using Nova.Simulation.Economy;
using Nova.Simulation.Movement;
using Nova.Simulation.Pathfinding;
using Nova.Simulation.Production;
using Nova.Simulation.State;
using Nova.Simulation.Victory;
using Nova.Simulation.Vision;

namespace Nova.AiLab
{
    /// <summary>
    /// One commanding slot's session sidecar: its own seat in the command
    /// path. An AI slot carries a <see cref="System"/>; a scripted slot has
    /// the identical seat with none, so a scenario submits through it.
    /// </summary>
    public sealed class SlotPeer
    {
        public byte Slot;
        public MatchSession Session;
        public CommandIngress Ingress;

        /// <summary>Null on a scripted slot — nothing decides on its own there.</summary>
        public SkirmishAiSystem System;

        /// <summary>The canonical transport — set unless the run counts intents.</summary>
        public AiPeerCommandTransport Transport;

        /// <summary>
        /// The counting stand-in, set only when <see cref="MatchSpec.CountIntents"/>
        /// is on. Exactly one of the two transports is bound per peer.
        /// </summary>
        public CountingAiPeerTransport IntentCounter;
    }

    /// <summary>
    /// The lab's match host: <c>MatchRunner.InitializeMatch</c> generalized
    /// from one AI slot to N (2..8), and nothing else.
    /// <para>
    /// It is built from the <see cref="AiHost"/> of
    /// <c>tools/Nova.SimRunner.Tests/SkirmishAiTests.cs</c>, which documents
    /// itself as a "byte-exact wiring mirror of MatchRunner.InitializeMatch".
    /// The mirror is the whole point: the lab is only worth running while it
    /// is the same match the game plays. Two things carry that and must not
    /// drift:
    /// </para>
    /// <list type="number">
    /// <item><b>The registration order is contract</b>
    /// (13-15_Parallelbetrieb.md, "Neue Systeme — wer die Tick-Reihenfolge
    /// setzt"): Economy, Construction, Production, Pathfinding, Movement,
    /// FogOfWar, Combat, [AI slots ascending], Victory. Determinism depends
    /// not only on WHAT a system computes but WHEN. The AI sits between combat
    /// and victory, so its decisions read the post-combat state and victory
    /// still judges last. <c>MultiSlotAiHostTests</c> pins this against the
    /// same canonical list <c>CanonicalMatchSetupTests</c> pins the game
    /// against.</item>
    /// <item><b>Every AI is a session PEER, not a local intent source</b>
    /// (AIArchitecture.md section 1): each AI slot owns a slot-bound
    /// <see cref="MatchSession"/> and <see cref="CommandIngress"/>, and an
    /// <see cref="AiPeerCommandTransport"/> forwards its sealed records into
    /// the ONE host ingress — the same validating intake a network peer's
    /// records pass. The AI never picks its own slot, sequence or target tick.
    /// The transport contract is used here, never changed (Arbeitsvertrag
    /// section 2, contract 1).</item>
    /// </list>
    /// <para>
    /// ISOLATION (plan section 3.1): every match builds kernel, entity manager
    /// and all systems fresh. Only immutable data is shared — SimDefinitions,
    /// WeaponProfiles and DamageMatrix are static readonly — so N matches run
    /// on N cores without locks. The E4 sampling double-runs guard that this
    /// stays true.
    /// </para>
    /// <para>
    /// Deliberate generalizations, both invisible at two slots: the host seats
    /// N sessions instead of two, and <see cref="FogOfWarSystem"/> is built
    /// with one team per slot instead of the hard-coded 2 (team == PlayerSlot
    /// is the MS-1 simplification in FogOfWarSystem; a real team notion is
    /// blocked — plan section 6).
    /// </para>
    /// </summary>
    public sealed class MultiSlotAiHost
    {
        /// <summary>Hard slot ceiling of the command contract (CommandLimits.ReservedPlayerSlots = 8).</summary>
        public const int MaxSlots = CommandLimits.ReservedPlayerSlots;

        /// <summary>
        /// The seat the host ingress belongs to — MatchRunner's LocalSlot. In
        /// an all-AI lab match nobody submits through it: it is the ingress
        /// owner and the tick clock of the host, exactly as slot 0 is the
        /// passive fixture in SkirmishAiTests.
        /// <para>
        /// ONE DEVIATION FROM MatchRunner, WORTH KNOWING. In the game the host
        /// session IS the local slot's session. Here, when slot 0 is played by
        /// an AI, slot 0 owns TWO sessions: this host session and its own peer
        /// session, and both can assign local sequences for slot 0. Nothing
        /// collides today because the host ingress is only ever an intake — no
        /// code path submits through it in a lab match, so every slot-0 record
        /// carries a peer-assigned sequence and the numbering stays monotone.
        /// The invariant is load-bearing rather than incidental: submitting
        /// through <see cref="Ingress"/> in an all-AI match would interleave
        /// two independent sequence counters on one slot, and the host intake
        /// would start refusing records as duplicates. Submit through
        /// <see cref="SlotPeer.Ingress"/>, never through this one.
        /// </para>
        /// </summary>
        public const byte HostSlot = 0;

        public SimulationKernel Kernel;
        public EntityManager Entities;
        public PathfindingSystem Pathfinding;
        public MovementSystem Movement;
        public EconomySystem Economy;
        public ConstructionSystem Construction;
        public ProductionSystem Production;
        public FogOfWarSystem FogOfWar;
        public CombatSystem Combat;
        public VictorySystem Victory;

        public MatchSession Session;
        public CommandIngress Ingress;

        /// <summary>Commanding slots in ascending slot order (the order they tick in).</summary>
        public SlotPeer[] Peers = Array.Empty<SlotPeer>();

        /// <summary>How many of <see cref="Peers"/> are played by a skirmish AI.</summary>
        public int AiSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Peers.Length; i++)
                {
                    if (Peers[i].System != null) count++;
                }
                return count;
            }
        }

        /// <summary>The command seat of a slot, or null when the slot is passive.</summary>
        public SlotPeer PeerOf(byte slot)
        {
            for (int i = 0; i < Peers.Length; i++)
            {
                if (Peers[i].Slot == slot) return Peers[i];
            }
            return null;
        }

        public int SlotCount { get; private set; }

        // ----------------------------------------------------------------
        // Construction (mirror of MatchRunner.InitializeMatch)
        // ----------------------------------------------------------------

        public static MultiSlotAiHost Build(MatchSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Slots == null || spec.Slots.Length < 2 || spec.Slots.Length > MaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(spec),
                    $"a match needs between 2 and {MaxSlots} slots");
            }

            int slotCount = spec.Slots.Length;
            var activeSlots = new byte[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                if (spec.Slots[i].Slot != i)
                {
                    throw new ArgumentException(
                        $"slot {i} of the spec declares itself as slot {spec.Slots[i].Slot}: " +
                        "slots must be dense and ascending — their order fixes entity ids and every hash.",
                        nameof(spec));
                }
                activeSlots[i] = (byte)i;
            }

            var kernel = new SimulationKernel(new SimRandom(spec.Seed));

            var entities = new EntityManager(spec.EntityCapacity);
            var pathfinding = new PathfindingSystem(spec.MapWidth, spec.MapHeight);
            var movement = new MovementSystem(entities, pathfinding);
            var economy = new EconomySystem(entities, spec.StartingCreditsAE);
            var construction = new ConstructionSystem(entities, economy, pathfinding.CostField);
            var production = new ProductionSystem(entities, economy, construction);
            var fogOfWar = new FogOfWarSystem(entities, construction, teamCount: slotCount, spec.MapWidth, spec.MapHeight);
            var combat = new CombatSystem(entities, fogOfWar, economy);
            var victory = new VictorySystem(entities, construction);

            var session = new MatchSession(HostSlot, activeSlots, inputDelayTicks: 1);
            var ingress = new CommandIngress(session);
            _ = new LocalLoopbackTransport(ingress);

            var peers = new List<SlotPeer>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                SlotSpec slotSpec = spec.Slots[i];
                if (!slotSpec.HasCommandSeat) continue;

                var peerSession = new MatchSession(slotSpec.Slot, activeSlots, inputDelayTicks: 1);
                var peerIngress = new CommandIngress(peerSession);

                // Exactly one transport binds to a peer ingress. The canonical
                // one is the default; the counting stand-in replaces it only
                // when a run needs the intent verdicts, and a test pins that
                // both produce the identical hash chain.
                var peer = new SlotPeer { Slot = slotSpec.Slot, Session = peerSession, Ingress = peerIngress };
                if (spec.NeedsIntentCounting)
                {
                    peer.IntentCounter = new CountingAiPeerTransport(peerIngress, ingress);
                }
                else
                {
                    peer.Transport = new AiPeerCommandTransport(peerIngress, ingress);
                }

                // A scripted slot gets the identical seat and NO system: the
                // scenario decides, nothing decides on its own. That keeps the
                // duel arena and the movement scenarios on the canonical
                // command path instead of poking entity state directly.
                if (slotSpec.Controller == SlotController.Ai)
                {
                    peer.System = new SkirmishAiSystem(
                        slotSpec.Slot,
                        slotSpec.Profile,
                        peerIngress, entities, economy, construction, production, fogOfWar, victory);
                }

                peers.Add(peer);
            }

            // Canonical tick order — see the class remarks. The AI slots are
            // registered in ascending slot order between combat and victory.
            kernel.RegisterSystem(economy);
            kernel.RegisterSystem(construction);
            kernel.RegisterSystem(production);
            kernel.RegisterSystem(pathfinding);
            kernel.RegisterSystem(movement);
            kernel.RegisterSystem(fogOfWar);
            kernel.RegisterSystem(combat);
            for (int i = 0; i < peers.Count; i++)
            {
                if (peers[i].System != null) kernel.RegisterSystem(peers[i].System);
            }
            kernel.RegisterSystem(victory);

            kernel.BindCommands(
                new UnitCommandStateView(entities, pathfinding, economy, construction, production), ingress);

            // Faction assignment BEFORE Kernel.Start() — the SetSlotFaction
            // guard forbids it once the kernel runs.
            for (int i = 0; i < slotCount; i++)
            {
                economy.SetSlotFaction(spec.Slots[i].Slot, spec.Slots[i].Faction);
            }

            kernel.Start();

            return new MultiSlotAiHost
            {
                Kernel = kernel,
                Entities = entities,
                Pathfinding = pathfinding,
                Movement = movement,
                Economy = economy,
                Construction = construction,
                Production = production,
                FogOfWar = fogOfWar,
                Combat = combat,
                Victory = victory,
                Session = session,
                Ingress = ingress,
                Peers = peers.ToArray(),
                SlotCount = slotCount,
            };
        }

        /// <summary>Builds the host and applies the canonical opening position.</summary>
        public static MultiSlotAiHost BuildMatch(MatchSpec spec)
        {
            MultiSlotAiHost host = Build(spec);
            CanonicalOpening.Apply(host);
            return host;
        }

        // ----------------------------------------------------------------
        // Stepping (mirror of MatchRunner.StepFixedTick)
        // ----------------------------------------------------------------

        /// <summary>
        /// Seals the batch due at the next tick through the HOST ingress — the
        /// AI peers' records were forwarded into it and drain together —
        /// submits it, advances every AI peer clock to the tick about to
        /// execute (their intents then target T+1), steps the kernel, and
        /// advances the host session. The peer clocks advance in ascending slot
        /// order, the same order they were registered in.
        /// </summary>
        public void Step()
        {
            uint nextTick = Kernel.CurrentTick.Value + 1;
            CommandBatch batch = Ingress.SealTickBatch(nextTick);
            if (batch.Count > 0 && !Kernel.SubmitBatch(batch))
            {
                throw new InvalidOperationException(
                    $"[AiLab] kernel refused the sealed batch of tick {nextTick}");
            }

            for (int i = 0; i < Peers.Length; i++)
            {
                Peers[i].Session.AdvanceTick();
            }

            Kernel.StepTick();
            Session.AdvanceTick();
        }

        public void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++) Step();
        }

        /// <summary>Runs until the match is decided or the budget is exhausted.</summary>
        public void RunUntilDecided(int budgetTicks)
        {
            for (int i = 0; i < budgetTicks && !Victory.IsDecided; i++) Step();
        }
    }
}
