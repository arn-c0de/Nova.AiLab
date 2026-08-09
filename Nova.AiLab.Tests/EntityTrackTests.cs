using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Nova.Simulation.State;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The position track: where every entity actually walked.
    /// <para>
    /// Two properties carry everything the trail in the player is worth. It
    /// must not change the run — the same hard condition every observer in
    /// this lab carries — and it must reconstruct to the SAME positions the
    /// view frames carry, verbatim, not merely close. A route that is nearly
    /// right is the failure mode LAUFROUTEN.md rejected the nearest-neighbour
    /// reconstruction over: it still looks like an observation.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EntityTrackTests
    {
        private const ulong Seed = 0xA17E57DE57UL;
        private const int ShortBudget = 2500;

        private static MatchSpec ShortSpec() => new MatchSpec { Seed = Seed, TickBudget = ShortBudget };

        /// <summary>
        /// A KEYFRAME EVERY 500 TICKS AT WORST, whatever the track interval is.
        /// <para>
        /// The keyframe used to be tested as <c>tick % 500 == 0</c> while the
        /// recorder is only CALLED on <c>tick % TrackIntervalTicks == 0</c>, so
        /// the real gap was lcm(interval, 500): 1.500 ticks at interval 3,
        /// 3.500 at interval 7, and at interval 11 a 4.000-tick match got a
        /// single keyframe at tick 0. The delta chain then has no restart point
        /// and the scrubber has to replay it from the beginning — the exact
        /// thing keyframes are in the file to prevent.
        /// </para>
        /// <para>
        /// Interval 3 on purpose: the two existing tests use 1 and 10, and both
        /// of those divide 500, which is why neither of them could see it.
        /// </para>
        /// </summary>
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(11)]
        public void KeyframesSurviveAThinnedTrack(int trackEvery)
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;
            spec.TrackIntervalTicks = trackEvery;

            var keyframeTicks = new List<uint>();
            foreach (TrackFrame frame in MatchRun.Execute(spec).Tracks)
            {
                if (frame.IsKeyframe) keyframeTicks.Add(frame.Tick);
            }

            Assert.That(keyframeTicks, Is.Not.Empty, "a track without a keyframe cannot be scrubbed into");
            Assert.That(keyframeTicks[0], Is.Zero, "the opening capture is always a keyframe");

            // The gap may exceed 500 by at most one capture — the boundary can
            // fall between two captured ticks, and the NEXT one carries it.
            for (int i = 1; i < keyframeTicks.Count; i++)
            {
                Assert.That(keyframeTicks[i] - keyframeTicks[i - 1],
                    Is.LessThanOrEqualTo((uint)(EntityTrackRecorder.KeyframeIntervalTicks + trackEvery)),
                    $"keyframes {keyframeTicks[i - 1]} and {keyframeTicks[i]} sit further apart than the " +
                    $"interval promises at --track-every {trackEvery}");
            }
        }

        // ================================================================
        // (a) THE HARD CONDITION: a pure observer
        // ================================================================

        [Test]
        public void RecordingTrackAndEvents_DoesNotChangeTheHashChain()
        {
            // Isolated against the VIEW window, which is a proven observer
            // already: both runs record frames, only one records the track and
            // the events. Anything that moved would be theirs.
            MatchSpec viewOnly = ShortSpec();
            viewOnly.HashIntervalTicks = 100;
            viewOnly.ViewIntervalTicks = 25;
            viewOnly.TrackIntervalTicks = 0;

            MatchSpec tracked = ShortSpec();
            tracked.HashIntervalTicks = 100;
            tracked.ViewIntervalTicks = 25;
            tracked.TrackIntervalTicks = 1;

            MatchRunResult without = MatchRun.Execute(viewOnly);
            MatchRunResult with = MatchRun.Execute(tracked);

            Assert.That(without.Tracks, Is.Empty, "--track-every 0 must record nothing");
            Assert.That(without.Events, Is.Empty);
            Assert.That(with.Tracks.Count, Is.GreaterThan(0), "the tracked run must actually have recorded");
            Assert.That(with.Events.Count, Is.GreaterThan(0));

            Assert.That(SweepRunner.Compare(without, with), Is.Null,
                "track and event log are pure observers: they read the committed state, never write back, " +
                "and are no part of the tick order, the state hash or a snapshot");
        }

        // ================================================================
        // (b) THE TRACK ITSELF
        // ================================================================

        [Test]
        public void Track_ReconstructsTheViewPositionsVerbatim()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 25;

            MatchRunResult result = MatchRun.Execute(spec);
            var positions = new Dictionary<uint, (int X, int Y)>();
            int trackIndex = 0, compared = 0;

            foreach (ViewFrame frame in result.View)
            {
                while (trackIndex < result.Tracks.Count && result.Tracks[trackIndex].Tick <= frame.Tick)
                {
                    Apply(result.Tracks[trackIndex], positions);
                    trackIndex++;
                }

                foreach (ViewEntity entity in frame.Entities)
                {
                    Assert.That(positions.ContainsKey(entity.Id), Is.True,
                        $"tick {frame.Tick}: entity {entity.Id} is on the map but not in the track");
                    Assert.That(positions[entity.Id], Is.EqualTo((entity.XRaw, entity.YRaw)),
                        $"tick {frame.Tick}, entity {entity.Id}: the track must rebuild the exact position, " +
                        "not one that is nearly right");
                    compared++;
                }
            }

            Assert.That(compared, Is.GreaterThan(100), "the run has to have produced something to compare");
        }

        /// <summary>
        /// The event stream has to rebuild the SAME picture the frames carry.
        /// <para>
        /// The player draws every tick from track and events and only takes fog
        /// and the header row from a frame, so the two descriptions of one run
        /// must not drift apart. They did: the retreat marker was "combat units
        /// under 25 %" in <see cref="ViewRecorder"/> and "anything that is not a
        /// building" in <see cref="DebugEventLog"/>, and a damaged BUILDER came
        /// out marked in one artifact and unmarked in the other. Five frames of
        /// one match, one unit — small enough to look like noise and wrong
        /// enough to make a page claim a retreat rule would act on a builder.
        /// </para>
        /// </summary>
        [Test]
        public void ReplayingTheEventsRebuildsTheFlagsAndShapesOfEveryFrame()
        {
            MatchSpec spec = ShortSpec();
            spec.TickBudget = 6000;
            spec.ViewIntervalTicks = 25;

            MatchRunResult result = MatchRun.Execute(spec);
            var role = new Dictionary<uint, UnitRole>();
            var site = new HashSet<uint>();
            var moving = new HashSet<uint>();
            var cargo = new HashSet<uint>();
            var below = new HashSet<uint>();

            int eventIndex = 0, compared = 0;
            foreach (ViewFrame frame in result.View)
            {
                while (eventIndex < result.Events.Count && result.Events[eventIndex].Tick <= frame.Tick)
                {
                    DebugEvent e = result.Events[eventIndex++];
                    switch (e.Kind)
                    {
                        case DebugEventKind.Spawn: role[e.Id] = e.Role; break;
                        case DebugEventKind.Death:
                            role.Remove(e.Id); site.Remove(e.Id); moving.Remove(e.Id);
                            cargo.Remove(e.Id); below.Remove(e.Id);
                            break;
                        case DebugEventKind.SiteOpen: site.Add(e.Id); break;
                        case DebugEventKind.SiteDone: site.Remove(e.Id); role[e.Id] = e.Role; break;
                        case DebugEventKind.MoveStart: moving.Add(e.Id); break;
                        case DebugEventKind.MoveStop: moving.Remove(e.Id); break;
                        case DebugEventKind.CargoFull: cargo.Add(e.Id); break;
                        case DebugEventKind.CargoDelivered: cargo.Remove(e.Id); break;
                        case DebugEventKind.RetreatBelow: below.Add(e.Id); break;
                        case DebugEventKind.RetreatAbove: below.Remove(e.Id); break;
                    }
                }

                foreach (ViewEntity entity in frame.Entities)
                {
                    compared++;
                    Assert.That(role, Does.ContainKey(entity.Id),
                        $"tick {frame.Tick}: entity {entity.Id} is on the map but not in the event stream");

                    Assert.That(ViewRecorder.ShapeOf(role[entity.Id], site.Contains(entity.Id)),
                        Is.EqualTo(entity.Shape),
                        $"tick {frame.Tick}, entity {entity.Id}: replayed shape differs from the frame");

                    int flags = (cargo.Contains(entity.Id) ? ViewFlags.ReturningCargo : 0)
                                | (below.Contains(entity.Id) ? ViewFlags.BelowRetreatThreshold : 0)
                                | (moving.Contains(entity.Id) ? ViewFlags.Moving : 0);
                    Assert.That(flags, Is.EqualTo(entity.Flags),
                        $"tick {frame.Tick}, entity {entity.Id}: replayed flags differ from the frame");
                }
            }

            Assert.That(compared, Is.GreaterThan(500));
        }

        [Test]
        public void Keyframes_CarryEveryLivingEntityAbsolutely()
        {
            // Without them a page that scrubs to a late tick has to replay
            // every delta from tick 0.
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            MatchRunResult result = MatchRun.Execute(spec);
            var alive = new HashSet<uint>();
            int keyframes = 0;

            foreach (TrackFrame frame in result.Tracks)
            {
                foreach (TrackSample sample in frame.Absolute) alive.Add(sample.Id);
                foreach (uint ended in frame.Ended) alive.Remove(ended);

                if (!frame.IsKeyframe) continue;
                keyframes++;

                Assert.That(frame.Tick % EntityTrackRecorder.KeyframeIntervalTicks, Is.EqualTo(0u));
                var absolute = new HashSet<uint>();
                foreach (TrackSample sample in frame.Absolute) absolute.Add(sample.Id);
                Assert.That(absolute, Is.SupersetOf(alive),
                    $"keyframe at tick {frame.Tick} must carry every living entity absolutely");
            }

            Assert.That(keyframes, Is.GreaterThan(1));
        }

        [Test]
        public void EveryDeltaHasAnAbsoluteBeforeIt()
        {
            // A delta against a position nobody knows is not a route, it is a
            // number. The page would silently drop it; the test does not.
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            var known = new HashSet<uint>();
            foreach (TrackFrame frame in MatchRun.Execute(spec).Tracks)
            {
                foreach (TrackSample sample in frame.Delta)
                {
                    Assert.That(known.Contains(sample.Id), Is.True,
                        $"tick {frame.Tick}: delta for {sample.Id} without an absolute anchor");
                }
                foreach (TrackSample sample in frame.Absolute) known.Add(sample.Id);
                foreach (uint ended in frame.Ended) known.Remove(ended);
            }
        }

        [Test]
        public void TrackEvery_ThinsThePositionsButNeverTheEvents()
        {
            MatchSpec dense = ShortSpec();
            dense.ViewIntervalTicks = 100;

            MatchSpec sparse = ShortSpec();
            sparse.ViewIntervalTicks = 100;
            sparse.TrackIntervalTicks = 10;

            MatchRunResult denseResult = MatchRun.Execute(dense);
            MatchRunResult sparseResult = MatchRun.Execute(sparse);

            Assert.That(sparseResult.Tracks.Count, Is.LessThan(denseResult.Tracks.Count));
            Assert.That(sparseResult.Events.Count, Is.EqualTo(denseResult.Events.Count),
                "an edge between two samples cannot be recovered, so events are read every tick regardless");
        }

        [Test]
        public void TrackJson_ContainsNoFloatingPointNumber()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            foreach (TrackFrame frame in MatchRun.Execute(spec).Tracks)
            {
                string line = frame.ToJsonLine();
                Assert.That(line, Does.Not.Contain("."),
                    "positions travel as Q16.16 raw integers; a decimal point means a float escaped:\n" +
                    line.Substring(0, Math.Min(200, line.Length)));
            }
        }

        // ================================================================
        // (c) THE ARTIFACTS
        // ================================================================

        [Test]
        public void TrackArtifacts_TravelWithTheFramesAndOnlyWithThem()
        {
            MatchSpec spec = ShortSpec();
            spec.ViewIntervalTicks = 100;

            string directory = Path.Combine(Path.GetTempPath(), "nova-ailab-tests", Guid.NewGuid().ToString("N"));
            try
            {
                RunArtifacts.Write(directory, spec, MatchRun.Execute(spec));
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.TracksFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.EventsFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.UnitsFileName)), Is.True);

                string player = File.ReadAllText(Path.Combine(directory, HtmlPlayer.FileName));
                Assert.That(player, Does.Contain(RunArtifacts.TracksFileName));
                Assert.That(player, Does.Contain(RunArtifacts.EventsFileName));
                Assert.That(player, Does.Contain(RunArtifacts.UnitsFileName));
                // "No build, no server, no dependency" holds for the grown page too.
                Assert.That(player, Does.Not.Contain("http://"));
                Assert.That(player, Does.Not.Contain("https://"));
                Assert.That(player, Does.Not.Contain("<script src"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void NoViewRequested_WritesNoTrackArtifacts()
        {
            MatchSpec spec = ShortSpec();
            string directory = Path.Combine(Path.GetTempPath(), "nova-ailab-tests", Guid.NewGuid().ToString("N"));
            try
            {
                RunArtifacts.Write(directory, spec, MatchRun.Execute(spec));
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.TracksFileName)), Is.False);
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.EventsFileName)), Is.False);
                Assert.That(File.Exists(Path.Combine(directory, RunArtifacts.UnitsFileName)), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        private static void Apply(TrackFrame frame, Dictionary<uint, (int X, int Y)> positions)
        {
            foreach (TrackSample sample in frame.Absolute) positions[sample.Id] = (sample.X, sample.Y);
            foreach (TrackSample sample in frame.Delta)
            {
                (int X, int Y) previous = positions[sample.Id];
                positions[sample.Id] = (previous.X + sample.X, previous.Y + sample.Y);
            }
            foreach (uint ended in frame.Ended) positions.Remove(ended);
        }
    }
}
