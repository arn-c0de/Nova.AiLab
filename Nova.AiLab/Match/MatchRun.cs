using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nova.Simulation.Definitions;
using Nova.Simulation.Victory;

namespace Nova.AiLab
{
    /// <summary>One entry of the state hash chain (plan section 3.5, proof 3).</summary>
    public readonly struct HashChainEntry
    {
        public readonly uint Tick;
        public readonly ulong StateHash;

        public HashChainEntry(uint tick, ulong stateHash)
        {
            Tick = tick;
            StateHash = stateHash;
        }
    }

    /// <summary>
    /// Output contract of one lab run (plan section 3.2). Integers only — no
    /// float ever leaves the simulation, otherwise comparing two runs is luck
    /// instead of arithmetic.
    /// </summary>
    public sealed class MatchRunResult
    {
        public int SpecVersion = MatchSpec.SpecVersion;
        public ulong Seed;
        public int SlotCount;
        public int AiSlotCount;
        public int TickBudget;

        public MatchOutcome Outcome;
        public byte WinnerSlot;
        public uint DecidedTick;
        public uint FinalTick;

        public ulong FinalStateHash;
        public ulong DefinitionsHash64;

        /// <summary>Empty unless <see cref="MatchSpec.HashIntervalTicks"/> is set.</summary>
        public List<HashChainEntry> HashChain = new List<HashChainEntry>();

        /// <summary>Empty unless <see cref="MatchSpec.TraceIntervalTicks"/> is set.</summary>
        public List<MetricSample> Trace = new List<MetricSample>();

        /// <summary>Empty unless <see cref="MatchSpec.ViewIntervalTicks"/> is set.</summary>
        public List<ViewFrame> View = new List<ViewFrame>();

        /// <summary>
        /// The position track — where every entity actually walked, at full
        /// tick resolution. Empty unless the view window is on and
        /// <see cref="MatchSpec.TrackIntervalTicks"/> is positive.
        /// </summary>
        public List<TrackFrame> Tracks = new List<TrackFrame>();

        /// <summary>What happened to each entity, at the exact tick it happened.</summary>
        public List<DebugEvent> Events = new List<DebugEvent>();

        /// <summary>One row per entity, derived from track and events at the end of the run.</summary>
        public List<RouteMetrics> Units = new List<RouteMetrics>();

        /// <summary>
        /// The game-feel columns (NEXT-STEPS.md section 7), one entry per slot.
        /// Empty without a trace: three of the four are per-interval or
        /// per-tick derivations, and zeros would read as measurements.
        /// </summary>
        public List<FeelMetrics> Feel = new List<FeelMetrics>();

        /// <summary>Wall-clock cost of this run — throughput bookkeeping, never an input to a result.</summary>
        public long ElapsedMilliseconds;

        public bool IsDecided => Outcome != MatchOutcome.Undecided;
    }

    /// <summary>
    /// Drives one match to its end and collects the E1 result: outcome, winner,
    /// deciding tick and end-state hash — plus the hash chain when the spec
    /// asks for one.
    /// <para>
    /// The chain is the diagnostic that section 8 of the plan wants from the
    /// lab: when a behaviour change turns a baseline red it shows the TICK at
    /// which two builds diverge, not just THAT they do.
    /// </para>
    /// </summary>
    public static class MatchRun
    {
        /// <summary>
        /// Runs one match to its end.
        /// <paramref name="onFrame"/> receives every view frame as it is
        /// captured, which is how the live terminal view watches a running
        /// match without a second capture path.
        /// </summary>
        public static MatchRunResult Execute(MatchSpec spec, Action<ViewFrame> onFrame = null)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var watch = Stopwatch.StartNew();

            MultiSlotAiHost host = MultiSlotAiHost.BuildMatch(spec);
            var result = new MatchRunResult
            {
                Seed = spec.Seed,
                SlotCount = host.SlotCount,
                AiSlotCount = host.AiSlotCount,
                TickBudget = spec.TickBudget,
                DefinitionsHash64 = SimDefinitions.ComputeDefinitionsHash64(),
            };

            bool chained = spec.HashIntervalTicks > 0;
            bool traced = spec.TraceIntervalTicks > 0;
            bool viewed = spec.ViewIntervalTicks > 0;
            // Track and events ride along with the view window rather than on
            // a switch of their own: whoever records frames is looking for an
            // explanation, and at thirty entities the route and the edges are
            // too cheap to be worth turning off.
            bool tracked = viewed && spec.TrackIntervalTicks > 0;
            TraceCollector collector = traced ? new TraceCollector(host) : null;
            ViewRecorder recorder = viewed ? new ViewRecorder(host, spec.RecordFog) : null;
            EntityTrackRecorder tracker = tracked ? new EntityTrackRecorder(host) : null;
            DebugEventLog eventLog = tracked ? new DebugEventLog(host) : null;

            if (chained)
            {
                result.HashChain.Add(new HashChainEntry(host.Kernel.CurrentTick.Value, host.Kernel.CalculateStateHash()));
            }
            if (traced)
            {
                result.Trace.Add(collector.Sample(host.Kernel.CurrentTick.Value));
            }
            if (viewed)
            {
                ViewFrame opening = recorder.Capture(host.Kernel.CurrentTick.Value);
                result.View.Add(opening);
                onFrame?.Invoke(opening);
            }
            if (tracked)
            {
                // The shadow state starts empty, so this first pass reports
                // the canonical opening as spawns — which is what it is.
                result.Tracks.Add(tracker.Capture(host.Kernel.CurrentTick.Value));
                eventLog.Collect(host.Kernel.CurrentTick.Value);
            }

            for (int i = 0; i < spec.TickBudget && !host.Victory.IsDecided; i++)
            {
                host.Step();

                uint tick = host.Kernel.CurrentTick.Value;
                if (traced)
                {
                    collector.OnTick(tick);
                    if (tick % (uint)spec.TraceIntervalTicks == 0) result.Trace.Add(collector.Sample(tick));
                }
                if (viewed && tick % (uint)spec.ViewIntervalTicks == 0)
                {
                    ViewFrame frame = recorder.Capture(tick);
                    result.View.Add(frame);
                    // The live terminal view and the recorded file read the
                    // SAME frame stream (plan decision 10) — one capture, two
                    // consumers, no second code path that could disagree.
                    onFrame?.Invoke(frame);
                }
                if (tracked)
                {
                    // Events EVERY tick, unconditionally: an edge that falls
                    // between two samples is not late, it is gone.
                    eventLog.Collect(tick);
                    if (tick % (uint)spec.TrackIntervalTicks == 0)
                    {
                        TrackFrame track = tracker.Capture(tick);
                        if (!track.IsEmpty) result.Tracks.Add(track);
                    }
                }
                if (chained && tick % (uint)spec.HashIntervalTicks == 0)
                {
                    result.HashChain.Add(new HashChainEntry(tick, host.Kernel.CalculateStateHash()));
                }
            }

            result.Outcome = host.Victory.Outcome;
            result.WinnerSlot = host.Victory.WinnerSlot;
            result.DecidedTick = host.Victory.DecidedTick;
            result.FinalTick = host.Kernel.CurrentTick.Value;
            result.FinalStateHash = host.Kernel.CalculateStateHash();

            if (traced)
            {
                // Damage still unanswered when the match ends belongs in the
                // tally before it is read, not after.
                collector.FinishReactions();
                result.Feel = FeelMetrics.Compute(
                    result.Trace, collector.Reactions, result.FinalTick, host.SlotCount);
            }

            if (tracked)
            {
                result.Events.AddRange(eventLog.Events);
                result.Units = RouteMetrics.Compute(result.Tracks, result.Events, eventLog.Tallies);
            }

            watch.Stop();
            result.ElapsedMilliseconds = watch.ElapsedMilliseconds;
            return result;
        }
    }
}
