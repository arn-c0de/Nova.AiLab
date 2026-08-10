using System;
using System.Collections.Generic;
using System.Text;
using Nova.AI.Data;
using Nova.Core;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>One intervention: from this tick on, these units are under this goal.</summary>
    public readonly struct GoalOverrideEntry
    {
        /// <summary>The first tick the entry is in force. Never the tick it was typed at — see the log's remarks.</summary>
        public readonly uint FromTick;

        /// <summary>The seat the entry applies to, or <see cref="AllSlots"/>.</summary>
        public readonly int Slot;

        /// <summary>The unit, or 0 for "every combat unit of the seat".</summary>
        public readonly uint EntityRaw;

        /// <summary><see cref="GoalKind.None"/> releases — the AI decides again.</summary>
        public readonly GoalKind Goal;

        public const int AllSlots = -1;

        public GoalOverrideEntry(uint fromTick, int slot, uint entityRaw, GoalKind goal)
        {
            FromTick = fromTick;
            Slot = slot;
            EntityRaw = entityRaw;
            Goal = goal;
        }

        public string ToJsonLine()
        {
            var json = new StringBuilder(96);
            json.Append("{\"t\":").Append(FromTick)
                .Append(",\"slot\":").Append(Slot)
                .Append(",\"id\":").Append(EntityRaw)
                .Append(",\"goal\":").Append((int)Goal)
                .Append('}');
            return json.ToString();
        }
    }

    /// <summary>
    /// Every intervention of a live run, and the mask the AI reads them through.
    /// <para>
    /// THE LOG AND THE MASK ARE ONE OBJECT ON PURPOSE. If the panel wrote a
    /// protocol on one side and pushed a value into the AI on the other, the two
    /// could disagree — and the file would describe a run that never happened,
    /// which is the only way a recording of an intervention can be actively
    /// harmful. Here the AI's answer IS derived from the log: replaying the log
    /// cannot diverge from the live run, because there is nothing else to
    /// replay.
    /// </para>
    /// <para>
    /// AN ENTRY IS IN FORCE FROM A TICK, NOT FROM A MOMENT. A button is pressed
    /// between two steps, and "now" is not a reproducible instant; the entry is
    /// therefore stamped with the first tick that has not been executed yet, and
    /// the runner tells the log which tick it is about to run
    /// (<see cref="AdvanceTo"/>) before it runs it. A replay does exactly the
    /// same in exactly the same order, which is what makes
    /// <c>LiveOverrideTests</c>' bit-equality claim mean something.
    /// </para>
    /// <para>
    /// THE HOST HOLDS THIS STATE, NOT THE AI. The AI reads the mask the way it
    /// reads its profile — once per unit per decision, no memory of the last
    /// answer — so it stays a pure function of the committed state and its
    /// inputs. Nothing here is a sidecar block.
    /// </para>
    /// </summary>
    public sealed class GoalOverrideLog : IAiGoalOverride
    {
        private readonly List<GoalOverrideEntry> _entries = new List<GoalOverrideEntry>();
        private EntityManager _entities;
        private uint _tick;

        public IReadOnlyList<GoalOverrideEntry> Entries => _entries;

        /// <summary>True once anybody has intervened — a run that is no longer a measurement.</summary>
        public bool Intervened => _entries.Count > 0;

        /// <summary>
        /// The entity store, so a seat-wide entry can find out which units it
        /// covers. Bound after the host is built, because the mask has to exist
        /// before the AI that reads it.
        /// </summary>
        public void Bind(EntityManager entities)
        {
            _entities = entities;
        }

        /// <summary>The tick the runner is about to execute. Entries stamped later are not in force yet.</summary>
        public void AdvanceTo(uint tick)
        {
            _tick = tick;
        }

        /// <summary>
        /// Records an intervention as in force from <paramref name="fromTick"/>.
        /// Entries stay in the order they were recorded; a later one on the same
        /// unit wins, which is what makes releasing possible without deleting
        /// history — and the history is the file.
        /// </summary>
        public void Record(uint fromTick, int slot, uint entityRaw, GoalKind goal)
        {
            _entries.Add(new GoalOverrideEntry(fromTick, slot, entityRaw, goal));
        }

        /// <summary>
        /// The goal forced on this unit right now, or <see cref="GoalKind.None"/>.
        /// <para>
        /// Walked from the back: the most recent entry in force that names this
        /// unit — by id, or by its seat — is the answer. The list is a handful of
        /// rows even in a long session, and a linear walk keeps the rule
        /// ("the last word wins") readable in the code that implements it.
        /// </para>
        /// </summary>
        public GoalKind ResolveGoal(uint entityRaw)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                GoalOverrideEntry entry = _entries[i];
                if (entry.FromTick > _tick) continue;
                if (entry.EntityRaw != 0)
                {
                    if (entry.EntityRaw != entityRaw) continue;
                    return entry.Goal;
                }
                if (entry.Slot != GoalOverrideEntry.AllSlots && entry.Slot != SlotOf(entityRaw)) continue;
                return entry.Goal;
            }
            return GoalKind.None;
        }

        /// <summary>
        /// The seat a unit belongs to, or -1 while nothing is bound. Read from
        /// the committed store rather than remembered: a remembered owner would
        /// be a second copy of state that can go stale, and the entity id is
        /// enough to ask.
        /// </summary>
        private int SlotOf(uint entityRaw)
        {
            if (_entities == null) return -1;
            EntityId id = UnitCommandStateView.ToEntityId(entityRaw);
            return _entities.TryGetUnit(id, out UnitState unit) ? unit.PlayerId : -1;
        }

        public string ToNdjson()
        {
            var json = new StringBuilder(_entries.Count * 64);
            for (int i = 0; i < _entries.Count; i++)
            {
                json.Append(_entries[i].ToJsonLine()).Append('\n');
            }
            return json.ToString();
        }

        /// <summary>
        /// Reads a protocol back — the replay half of the bit-equality claim.
        /// Deliberately tolerant of nothing: a row it cannot read is an error,
        /// because a replay that silently skipped an intervention would "prove"
        /// reproducibility by leaving out the thing being reproduced.
        /// </summary>
        public static GoalOverrideLog Parse(string ndjson)
        {
            var log = new GoalOverrideLog();
            if (string.IsNullOrWhiteSpace(ndjson)) return log;

            foreach (string line in ndjson.Split('\n'))
            {
                string row = line.Trim();
                if (row.Length == 0) continue;
                log.Record(
                    (uint)ReadInt(row, "\"t\":"),
                    ReadInt(row, "\"slot\":"),
                    (uint)ReadInt(row, "\"id\":"),
                    (GoalKind)ReadInt(row, "\"goal\":"));
            }
            return log;
        }

        private static int ReadInt(string row, string key)
        {
            int at = row.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) throw new FormatException($"[AiLab] an override row without {key}: {row}");
            at += key.Length;
            int end = at;
            if (end < row.Length && row[end] == '-') end++;
            while (end < row.Length && char.IsDigit(row[end])) end++;
            return int.Parse(row.Substring(at, end - at), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
