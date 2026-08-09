using System;
using System.Collections.Generic;
using System.Text;
using Nova.Simulation.CommandsV1;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>One entity's position in one track frame — Q16.16 raw, absolute or delta.</summary>
    public struct TrackSample
    {
        public uint Id;
        public int X;
        public int Y;
    }

    /// <summary>
    /// One tick of the position track: what moved, what appeared, what ended.
    /// <para>
    /// Three lists rather than one, so the page can decode a line without
    /// looking ahead: <c>a</c> carries absolute positions (a new id, or every
    /// id on a keyframe line), <c>d</c> carries the step since the same id's
    /// previous sample, and <c>x</c> names the ids whose track ends here.
    /// A unit that did not move appears in none of them and keeps its
    /// position — standing still is the common case and costs nothing.
    /// </para>
    /// </summary>
    public sealed class TrackFrame : INdjsonLine
    {
        public uint Tick;

        /// <summary>True on a keyframe line: every living id is in <see cref="Absolute"/>.</summary>
        public bool IsKeyframe;

        public List<TrackSample> Absolute = new List<TrackSample>();
        public List<TrackSample> Delta = new List<TrackSample>();
        public List<uint> Ended = new List<uint>();

        public bool IsEmpty => !IsKeyframe && Absolute.Count == 0 && Delta.Count == 0 && Ended.Count == 0;

        public string ToJsonLine()
        {
            var json = new StringBuilder(32 + (Absolute.Count + Delta.Count) * 24);
            json.Append("{\"t\":").Append(Tick);
            if (IsKeyframe) json.Append(",\"k\":1");
            AppendSamples(json, "a", Absolute);
            AppendSamples(json, "d", Delta);
            if (Ended.Count > 0)
            {
                json.Append(",\"x\":[");
                for (int i = 0; i < Ended.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append(Ended[i]);
                }
                json.Append(']');
            }
            json.Append('}');
            return json.ToString();
        }

        private static void AppendSamples(StringBuilder json, string key, List<TrackSample> samples)
        {
            if (samples.Count == 0) return;
            json.Append(",\"").Append(key).Append("\":[");
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0) json.Append(',');
                TrackSample s = samples[i];
                json.Append('[').Append(s.Id).Append(',').Append(s.X).Append(',').Append(s.Y).Append(']');
            }
            json.Append(']');
        }
    }

    /// <summary>
    /// Records where every entity actually WALKED, one sample per tick.
    /// <para>
    /// WHY IT IS NOT THE VIEW FRAME. A view frame is a picture and costs a
    /// picture: at <c>--view-every 25</c> a unit crosses two to three cells
    /// between two frames, and a route drawn from those points is a straight
    /// line the unit never took. The track carries only identity and position,
    /// so it can afford EVERY tick — and then the route is what happened, not
    /// what fits between two pictures.
    /// </para>
    /// <para>
    /// MEASURED, NOT ESTIMATED: a decided match carries 28–37 entities. At one
    /// sample per tick that is a few megabytes, which is why this records
    /// verbatim instead of thinning, smoothing or interpolating. Nothing here
    /// is guessed — the identity is the entity id, not a nearest-neighbour
    /// match between two point clouds (LAUFROUTEN.md section 2A, rejected).
    /// </para>
    /// <para>
    /// PURE OBSERVER, the same hard condition <see cref="ViewRecorder"/> and
    /// <see cref="TraceCollector"/> carry: it reads the committed state after
    /// <c>StepTick()</c>, never writes back, and is no part of the tick order,
    /// the state hash or a snapshot. Asserted in <c>EntityTrackTests</c>.
    /// </para>
    /// </summary>
    public sealed class EntityTrackRecorder
    {
        /// <summary>
        /// Ticks between keyframe lines. A page that scrubs to tick 20.000
        /// would otherwise have to replay every delta from tick 0; with a
        /// keyframe it starts at the nearest one — the same reason a video
        /// stream carries them.
        /// <para>
        /// It is a CEILING on the gap, not a divisor of the tick number. The
        /// first capture at or past each boundary is the keyframe, because
        /// <see cref="MatchSpec.TrackIntervalTicks"/> decides which ticks are
        /// captured at all: tested as <c>tick % 500 == 0</c>, an interval that
        /// does not divide 500 pushed the keyframes out to every lcm(interval,
        /// 500) ticks — 1.500 at interval 3, 3.500 at interval 7 — and at
        /// interval 11 a 4.000-tick match got exactly one, at tick 0. The
        /// promise this constant makes would then have quietly stopped holding
        /// for every interval nobody happened to test.
        /// </para>
        /// </summary>
        public const int KeyframeIntervalTicks = 500;

        private readonly MultiSlotAiHost _host;

        /// <summary>First tick that is allowed to be a keyframe; 0, so the opening capture is one.</summary>
        private uint _nextKeyframeTick;

        // Shadow state per entity SLOT (not per id): the array index is the
        // pool index, and the version tells a reused slot from a survivor.
        private readonly bool[] _wasActive;
        private readonly ushort[] _lastVersion;
        private readonly uint[] _lastRaw;
        private readonly int[] _lastX;
        private readonly int[] _lastY;

        public EntityTrackRecorder(MultiSlotAiHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            int capacity = host.Entities.Capacity;
            _wasActive = new bool[capacity];
            _lastVersion = new ushort[capacity];
            _lastRaw = new uint[capacity];
            _lastX = new int[capacity];
            _lastY = new int[capacity];
        }

        /// <summary>One tick's samples. Ascending index scan — no dictionary order anywhere.</summary>
        public TrackFrame Capture(uint tick)
        {
            bool keyframe = tick >= _nextKeyframeTick;
            if (keyframe) _nextKeyframeTick = tick + KeyframeIntervalTicks;
            var frame = new TrackFrame { Tick = tick, IsKeyframe = keyframe };

            UnitState[] units = _host.Entities.RawUnits;
            for (int i = 0; i < units.Length; i++)
            {
                ref readonly UnitState u = ref units[i];
                bool sameUnit = _wasActive[i] && u.IsActive && u.Id.Version == _lastVersion[i];

                // A slot that was taken and is now taken by a DIFFERENT unit
                // ends the old track and opens a new one in the same tick.
                if (_wasActive[i] && !sameUnit) frame.Ended.Add(_lastRaw[i]);

                if (!u.IsActive)
                {
                    _wasActive[i] = false;
                    continue;
                }

                int x = u.Transform.PositionX.RawValue;
                int y = u.Transform.PositionY.RawValue;
                uint raw = UnitCommandStateView.ToRawEntityId(u.Id);

                if (!sameUnit || keyframe)
                {
                    frame.Absolute.Add(new TrackSample { Id = raw, X = x, Y = y });
                }
                else if (x != _lastX[i] || y != _lastY[i])
                {
                    frame.Delta.Add(new TrackSample { Id = raw, X = x - _lastX[i], Y = y - _lastY[i] });
                }

                _wasActive[i] = true;
                _lastVersion[i] = u.Id.Version;
                _lastRaw[i] = raw;
                _lastX[i] = x;
                _lastY[i] = y;
            }

            return frame;
        }
    }
}
