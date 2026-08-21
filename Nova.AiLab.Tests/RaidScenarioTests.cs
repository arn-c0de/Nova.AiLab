using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The field raid (issue #101). Four things are pinned: that the scenario
    /// is a measurement at all (reproducible, no refused order), that the
    /// geometry the whole reading rests on is what it claims to be, and the two
    /// readings themselves.
    /// <para>
    /// THE LAST TWO PIN A DEFECT, ON PURPOSE. They describe what the AI does
    /// today, not what it should do — the day the defence rule changes they go
    /// red, and that is the signal, not a break. A lab test that only asserts
    /// harmless things cannot tell anybody that the behaviour moved.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class RaidScenarioTests
    {
        private static RaidSpec Spec(RaidField field, int guardDistance, int delay = 0) => new RaidSpec
        {
            Field = field,
            GuardDistanceCells = guardDistance,
            RaidDelayTicks = delay,
            TickBudget = 600,
            GuardCount = 6,
        };

        [Test]
        public void ARaidIsReproducible()
        {
            RaidResult first = RaidScenarios.Run(Spec(RaidField.Start, 4));
            RaidResult second = RaidScenarios.Run(Spec(RaidField.Start, 4));

            Assert.That(second.FinalStateHash, Is.EqualTo(first.FinalStateHash),
                "same spec, same run — a raid that drifts measures the lab, not the AI");
            Assert.That(second.ReturnFireTick, Is.EqualTo(first.ReturnFireTick));
            Assert.That(second.HarvesterEndHealth, Is.EqualTo(first.HarvesterEndHealth));
        }

        [Test]
        public void NoOrderIsRefused()
        {
            foreach (RaidField field in new[] { RaidField.Start, RaidField.Expansion })
            {
                RaidResult result = RaidScenarios.Run(Spec(field, 4));
                Assert.That(result.RejectedOrders, Is.Zero,
                    $"{field}: a refused order makes the row a description of a command that never ran");
            }
        }

        [Test]
        public void TheStartFieldIsInsideTheHomeRadius_AndTheExpansionFieldIsNot()
        {
            RaidResult start = RaidScenarios.Run(Spec(RaidField.Start, 4));
            RaidResult expansion = RaidScenarios.Run(Spec(RaidField.Expansion, 4));

            // This is the number issue #101 assumed the other way round: it
            // argued the field lies outside DefendHomeCells, which holds for
            // the expansion and not for the start field two cells off the HQ.
            Assert.That(start.RaidToHomeCells, Is.LessThanOrEqualTo(start.DefendHomeCells),
                "the start field sits inside the home radius — the defence rule CAN see a raid there");
            Assert.That(expansion.RaidToHomeCells, Is.GreaterThan(expansion.DefendHomeCells),
                "the expansion field sits outside it — no anchor covers a raid there");
        }

        [Test]
        public void AnIdleArmyAnswers_ButTooLateForTheHarvester()
        {
            RaidSpec spec = Spec(RaidField.Start, 4);
            spec.TickBudget = 1200;
            RaidResult result = RaidScenarios.Run(spec);

            Assert.That(result.ReturnFireTick, Is.GreaterThanOrEqualTo(0),
                "with nothing else to do the wave walks over to the only visible enemy — " +
                "that is the ATTACK goal doing it, not a defence rule");

            // The half of the answer that matters for the issue: it is not that
            // nothing ever comes, it is that what comes arrives after the thing
            // it would have protected is gone. Four cells away, and the
            // harvester is dead hundreds of ticks before the first shot back.
            Assert.That(result.HarvesterDied, Is.True);
            Assert.That(result.ReturnFireTick, Is.GreaterThan(result.HarvesterDeathTick),
                "the harvester dies first — a reaction this late is not a defence");
        }

        [Test]
        public void ACommittedArmyDoesNotAnswer_EvenWithTheHomeThreatFlagUp()
        {
            // Long enough for the seat to gather a wave and march it off.
            RaidResult result = RaidScenarios.Run(Spec(RaidField.Start, 10, delay: 1500));

            Assert.That(result.RaiderVisibleTicks, Is.GreaterThan(0),
                "the raider is seen the whole time — this is not a vision problem");
            Assert.That(result.HomeThreatenedSeen, Is.True,
                "two cells off the HQ the home-threat flag is up");
            Assert.That(result.MaxCommitted, Is.GreaterThan(0),
                "and the army is committed to a wave");
            Assert.That(result.DefendHomeTick, Is.LessThan(0),
                "so DefendHome never fires: ResolveGoal tests HomeThreatened AND !committed. " +
                "The raid is seen, the flag is up, and the rule stays silent by construction");
            Assert.That(result.ReturnFireTick, Is.LessThan(0), "nothing comes back");
        }
    }
}
