using System;
using Nova.AI;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.Economy;
using Nova.Simulation.State;
using Nova.Simulation.Victory;

namespace Nova.AiLab
{
    /// <summary>Who issues this slot's commands.</summary>
    public enum SlotController
    {
        /// <summary>The MS-1 skirmish AI plays the slot.</summary>
        Ai = 0,

        /// <summary>
        /// Nobody. The slot exists and owns its opening position, and no
        /// command is ever issued for it — the passive fixture of
        /// SkirmishAiTests.
        /// </summary>
        Passive = 1,

        /// <summary>
        /// A scenario issues the commands. The slot gets its own session,
        /// ingress and transport — the same seat an AI peer holds — but no
        /// SkirmishAiSystem, so nothing decides on its own. This is what the
        /// duel arena and the movement scenarios use: their orders travel the
        /// canonical sealed command path exactly like a human's, instead of
        /// poking entity state directly.
        /// </summary>
        Scripted = 2,
    }

    /// <summary>What one slot is: its faction and who commands it.</summary>
    public sealed class SlotSpec
    {
        public byte Slot;
        public FactionId Faction;
        public SlotController Controller = SlotController.Ai;

        public bool IsAi => Controller == SlotController.Ai;

        /// <summary>True when the slot owns a session it can submit through.</summary>
        public bool HasCommandSeat => Controller != SlotController.Passive;

        /// <summary>
        /// The AI profile of this slot. The canonical default mirrors
        /// <c>MatchRunner.InitializeMatch</c> exactly (power margin 0, army 12,
        /// squad threshold 6, harvesters 2) — E6 moved these numbers into
        /// <c>AI.Data/</c>, and the shipped profile still carries them
        /// value-for-value.
        /// </summary>
        public AiFactionProfile Profile;

        /// <summary>
        /// WHICH profile the slot played, by name — provenance, not behaviour.
        /// <para>
        /// <see cref="AiFactionProfile"/> carries the numbers but no identity a
        /// report can print, so <c>result.json</c> used to write the literal
        /// string "canonical" for every slot of every run. In a comparison that
        /// is a lie about the one artifact the report links into: the sample run
        /// kept for <c>late-push</c> claimed both slots played the shipped
        /// profile. A number whose provenance is wrong is worse than a missing
        /// number, because it still reads like a measurement.
        /// </para>
        /// </summary>
        public string ProfileId = CanonicalProfileId;

        /// <summary>The shipped profile's id — the one <c>MatchRunner</c> constructs.</summary>
        public const string CanonicalProfileId = "ms1-canonical";

        public static AiFactionProfile CanonicalProfile(FactionId faction) =>
            new AiFactionProfile(faction.ToString(),
                targetPowerMargin: 0,
                targetArmySize: 12,
                attackSquadThreshold: 6,
                targetHarvesterCount: 2);
    }

    /// <summary>
    /// Input contract of one lab run (plan section 3.2). E1 fills it from the
    /// command line; E2 reads it from JSON — the shape is already the one the
    /// spec file describes, so that step adds a reader, not a rewrite.
    /// </summary>
    public sealed class MatchSpec
    {
        public const int SpecVersion = 1;

        public ulong Seed = 0xA17E57DE57UL;

        /// <summary>
        /// Default 27.000 = <see cref="VictorySystem.TimeLimitTick"/>, the
        /// game's own time limit (plan decision 18): a lab result carries no
        /// footnote only while the budget is the game's. Shortening it biases
        /// toward fast strategies.
        /// </summary>
        public int TickBudget = (int)VictorySystem.TimeLimitTick;

        public ushort MapWidth = 128;
        public ushort MapHeight = 128;
        public int EntityCapacity = 1024;
        public long StartingCreditsAE = EconomySystem.CanonicalMatchStartingCreditsAE;

        /// <summary>State hash every n ticks (0 = only the end state).</summary>
        public int HashIntervalTicks;

        /// <summary>Metric sample every n ticks (0 = no trace).</summary>
        public int TraceIntervalTicks;

        /// <summary>View frame every n ticks (0 = no view window).</summary>
        public int ViewIntervalTicks;

        /// <summary>
        /// Position sample every n ticks while the view window is on
        /// (0 = no track). Default 1: EVERY tick.
        /// <para>
        /// It is not a copy of <see cref="ViewIntervalTicks"/> and must not
        /// become one. A view frame is a picture and costs a picture; a track
        /// sample is an id and two integers. At <c>--view-every 25</c> a unit
        /// crosses two to three cells between frames, and a route drawn
        /// through those points is a straight line nobody walked. Measured on
        /// a decided match — 28 to 37 entities — one sample per tick costs a
        /// few megabytes, which is why the default records verbatim instead of
        /// thinning. Raise it only for a run that would otherwise be
        /// unwieldy, and know that the route gets coarser when you do.
        /// </para>
        /// <para>
        /// The EVENT stream ignores this: an edge between two samples is gone
        /// forever, so events are always read every tick.
        /// </para>
        /// </summary>
        public int TrackIntervalTicks = 1;

        /// <summary>
        /// Record the fog layer with each view frame. Off by default because it
        /// dominates the file size; on when the question is "could the AI see
        /// it?", which is the most common one.
        /// </summary>
        public bool RecordFog;

        /// <summary>
        /// Bind the counting transport instead of the canonical one, so intent
        /// verdicts become countable. Unset follows the trace: a run that
        /// collects metrics needs the verdicts, a run that does not keeps the
        /// literal MatchRunner wiring. Set explicitly only to force one of the
        /// two — the equivalence test does exactly that.
        /// <para>
        /// Turning it on is proven free: with and without produces the
        /// identical hash chain.
        /// </para>
        /// </summary>
        public bool? CountIntents;

        public bool NeedsIntentCounting => CountIntents ?? (TraceIntervalTicks > 0);

        public SlotSpec[] Slots = DefaultSlots(2);

        /// <summary>
        /// The canonical seating: slot 0 Alliance, slot 1 Legion — the pairing
        /// MatchRunner, MatchBootstrap and the determinism scenario all use.
        /// Further slots alternate, so a 4-slot free-for-all stays 2v2 by
        /// faction without inventing a third one.
        /// </summary>
        public static SlotSpec[] DefaultSlots(int slotCount)
        {
            if (slotCount < 2 || slotCount > CommandLimits.ReservedPlayerSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount,
                    $"slot count must be in [2, {CommandLimits.ReservedPlayerSlots}]");
            }

            var slots = new SlotSpec[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                FactionId faction = (i % 2) == 0 ? FactionId.Alliance : FactionId.Legion;
                slots[i] = new SlotSpec
                {
                    Slot = (byte)i,
                    Faction = faction,
                    Controller = SlotController.Ai,
                    Profile = SlotSpec.CanonicalProfile(faction),
                };
            }
            return slots;
        }
    }
}
