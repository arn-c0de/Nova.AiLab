using System;
using System.Collections.Generic;
using System.Text;
using Nova.AI;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// One decision of one seat: what the army decided, and what each of its
    /// combat units was put under.
    /// </summary>
    public sealed class GoalFrame
    {
        public uint Tick;
        public byte Slot;
        public AiArmyGoal Army;
        public readonly List<AiUnitGoal> Units = new List<AiUnitGoal>();

        /// <summary>
        /// One NDJSON line. Integers only, booleans as 0/1 — the same rule the
        /// rest of the artifacts follow, so two runs are comparable by
        /// arithmetic rather than by reading.
        /// <para>
        /// Column order is fixed and documented in <c>AGENTS.md</c>; new columns
        /// are APPENDED, exactly as the entity id was appended to the view
        /// frame, so an older file keeps reading correctly in the columns it
        /// has.
        /// </para>
        /// </summary>
        public string ToJsonLine()
        {
            var json = new StringBuilder(96 + Units.Count * 56);
            json.Append("{\"t\":").Append(Tick).Append(",\"s\":").Append(Slot);

            json.Append(",\"a\":[")
                .Append(Army.Engages ? 1 : 0).Append(',')
                .Append(Army.TargetRaw).Append(',')
                .Append(Army.MoveCellX).Append(',').Append(Army.MoveCellY).Append(',')
                .Append(Army.StagingCellX).Append(',').Append(Army.StagingCellY).Append(',')
                .Append(Army.WaveReady ? 1 : 0).Append(',')
                .Append((int)Army.WaveMode).Append(',')
                .Append(Army.Gathered).Append(',').Append(Army.Committed).Append(',')
                .Append(Army.GatheredStrength).Append(',').Append(Army.WaveThreshold).Append(',')
                .Append(Army.HomeThreatened ? 1 : 0)
                .Append(']');

            json.Append(",\"u\":[");
            for (int i = 0; i < Units.Count; i++)
            {
                if (i > 0) json.Append(',');
                AiUnitGoal u = Units[i];
                json.Append('[').Append(u.EntityRaw).Append(',')
                    .Append((int)u.Goal).Append(',')
                    .Append(u.Forced ? 1 : 0).Append(',')
                    .Append(u.AttackTargetRaw).Append(',')
                    .Append(u.MoveCellX).Append(',').Append(u.MoveCellY).Append(',')
                    .Append(u.HealthPercent).Append(',')
                    .Append(u.ThreatDistanceCells).Append(',')
                    .Append(u.StagingDistanceCells).Append(',')
                    .Append(u.HomeDistanceCells).Append(']');
            }
            json.Append("]}");
            return json.ToString();
        }
    }

    /// <summary>
    /// Writes down what the AI intended, at the moment it intended it.
    /// <para>
    /// WHY THIS EXISTS AT ALL. Until the goals had names, the only way to show
    /// a unit's intent was to re-derive it beside the recording: the player page
    /// carried a JavaScript copy of the wave gate, the staging ring and the
    /// retreat geometry, and every number it produced had to be labelled
    /// "derived" because it was a second implementation of the rules that could
    /// drift from the first without anything going red. A diagnostic tool
    /// showing a slightly different set of rules than the thing it diagnoses is
    /// worse than one showing nothing. This records the verdict where it is
    /// reached.
    /// </para>
    /// <para>
    /// HARD CONDITION, the same one the view recorder lives under: pure
    /// observer. It is handed to the AI as an <see cref="IAiGoalObserver"/>,
    /// which has no way to answer back — the AI cannot read anything from here,
    /// and a run with and without the recorder must produce the identical hash
    /// chain. <c>GoalRecorderTests</c> asserts it.
    /// </para>
    /// <para>
    /// NOT DELTA-CODED, and the plan in <c>GOALS.md</c> said it would be. The
    /// track file is delta-coded because it carries every entity on every tick;
    /// this carries the judged units on every DECISION, and the decision cadence
    /// is 20 ticks. The canonical match produces some two thousand rows — an
    /// encoding that saves a fifth of a small file and costs every reader a
    /// reconstruction step is a bad trade, and the numbers beside the goal
    /// (health, distances) change on every row anyway, so only the goal column
    /// would compress at all.
    /// </para>
    /// </summary>
    public sealed class GoalRecorder : IAiGoalObserver
    {
        private readonly List<GoalFrame> _frames = new List<GoalFrame>();

        public IReadOnlyList<GoalFrame> Frames => _frames;

        public void OnArmyGoal(byte slot, uint tick, in AiArmyGoal army)
        {
            _frames.Add(new GoalFrame { Tick = tick, Slot = slot, Army = army });
        }

        /// <summary>
        /// Appends to the frame of the same seat and tick.
        /// <para>
        /// It looks the frame up rather than trusting "the last one", because
        /// several AI seats decide within the same tick — ascending slot order,
        /// one after another — and a unit landing in the neighbour's frame would
        /// be an error nothing else could catch: the file would still parse, the
        /// panel would still draw, and the goals would belong to the wrong side.
        /// </para>
        /// </summary>
        public void OnUnitGoal(byte slot, uint tick, in AiUnitGoal goal)
        {
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                GoalFrame frame = _frames[i];
                if (frame.Tick != tick) break;
                if (frame.Slot != slot) continue;
                frame.Units.Add(goal);
                return;
            }
            throw new InvalidOperationException(
                $"[AiLab] slot {slot} reported a unit goal at tick {tick} without an army decision — " +
                "the observer contract says the army is reported first");
        }
    }
}
