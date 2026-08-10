using System;
using System.Collections.Generic;
using System.Text;
using Nova.AI;
using Nova.AI.Data;
using Nova.Core;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// A match that is HELD instead of run to its end: it steps when it is told
    /// to, it can be paused, and single units can be put under a goal while it
    /// stands.
    /// <para>
    /// WHY A SEPARATE RUNNER AND NOT A FLAG ON <see cref="MatchRun"/>.
    /// <c>MatchRun.Execute</c> is a function from a spec to a result — it starts
    /// a match, runs it, and hands back numbers, which is exactly what a
    /// measurement should be. A live session is the opposite shape: it outlives
    /// every call, its tick is driven from outside, and somebody is allowed to
    /// interfere with it. Bolting that onto the measuring path would have put a
    /// "somebody might be interfering" branch inside every run the lab archives.
    /// </para>
    /// <para>
    /// A RUN WITH AN INTERVENTION IS NOT A MEASUREMENT, and this class says so
    /// in its artifacts rather than leaving it to whoever reads them: the result
    /// carries <c>intervened</c>, the interventions are written beside it, and
    /// the reports never archive such a directory. What the panel answers is
    /// "what would she have done if" — a question worth asking and not the same
    /// question as "what does she do".
    /// </para>
    /// <para>
    /// SINGLE-THREADED BY CONTRACT. The server calls in from request threads and
    /// takes <see cref="Gate"/> around everything; the simulation itself never
    /// sees two threads. That is not caution, it is the determinism rule: two
    /// interleaved steps would seal batches in an order nothing could reproduce.
    /// </para>
    /// </summary>
    public sealed class LiveMatch
    {
        private readonly MatchSpec _spec;
        private readonly GoalRecorder _goals = new GoalRecorder();
        private readonly GoalOverrideLog _overrides;
        private MultiSlotAiHost _host;

        /// <summary>Everything that touches the match is taken under this.</summary>
        public readonly object Gate = new object();

        /// <summary>Ticks per second the session runs at while it is not paused; 0 pauses it.</summary>
        public int TicksPerSecond { get; private set; } = 20;

        public bool Paused { get; private set; }

        public MatchSpec Spec => _spec;
        public GoalRecorder Goals => _goals;
        public GoalOverrideLog Overrides => _overrides;
        public MultiSlotAiHost Host => _host;

        /// <summary>
        /// A fresh session, or — with an <paramref name="overrides"/> log read
        /// back from a protocol — the REPLAY of an earlier one.
        /// <para>
        /// The replay is not a second implementation: it is this class with the
        /// same log, stepping through the same loop, so "the protocol reproduces
        /// the session" is a claim about one piece of code rather than about two
        /// agreeing. A separate replay path could pass its own test and still
        /// fail to reproduce what the panel actually did.
        /// </para>
        /// </summary>
        public LiveMatch(MatchSpec spec, GoalOverrideLog overrides = null)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _overrides = overrides ?? new GoalOverrideLog();
        }

        public void Start()
        {
            lock (Gate)
            {
                _host = MultiSlotAiHost.BuildMatch(_spec, _goals, _overrides);
                _overrides.Bind(_host.Entities);
                _overrides.AdvanceTo(_host.Kernel.CurrentTick.Value);
            }
        }

        public uint Tick => _host == null ? 0u : _host.Kernel.CurrentTick.Value;

        public bool Decided => _host != null && _host.Victory.IsDecided;

        /// <summary>
        /// Steps the match on. The mask is told which tick is about to run
        /// BEFORE the step, so an intervention typed between two steps takes
        /// effect on the tick it was stamped with and not on whichever one the
        /// scheduler happened to be on.
        /// </summary>
        public void Step(int ticks)
        {
            lock (Gate)
            {
                for (int i = 0; i < ticks; i++)
                {
                    if (_host.Victory.IsDecided) return;
                    if (_host.Kernel.CurrentTick.Value >= (uint)_spec.TickBudget) return;
                    _overrides.AdvanceTo(_host.Kernel.CurrentTick.Value + 1);
                    _host.Step();
                }
            }
        }

        public void SetPaused(bool paused)
        {
            lock (Gate) Paused = paused;
        }

        public void SetSpeed(int ticksPerSecond)
        {
            lock (Gate) TicksPerSecond = Math.Max(0, Math.Min(200, ticksPerSecond));
        }

        /// <summary>
        /// Puts units under a goal from the NEXT tick on, and writes the
        /// intervention down. <paramref name="goal"/> of
        /// <see cref="GoalKind.None"/> releases them.
        /// </summary>
        public void Force(int slot, uint entityRaw, GoalKind goal)
        {
            lock (Gate)
            {
                _overrides.Record(_host.Kernel.CurrentTick.Value + 1, slot, entityRaw, goal);
            }
        }

        // ----------------------------------------------------------------
        // What the panel reads
        // ----------------------------------------------------------------

        /// <summary>
        /// The whole visible state of one moment as one JSON object: the seats,
        /// every unit with its goal, and the session's own controls.
        /// <para>
        /// ONE REQUEST, ONE CONSISTENT PICTURE. Splitting this into an endpoint
        /// per concern would let the panel draw a unit list from one tick beside
        /// a seat bar from the next, and a diagnostic tool that mixes two ticks
        /// is showing a state the match was never in.
        /// </para>
        /// </summary>
        public string StateJson()
        {
            lock (Gate)
            {
                uint tick = _host.Kernel.CurrentTick.Value;
                var json = new StringBuilder(4096);
                json.Append("{\"tick\":").Append(tick)
                    .Append(",\"paused\":").Append(Paused ? "true" : "false")
                    .Append(",\"speed\":").Append(TicksPerSecond)
                    .Append(",\"decided\":").Append(_host.Victory.IsDecided ? "true" : "false")
                    .Append(",\"outcome\":\"").Append(_host.Victory.Outcome).Append('"')
                    .Append(",\"winnerSlot\":").Append(_host.Victory.WinnerSlot)
                    .Append(",\"budget\":").Append(_spec.TickBudget)
                    .Append(",\"intervened\":").Append(_overrides.Intervened ? "true" : "false");

                // The seats, with the army decision each of them last took.
                json.Append(",\"seats\":[");
                for (int i = 0; i < _host.SlotCount; i++)
                {
                    if (i > 0) json.Append(',');
                    AppendSeat(json, (byte)i);
                }
                json.Append(']');

                json.Append(",\"units\":[");
                AppendUnits(json);
                json.Append(']');

                json.Append(",\"overrides\":[");
                IReadOnlyList<GoalOverrideEntry> entries = _overrides.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append(entries[i].ToJsonLine());
                }
                json.Append("]}");
                return json.ToString();
            }
        }

        private void AppendSeat(StringBuilder json, byte slot)
        {
            GoalFrame frame = LastFrameOf(slot);
            json.Append("{\"slot\":").Append(slot)
                .Append(",\"faction\":\"").Append(_host.Economy.GetSlotFaction(slot).ToString().ToLowerInvariant())
                .Append('"');

            ref Nova.Simulation.Economy.PlayerEconomyState eco = ref _host.Economy.GetPlayerEconomy(slot);
            json.Append(",\"credits\":").Append(eco.AetheriumCredits)
                .Append(",\"power\":").Append(eco.PowerProvided - eco.PowerRequired);

            if (frame == null)
            {
                json.Append(",\"decided\":-1}");
                return;
            }

            AiArmyGoal army = frame.Army;
            json.Append(",\"decided\":").Append(frame.Tick)
                .Append(",\"engages\":").Append(army.Engages ? "true" : "false")
                .Append(",\"target\":").Append(army.TargetRaw)
                .Append(",\"move\":[").Append(army.MoveCellX).Append(',').Append(army.MoveCellY).Append(']')
                .Append(",\"staging\":[").Append(army.StagingCellX).Append(',').Append(army.StagingCellY).Append(']')
                .Append(",\"waveReady\":").Append(army.WaveReady ? "true" : "false")
                .Append(",\"waveMode\":").Append((int)army.WaveMode)
                .Append(",\"gathered\":").Append(army.Gathered)
                .Append(",\"committed\":").Append(army.Committed)
                .Append(",\"strength\":").Append(army.GatheredStrength)
                .Append(",\"threshold\":").Append(army.WaveThreshold)
                .Append('}');
        }

        /// <summary>
        /// Every living unit, with the goal of its seat's LAST decision — and
        /// with <c>judged</c> saying whether that goal is still in force.
        /// <para>
        /// The distinction is the same one the recorded player needs and for the
        /// same reason: the army step only runs while a seat is at or above its
        /// squad threshold, so a seat that has been ground down hands out no
        /// goals at all. Showing the last one as current would be the panel
        /// inventing an answer.
        /// </para>
        /// </summary>
        private void AppendUnits(StringBuilder json)
        {
            // Built from each seat's LAST decision and nothing older, so being in
            // this map IS being under a goal right now. A unit that is missing
            // from it is one nobody is deciding about — the honest answer is
            // "none", not the goal it had before its seat fell below the squad
            // threshold.
            var goalOf = new Dictionary<uint, AiUnitGoal>();
            for (byte slot = 0; slot < _host.SlotCount; slot++)
            {
                GoalFrame frame = LastFrameOf(slot);
                if (frame == null) continue;
                foreach (AiUnitGoal goal in frame.Units) goalOf[goal.EntityRaw] = goal;
            }

            UnitState[] units = _host.Entities.RawUnits;
            bool first = true;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                if (!u.IsActive || u.PlayerId >= _host.SlotCount) continue;
                uint raw = UnitCommandStateView.ToRawEntityId(u.Id);
                if (raw == 0) continue;

                if (!first) json.Append(',');
                first = false;

                bool isSite = _host.Construction.TryGetSite(raw, out _, out _, out _);
                json.Append("{\"id\":").Append(raw)
                    .Append(",\"slot\":").Append(u.PlayerId)
                    .Append(",\"role\":").Append((int)u.Role)
                    .Append(",\"site\":").Append(isSite ? "true" : "false")
                    .Append(",\"x\":").Append(SimFixed.WorldToGrid(u.Transform.PositionX))
                    .Append(",\"y\":").Append(SimFixed.WorldToGrid(u.Transform.PositionY))
                    .Append(",\"hp\":").Append(u.CurrentHealth)
                    .Append(",\"hpMax\":").Append(u.MaxHealth)
                    // ONLY FOR UNITS THE ARMY STEP EVER ASKS ABOUT. A seat-wide
                    // entry names the harvesters too, but the AI never asks the
                    // mask about one — printing "attack" beside a harvester
                    // would promise a behaviour that has no code behind it, and
                    // this panel exists to end exactly that kind of sentence.
                    .Append(",\"forced\":")
                    .Append(IsCombatRole(u.Role) && !isSite ? (int)_overrides.ResolveGoal(raw) : 0);

                if (goalOf.TryGetValue(raw, out AiUnitGoal goal))
                {
                    json.Append(",\"goal\":").Append((int)goal.Goal)
                        .Append(",\"judged\":true")
                        .Append(",\"health\":").Append(goal.HealthPercent)
                        .Append(",\"threat\":").Append(goal.ThreatDistanceCells)
                        .Append(",\"toStaging\":").Append(goal.StagingDistanceCells)
                        .Append(",\"toHome\":").Append(goal.HomeDistanceCells);
                }
                else
                {
                    json.Append(",\"goal\":0,\"judged\":false");
                }
                json.Append('}');
            }
        }

        /// <summary>
        /// The roles the army step judges — the same span
        /// <c>SkirmishAiSystem.IsCombatRole</c> uses, named here because the
        /// panel may not claim a goal for a unit the AI never looks at.
        /// </summary>
        private static bool IsCombatRole(UnitRole role) =>
            role >= UnitRole.BasicInfantry && role <= UnitRole.Artillery;

        private GoalFrame LastFrameOf(byte slot)
        {
            IReadOnlyList<GoalFrame> frames = _goals.Frames;
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].Slot == slot) return frames[i];
            }
            return null;
        }
    }
}
