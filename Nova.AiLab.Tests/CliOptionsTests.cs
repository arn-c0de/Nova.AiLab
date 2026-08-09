using System;
using System.Reflection;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The command line, which had no test at all until Cli/ was compiled into
    /// this assembly.
    /// <para>
    /// The rules under test are not conveniences. A lab exists to produce
    /// numbers somebody else can reproduce, and every case here is a way the
    /// old parser produced a number under settings it never applied: an
    /// interval that silently switched a stream off, a flag the mode does not
    /// read, a group size of zero. <see cref="SpecFile"/> has refused all of
    /// this in a spec FILE since it was written — the flags override the file,
    /// so the file's discipline was only ever half the gate.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class CliOptionsTests
    {
        private static Options Parse(params string[] args) => Options.Parse(args, args[0]);

        private static string MessageOf(params string[] args)
        {
            ArgumentException thrown = Assert.Throws<ArgumentException>(() => Parse(args));
            return thrown.Message;
        }

        // ---- intervals ---------------------------------------------------

        [Test]
        public void NegativeInterval_IsRefused_InsteadOfWrappingToNever()
        {
            // (uint)(-5) is 4.294.967.291 and no tick is ever divisible by it,
            // so the old parser accepted this, recorded nothing, and finished
            // green without a word about why the artifacts were missing.
            Assert.That(MessageOf("match", "--view-every", "-5"), Does.Contain("--view-every"));
            Assert.That(MessageOf("match", "--trace-every", "-1"), Does.Contain("negative"));
            Assert.That(MessageOf("match", "--hash-every", "-1"), Does.Contain("negative"));
            Assert.That(MessageOf("match", "--track-every", "-1"), Does.Contain("negative"));
        }

        [Test]
        public void ZeroInterval_IsAccepted_BecauseZeroMeansOff()
        {
            Options options = Parse("match", "--view-every", "0", "--trace-every", "0");
            Assert.That(options.Spec.ViewIntervalTicks, Is.Zero);
            Assert.That(options.Spec.TraceIntervalTicks, Is.Zero);
        }

        // ---- counts ------------------------------------------------------

        [Test]
        public void NonPositiveCounts_AreRefused()
        {
            Assert.That(MessageOf("movement", "--group", "0"), Does.Contain("--group"));
            Assert.That(MessageOf("duel", "--units", "0"), Does.Contain("--units"));
            Assert.That(MessageOf("match", "--ticks", "0"), Does.Contain("--ticks"));
            Assert.That(MessageOf("match", "--repeat", "0"), Does.Contain("--repeat"));
            Assert.That(MessageOf("sweep", "--seeds", "0"), Does.Contain("--seeds"));
            Assert.That(MessageOf("sweep", "--parallel", "0"), Does.Contain("--parallel"));
        }

        // ---- which flags a mode may carry --------------------------------

        [Test]
        public void AFlagTheModeDoesNotRead_IsRefused_NotIgnored()
        {
            // The arena builds its own spec and never looks at a view interval,
            // so this used to parse, run, and write no frames.
            string message = MessageOf("duel", "--view-every", "25");
            Assert.That(message, Does.Contain("does not apply to mode 'duel'"));
            Assert.That(message, Does.Contain("--units"), "the message names what the mode DOES take");
        }

        [Test]
        public void TheTournamentSeatsItsOwnCandidates_SoCompareRefusesProfileFlags()
        {
            Assert.That(MessageOf("compare", "--profile", "wave-6"), Does.Contain("does not apply"));
            Assert.That(MessageOf("compare", "--slots", "4"), Does.Contain("does not apply"));
        }

        [Test]
        public void AnUnknownFlag_IsATypo_AndSaysSo()
        {
            Assert.That(MessageOf("match", "--vieweveryy", "25"), Does.Contain("unknown option"));
        }

        [Test]
        public void AnUnknownMode_IsNamedAsAMode()
        {
            Assert.That(MessageOf("mtach", "--ticks", "100"), Does.Contain("unknown mode"));
        }

        [Test]
        public void AFlagWithoutItsValue_IsRefused()
        {
            Assert.That(MessageOf("match", "--ticks"), Does.Contain("needs a value"));
        }

        // ---- the switches ------------------------------------------------

        [Test]
        public void SwitchFlags_DoNotSwallowTheNextArgument()
        {
            Options options = Parse("match", "--fog", "--ticks", "500");
            Assert.That(options.Spec.RecordFog, Is.True);
            Assert.That(options.Spec.TickBudget, Is.EqualTo(500), "--fog must not have eaten '--ticks'");
        }

        [Test]
        public void Watch_TurnsTheViewOn_AtTheDecisionCadence()
        {
            Options options = Parse("match", "--watch");
            Assert.That(options.Watch, Is.True);
            Assert.That(options.Spec.ViewIntervalTicks, Is.EqualTo(20));
        }

        // ---- mode defaults ------------------------------------------------

        [Test]
        public void DuelAndMovement_AreSeconds_NotAMatch()
        {
            Assert.That(Parse("duel").Spec.TickBudget, Is.EqualTo(3000));
            Assert.That(Parse("movement").Spec.TickBudget, Is.EqualTo(3000));
            Assert.That(Parse("duel", "--ticks", "900").Spec.TickBudget, Is.EqualTo(900),
                "an explicit budget still wins");
        }

        [Test]
        public void Compare_DefaultsToOneSeed_BecauseTheSeedAxisIsEmpty()
        {
            Assert.That(Parse("compare").SeedCount, Is.EqualTo(1));
            Assert.That(Parse("sweep").SeedCount, Is.EqualTo(8));
        }

        // ---- seeds ---------------------------------------------------------

        [Test]
        public void SeedsAreReadAsDecimalOrHex()
        {
            Assert.That(Parse("match", "--seed", "0xA17E57DE57").Spec.Seed, Is.EqualTo(0xA17E57DE57UL));
            Assert.That(Parse("match", "--seed", "42").Spec.Seed, Is.EqualTo(42UL));
            Assert.That(MessageOf("match", "--seed", "beef"), Does.Contain("not a valid seed"));
        }

        // ---- profiles -------------------------------------------------------

        [Test]
        public void AnUnknownProfile_NamesTheKnownOnes()
        {
            string message = MessageOf("match", "--profile", "not-a-profile");
            Assert.That(message, Does.Contain("unknown"));
            Assert.That(message, Does.Contain("ms1-canonical"), "it has to say what CAN be picked");
        }

        [Test]
        public void ProfileZeroAndOne_MakeAOneSidedMatch()
        {
            Options options = Parse("match", "--profile0", "wave-off", "--profile1", "canonical");
            Assert.That(options.Spec.Slots[0].ProfileId, Is.EqualTo("wave-off"));
            Assert.That(options.Spec.Slots[1].ProfileId, Is.EqualTo(SlotSpec.CanonicalProfileId));
        }

        // ---- the help text is a promise the parser keeps ---------------------

        [Test]
        public void EveryFlagTheParserKnows_AppearsInTheHelpText()
        {
            // Usage.cs groups the flags by mode and the parser now ENFORCES
            // that grouping. A flag added to one and not the other makes the
            // help text lie, which is the failure this catches.
            FieldInfo field = typeof(Options).GetField("AllFlags",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "AllFlags is the parser's own inventory");

            var all = (System.Collections.Generic.HashSet<string>)field.GetValue(null);
            foreach (string flag in all)
            {
                Assert.That(Usage.Text, Does.Contain(flag), $"'{flag}' is accepted but undocumented");
            }
        }
    }
}
