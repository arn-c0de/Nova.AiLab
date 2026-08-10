using System.Collections.Generic;
using System.Text;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// The columns of <c>units.json</c>: what ONE entity did over its whole
    /// life, derived from the position track and the event stream.
    /// <para>
    /// WHY PER UNIT AND NOT PER SLOT. <see cref="SlotMetrics"/> and
    /// <see cref="FeelMetrics"/> answer "how did this army do". They cannot
    /// answer "why does that tank take the long way round the refinery", and
    /// that question is the whole reason the movement work exists. These are
    /// the three columns LAUFROUTEN.md section 2C asks for, one row per
    /// entity.
    /// </para>
    /// <para>
    /// STILL NOT A SCORE, like everything the lab writes: nothing here is
    /// summed, weighted or ranked. Integers only — the walked distance goes
    /// through an integer square root, so no <c>double</c> exists on the path
    /// at all.
    /// </para>
    /// </summary>
    public sealed class RouteMetrics
    {
        public uint Id;
        public byte Slot;
        public UnitRole Role;

        public uint FirstTick;
        public uint LastTick;
        public bool Died;

        /// <summary>Ticks with <c>IsMoving</c> set.</summary>
        public int MovingTicks;

        /// <summary>
        /// Ticks with <c>IsMoving</c> set and the position unchanged — the
        /// measured form of "no mutual blocking" (CLAUDE.md section 1).
        /// </summary>
        public int BlockedTicks;

        public int OrderChanges;
        public int GoalChanges;
        public int AttackStarts;
        public int DamageTaken;
        public int DamageDealtDerived;
        public int KillsDerived;

        /// <summary>Distance actually walked, in whole cells.</summary>
        public int PathLengthCells;

        /// <summary>
        /// Walked distance against the straight line between where a movement
        /// segment BEGAN and where it ENDED, in percent, summed over all
        /// segments. 100 = the unit walked the beeline; 200 = it walked twice
        /// as far as it had to. <c>-1</c> when the unit never walked a
        /// measurable segment — a 0 would read as a perfect route.
        /// <para>
        /// AGAINST THE END POINT, NOT THE GOAL. Measured against the goal cell
        /// the column drops BELOW 100 on every normal arrival, because a unit
        /// stops within its arrival tolerance and the beeline runs to the cell
        /// itself — a "detour" of 91 % is arithmetic nonsense that reads like a
        /// finding. Between the two points the unit actually visited, the ratio
        /// can never fall under 100, so any value that does is a defect in this
        /// file and not in the movement code.
        /// </para>
        /// <para>
        /// A SEGMENT ENDS WHERE THE GOAL CHANGES. A goal that jumps mid-walk
        /// opens a NEW segment instead of inflating the old one; otherwise
        /// this column would measure how often the AI changes its mind, not
        /// how good the path was (the open question of LAUFROUTEN.md section
        /// 4, decided here).
        /// </para>
        /// </summary>
        public int DetourPercent = -1;

        /// <summary>Movement segments that had a measurable straight line — the sample size behind the detour.</summary>
        public int Segments;

        public string ToJsonLine()
        {
            var json = new StringBuilder(256);
            json.Append("{\"id\":").Append(Id)
                .Append(",\"slot\":").Append(Slot)
                .Append(",\"role\":").Append((int)Role)
                .Append(",\"firstTick\":").Append(FirstTick)
                .Append(",\"lastTick\":").Append(LastTick)
                .Append(",\"died\":").Append(Died ? 1 : 0)
                .Append(",\"movingTicks\":").Append(MovingTicks)
                .Append(",\"blockedTicks\":").Append(BlockedTicks)
                .Append(",\"orderChanges\":").Append(OrderChanges)
                .Append(",\"goalChanges\":").Append(GoalChanges)
                .Append(",\"attackStarts\":").Append(AttackStarts)
                .Append(",\"damageTaken\":").Append(DamageTaken)
                .Append(",\"damageDealtDerived\":").Append(DamageDealtDerived)
                .Append(",\"killsDerived\":").Append(KillsDerived)
                .Append(",\"pathLengthCells\":").Append(PathLengthCells)
                .Append(",\"detourPercent\":").Append(DetourPercent)
                .Append(",\"segments\":").Append(Segments)
                .Append('}');
            return json.ToString();
        }

        /// <summary>
        /// Walks the recorded track once, in tick order, and turns it into one
        /// row per entity.
        /// <para>
        /// The track carries WHERE, the events carry WHEN A GOAL CHANGED —
        /// neither alone is enough: a distance without segment boundaries is a
        /// number without a question, and a goal without positions has nothing
        /// to compare against.
        /// </para>
        /// </summary>
        public static List<RouteMetrics> Compute(
            IReadOnlyList<TrackFrame> tracks,
            IReadOnlyList<DebugEvent> events,
            IReadOnlyDictionary<uint, UnitTally> tallies)
        {
            var rows = new List<RouteMetrics>();
            if (tallies == null || tallies.Count == 0) return rows;

            var positions = new Dictionary<uint, long>(256);   // id -> (x << 32) | y, unsigned halves
            var walked = new Dictionary<uint, long>(256);       // id -> total walked, Q16.16 raw
            var open = new Dictionary<uint, Segment>(256);
            var closed = new Dictionary<uint, Closed>(256);

            int eventIndex = 0;
            for (int f = 0; tracks != null && f < tracks.Count; f++)
            {
                TrackFrame frame = tracks[f];

                // A keyframe writes EVERY living unit absolutely, including the
                // ones that moved in that tick — the recorder does not skip
                // them, it just states them differently. Reading an absolute
                // sample as a mere position assignment therefore drops one
                // sampled interval of walking every KeyframeIntervalTicks,
                // while the endpoint it lands on still counts in full towards
                // the straight line. That is a walk measured shorter than the
                // beeline between its own ends, and no movement code can
                // produce it.
                //
                // A unit seen here for the FIRST time has nothing to travel
                // from: that absolute sample opens the track, it does not
                // continue one.
                for (int i = 0; i < frame.Absolute.Count; i++)
                {
                    TrackSample s = frame.Absolute[i];
                    if (positions.TryGetValue(s.Id, out long previous))
                    {
                        long step = Length(s.X - UnpackX(previous), s.Y - UnpackY(previous));
                        if (step > 0)
                        {
                            walked[s.Id] = (walked.TryGetValue(s.Id, out long total) ? total : 0) + step;
                            if (open.TryGetValue(s.Id, out Segment segment)) segment.Walked += step;
                        }
                    }

                    positions[s.Id] = Pack(s.X, s.Y);
                }
                for (int i = 0; i < frame.Delta.Count; i++)
                {
                    TrackSample s = frame.Delta[i];
                    if (!positions.TryGetValue(s.Id, out long packed)) continue;

                    long step = Length(s.X, s.Y);
                    walked[s.Id] = (walked.TryGetValue(s.Id, out long total) ? total : 0) + step;
                    if (open.TryGetValue(s.Id, out Segment segment)) segment.Walked += step;

                    positions[s.Id] = Pack(UnpackX(packed) + s.X, UnpackY(packed) + s.Y);
                }
                for (int i = 0; i < frame.Ended.Count; i++)
                {
                    CloseSegment(frame.Ended[i], positions, open, closed);
                    positions.Remove(frame.Ended[i]);
                }

                // Events of the same tick are read AFTER the positions of that
                // tick: a goal set at tick T is walked towards from where the
                // unit stands at T.
                while (events != null && eventIndex < events.Count && events[eventIndex].Tick <= frame.Tick)
                {
                    ApplyEvent(events[eventIndex], positions, open, closed);
                    eventIndex++;
                }
            }

            while (events != null && eventIndex < events.Count)
            {
                ApplyEvent(events[eventIndex], positions, open, closed);
                eventIndex++;
            }

            // A unit still walking when the match ended keeps its open segment
            // out of the column: an unfinished walk has no detour yet.
            var ids = new List<uint>(tallies.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                UnitTally tally = tallies[ids[i]];
                var row = new RouteMetrics
                {
                    Id = tally.Id,
                    Slot = tally.Slot,
                    Role = tally.Role,
                    FirstTick = tally.FirstTick,
                    LastTick = tally.LastTick,
                    Died = tally.Died,
                    MovingTicks = tally.MovingTicks,
                    BlockedTicks = tally.BlockedTicks,
                    OrderChanges = tally.OrderChanges,
                    GoalChanges = tally.GoalChanges,
                    AttackStarts = tally.AttackStarts,
                    DamageTaken = tally.DamageTaken,
                    DamageDealtDerived = tally.DamageDealtDerived,
                    KillsDerived = tally.KillsDerived,
                    PathLengthCells = walked.TryGetValue(tally.Id, out long total) ? (int)(total >> 16) : 0,
                };

                if (closed.TryGetValue(tally.Id, out Closed sums) && sums.Straight > 0)
                {
                    row.DetourPercent = (int)(sums.Walked * 100 / sums.Straight);
                    row.Segments = sums.Count;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static void ApplyEvent(
            DebugEvent debugEvent,
            Dictionary<uint, long> positions,
            Dictionary<uint, Segment> open,
            Dictionary<uint, Closed> closed)
        {
            switch (debugEvent.Kind)
            {
                case DebugEventKind.PathGoal:
                    CloseSegment(debugEvent.Id, positions, open, closed);
                    // C/D carry the NEW goal cell; -1 means the goal was cleared.
                    if (debugEvent.C < 0 || debugEvent.D < 0) return;
                    if (!positions.TryGetValue(debugEvent.Id, out long packed)) return;
                    open[debugEvent.Id] = new Segment { StartX = UnpackX(packed), StartY = UnpackY(packed) };
                    return;

                case DebugEventKind.MoveStop:
                case DebugEventKind.Death:
                    CloseSegment(debugEvent.Id, positions, open, closed);
                    return;
            }
        }

        private static void CloseSegment(
            uint id,
            Dictionary<uint, long> positions,
            Dictionary<uint, Segment> open,
            Dictionary<uint, Closed> closed)
        {
            if (!open.TryGetValue(id, out Segment segment)) return;
            open.Remove(id);
            if (!positions.TryGetValue(id, out long packed)) return;

            long straight = Length(UnpackX(packed) - segment.StartX, UnpackY(packed) - segment.StartY);
            if (straight <= 0 || segment.Walked <= 0) return;

            if (!closed.TryGetValue(id, out Closed sums))
            {
                sums = new Closed();
                closed[id] = sums;
            }
            sums.Walked += segment.Walked;
            sums.Straight += straight;
            sums.Count++;
        }

        /// <summary>Euclidean length of a Q16.16 vector, in Q16.16 — integer square root, no double anywhere.</summary>
        private static long Length(long dx, long dy)
        {
            return IntSqrt(dx * dx + dy * dy);
        }

        /// <summary>Newton's method on longs. Deterministic, and free of the one type this repo does not allow.</summary>
        private static long IntSqrt(long value)
        {
            if (value <= 0) return 0;

            long guess = value;
            long next = (guess + 1) / 2;
            while (next < guess)
            {
                guess = next;
                next = (guess + value / guess) / 2;
            }
            return guess;
        }

        private static long Pack(long x, long y) => ((x & 0xFFFFFFFFL) << 32) | (y & 0xFFFFFFFFL);
        private static long UnpackX(long packed) => (int)(packed >> 32);
        private static long UnpackY(long packed) => (int)(packed & 0xFFFFFFFFL);

        private sealed class Segment
        {
            public long StartX;
            public long StartY;
            public long Walked;
        }

        private sealed class Closed
        {
            public long Walked;
            public long Straight;
            public int Count;
        }
    }
}
