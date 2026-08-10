using System;
using System.Text.RegularExpressions;
using Nova.AI.Data;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The goal panel of the player page.
    /// <para>
    /// The page cannot be executed here — it is one file of HTML and JavaScript
    /// with no build step, deliberately — so what these tests hold is the seam
    /// between the page and everything it depends on. That is where it breaks
    /// silently: a renamed artifact, a goal added to the simulation, a placeholder
    /// that never got substituted. Each of those leaves a page that still opens,
    /// still draws, and quietly stops saying anything about the new case.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PlayerGoalPanelTests
    {
        private const ulong Seed = 0xA17E57DE57UL;

        private static string Page() => HtmlPlayer.Build(128, 128, Seed, MatchSpec.DefaultSlots(2));

        [Test]
        public void ThePageAsksForTheGoalFileByItsRealName()
        {
            string html = Page();

            Assert.That(html, Does.Not.Contain("__GOALS_FILE__"),
                "the goal file placeholder was never substituted");
            Assert.That(html, Does.Contain(RunArtifacts.GoalsFileName),
                "the page does not fetch the goal recording at all");
        }

        /// <summary>
        /// EVERY GOAL THE SIMULATION CAN PICK HAS A NAME ON THE PAGE, and it sits
        /// at the index of its own enum value.
        /// <para>
        /// The page indexes <c>GOAL_NAMES</c> with the raw number out of the
        /// recording, because <see cref="GoalKind"/> is shared vocabulary rather
        /// than a lab invention. Add a goal module over in <c>AI/</c> and without
        /// this test the panel would print <c>undefined</c> for it — on the one
        /// screen somebody opens to find out what the new module does.
        /// </para>
        /// </summary>
        [Test]
        public void EveryGoalOfTheSimulationHasANameAtItsOwnIndex()
        {
            string html = Page();
            Match names = Regex.Match(html, @"const GOAL_NAMES = \[(.*?)\];", RegexOptions.Singleline);
            Assert.That(names.Success, Is.True, "the page carries no goal name table");

            string[] listed = names.Groups[1].Value.Split(',');
            foreach (GoalKind goal in Enum.GetValues(typeof(GoalKind)))
            {
                int index = (int)goal;
                Assert.That(listed.Length, Is.GreaterThan(index),
                    $"{goal} is value {index} and the page's name table is shorter than that");
                if (goal == GoalKind.None) continue;

                string name = listed[index].Trim().Trim('\'');
                Assert.That(name.ToLowerInvariant(), Is.EqualTo(goal.ToString().ToLowerInvariant()),
                    $"the page calls goal {index} '{name}', the simulation calls it {goal}");
            }
        }

        /// <summary>
        /// The other side of every goal condition travels with the page.
        /// <para>
        /// The recording carries the MEASURED quantity — health, distances — and
        /// the profile carries what it was compared against. A tipping point is
        /// the difference of the two, so a page missing the second half can show
        /// what a unit is and not how close it is to being something else.
        /// </para>
        /// </summary>
        [Test]
        public void TheProfileValuesEveryGoalConditionComparesAgainstAreInThePage()
        {
            string html = Page();
            Match wave = Regex.Match(html, @"const WAVE = (\[.*?\]);", RegexOptions.Singleline);
            Assert.That(wave.Success, Is.True, "the page carries no rule table");

            foreach (string field in new[] { "retreatPercent", "dangerCells", "tolerance", "ring", "squad" })
            {
                Assert.That(wave.Groups[1].Value, Does.Contain("\"" + field + "\""),
                    $"the rule table has no {field}, so the panel cannot say how far off the next goal is");
            }
        }

        /// <summary>
        /// THE OLD EVENT IS NOT CALLED <c>goal</c> ANY MORE. It is the cell the
        /// movement walks towards, and the AI's goals now share the page with it;
        /// two different things under one word is a display error nobody
        /// recognises as one, because both readings make sense.
        /// </summary>
        [Test]
        public void TheWaypointEventIsNotCalledAGoal()
        {
            Assert.That(DebugEvent.NameOf(DebugEventKind.PathGoal), Is.EqualTo("pathGoal"));

            string html = Page();
            Assert.That(html, Does.Contain("'pathGoal'"), "the page does not know the renamed event");
            Assert.That(html, Does.Not.Match(@"case 'goal'"),
                "the page still handles an event called 'goal'");
        }

        /// <summary>
        /// The page stays one file. Adding a fifth artifact to fetch must not
        /// have added anything beside it.
        /// </summary>
        [Test]
        public void ThePageStaysSelfContained()
        {
            string html = Page();
            Assert.That(html, Does.Not.Contain("<link "));
            Assert.That(html, Does.Not.Contain("src=\"http"));
        }
    }
}
