using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Nova.Simulation.State;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The guard on <see cref="MatchSpec.Clone"/>.
    /// <para>
    /// WHY A REFLECTION TEST AND NOT A LIST OF ASSERTS. A hand-written
    /// assertion per field has the same defect the code had: the next field is
    /// added to the type and to neither. The sweep and the tournament each
    /// carried their own copy of this, one of them missing two fields, and
    /// nothing anywhere said so — the sample run a report links into could
    /// differ from the run whose numbers it prints, silently.
    /// </para>
    /// <para>
    /// So the test walks the fields the type HAS, and fails on the one that
    /// does not survive the copy, by name.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MatchSpecCloneTests
    {
        /// <summary>
        /// A spec whose every field differs from the default, so a field the
        /// copy forgets shows up as the default instead of matching by luck.
        /// </summary>
        private static MatchSpec Distinctive()
        {
            var spec = new MatchSpec
            {
                Seed = 0x0BADC0DEUL,
                TickBudget = 4321,
                MapWidth = 96,
                MapHeight = 64,
                EntityCapacity = 512,
                StartingCreditsAE = 7_777_777L,
                HashIntervalTicks = 111,
                TraceIntervalTicks = 22,
                ViewIntervalTicks = 33,
                TrackIntervalTicks = 4,
                RecordFog = true,
                CountIntents = true,
                Slots = MatchSpec.DefaultSlots(2),
            };
            spec.Slots[1].Controller = SlotController.Scripted;
            spec.Slots[1].ProfileId = "wave-6";
            return spec;
        }

        [Test]
        public void Clone_CarriesEveryFieldOfTheType()
        {
            MatchSpec source = Distinctive();
            MatchSpec copy = source.Clone();

            var missed = new List<string>();
            foreach (FieldInfo field in typeof(MatchSpec).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // The slot array is a DEEP copy and is checked below; comparing
                // references here would report a correct copy as a defect.
                if (field.Name == nameof(MatchSpec.Slots)) continue;

                object a = field.GetValue(source);
                object b = field.GetValue(copy);
                if (!Equals(a, b)) missed.Add($"{field.Name}: {a} became {b}");
            }

            Assert.That(missed, Is.Empty,
                "a field that does not survive Clone() is a field a sweep or a comparison would " +
                "silently run without: " + string.Join(", ", missed));
        }

        [Test]
        public void Clone_CopiesTheSlots_InsteadOfSharingThem()
        {
            MatchSpec source = Distinctive();
            MatchSpec copy = source.Clone();

            Assert.That(copy.Slots, Is.Not.SameAs(source.Slots), "the array itself must not be shared");
            Assert.That(copy.Slots.Length, Is.EqualTo(source.Slots.Length));

            for (int i = 0; i < source.Slots.Length; i++)
            {
                Assert.That(copy.Slots[i], Is.Not.SameAs(source.Slots[i]), $"slot {i} must be its own object");
                Assert.That(copy.Slots[i].Slot, Is.EqualTo(source.Slots[i].Slot));
                Assert.That(copy.Slots[i].Faction, Is.EqualTo(source.Slots[i].Faction));
                Assert.That(copy.Slots[i].Controller, Is.EqualTo(source.Slots[i].Controller));
                Assert.That(copy.Slots[i].ProfileId, Is.EqualTo(source.Slots[i].ProfileId));
            }

            // Two matches run in parallel off one template: a mutation on the
            // copy that reached the template would be shared state between
            // them, which is the exact bug the sweep's double-check hunts.
            copy.Slots[0].Faction = FactionId.Legion;
            Assert.That(source.Slots[0].Faction, Is.EqualTo(FactionId.Alliance));
        }

        [Test]
        public void WithSeed_ChangesTheSeedAndNothingElse()
        {
            MatchSpec source = Distinctive();
            MatchSpec copy = source.WithSeed(0xFEEDUL);

            Assert.That(copy.Seed, Is.EqualTo(0xFEEDUL));
            Assert.That(source.Seed, Is.EqualTo(0x0BADC0DEUL), "the template must not move");

            foreach (FieldInfo field in typeof(MatchSpec).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name == nameof(MatchSpec.Seed) || field.Name == nameof(MatchSpec.Slots)) continue;
                Assert.That(field.GetValue(copy), Is.EqualTo(field.GetValue(source)), field.Name);
            }
        }

        [Test]
        public void ACloneRunsTheIdenticalMatch()
        {
            // The point of all of the above, stated as behaviour: a sweep runs
            // clones, and a clone that differs from its template is a row in
            // the table that measured something else.
            var spec = new MatchSpec { TickBudget = 300, HashIntervalTicks = 50 };

            MatchRunResult original = MatchRun.Execute(spec);
            MatchRunResult cloned = MatchRun.Execute(spec.Clone());

            Assert.That(SweepRunner.Compare(original, cloned), Is.Null,
                "the clone diverged from its template");
        }
    }
}
