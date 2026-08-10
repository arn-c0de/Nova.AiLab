using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Nova.Simulation.State;
using NUnit.Framework;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// The shared UI kit: one set of tokens and one set of icons for the player,
    /// the control page and the dashboard (<c>Nova.AiLab/report/uikit/</c>).
    /// <para>
    /// TWO WAYS THIS BREAKS SILENTLY, and both are checked here.
    /// </para>
    /// <para>
    /// The first is a page that ships WITHOUT the kit. The kit is embedded in
    /// the assembly and written into the generated page, so a wrong resource
    /// name or a lost <c>EmbeddedResource</c> item would leave a page that is
    /// still valid HTML and looks like a broken run.
    /// </para>
    /// <para>
    /// The second is a ROLE THE GAME HAS AND THE KIT DOES NOT. Add a unit role
    /// over in the definitions and the map would draw it as a nameless dot for
    /// as long as nobody notices. This test walks
    /// <see cref="UnitRole"/> and insists on a name and a symbol for every one
    /// of them — it is meant to fail on the commit that adds the role, not on
    /// the screenshot three weeks later.
    /// </para>
    /// </summary>
    public class UiKitTests
    {
        private const ulong Seed = 0xA17E57DE57UL;

        private static string Resource(string name)
        {
            Assembly assembly = typeof(HtmlPlayer).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                Assert.That(stream, Is.Not.Null, "the kit resource '" + name + "' must be embedded");
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string Page()
        {
            return HtmlPlayer.Build(128, 128, Seed, MatchSpec.DefaultSlots(2));
        }

        [Test]
        public void GeneratedPageCarriesTheKitInsteadOfItsPlaceholders()
        {
            string html = Page();

            Assert.That(html, Does.Not.Contain("<style>__UIKIT_CSS__"),
                "the CSS placeholder is still in the page");
            Assert.That(html, Does.Not.Contain("<script>__UIKIT_JS__"),
                "the JS placeholder is still in the page");

            Assert.That(html, Does.Contain("--slot-0"), "the seat colours come from the kit");
            Assert.That(html, Does.Contain("const ICONS"), "the icon set has to be in the page");
            Assert.That(html, Does.Contain("function iconSvg"), "the DOM side of the icon set");
            Assert.That(html, Does.Contain("function iconPath2D"), "the canvas side of the icon set");
        }

        /// <summary>
        /// A page that is one file stays one file. The player is dropped beside
        /// its artifacts and has to open with a double-click from a copied
        /// folder, so it may not reach for anything over the network or beside
        /// itself except the four artifact files it already documents.
        /// </summary>
        [Test]
        public void GeneratedPageStaysSelfContained()
        {
            string html = Page();
            Assert.That(html, Does.Not.Contain("<link "), "no stylesheet may be linked");
            Assert.That(html, Does.Not.Contain("src=\"http"), "nothing may be loaded over the network");
            Assert.That(html, Does.Not.Contain("uikit/"), "the kit is written in, not referenced");
        }

        [Test]
        public void EverySeatColourIsDefinedForEverySlotTheColourTableOffers()
        {
            string css = Resource("uikit.tokens.css");
            for (int slot = 0; slot < 8; slot++)
            {
                Assert.That(css, Does.Contain("--slot-" + slot + ":"),
                    "slot " + slot + " has no colour; the page reads eight of them");
            }
        }

        /// <summary>
        /// Red is damage. It used to be Legion as well — slot 1 was the same
        /// <c>#f85149</c> the map paints hits and deaths with, so a red dot was
        /// either a unit or a hit landing. Nothing else may take that colour
        /// back.
        /// </summary>
        [Test]
        public void NoSeatWearsTheColourOfDamage()
        {
            string css = Resource("uikit.tokens.css");
            foreach (Match match in Regex.Matches(css, @"--slot-\d:\s*(#[0-9a-fA-F]{6})"))
            {
                string colour = match.Groups[1].Value.ToLowerInvariant();
                Assert.That(colour, Is.Not.EqualTo("#f85149"), "a seat wears the damage red again");
                Assert.That(colour, Is.Not.EqualTo("#d03b3b"), "a seat wears the critical red");
                Assert.That(colour, Is.Not.EqualTo("#e66767"), "a seat wears the critical red");
            }
        }

        [Test]
        public void EveryUnitRoleHasANameAndASymbol()
        {
            string icons = Resource("uikit.icons.js");
            string html = Page();

            List<string> roleIcons = ArrayOf(icons, "const ROLE_ICON = ");
            List<string> roleNames = ArrayOf(html, "const ROLE_NAME = ");

            var known = new HashSet<string>();
            foreach (Match match in Regex.Matches(icons, @"'(role\.[A-Za-z]+)':"))
            {
                known.Add(match.Groups[1].Value);
            }

            foreach (UnitRole role in (UnitRole[])Enum.GetValues(typeof(UnitRole)))
            {
                int index = (int)role;
                Assert.That(roleNames.Count, Is.GreaterThan(index),
                    "role " + role + " has no name in the player's ROLE_NAME");
                Assert.That(roleIcons.Count, Is.GreaterThan(index),
                    "role " + role + " has no entry in ROLE_ICON — add one in report/uikit/icons.js");
                Assert.That(known, Does.Contain(roleIcons[index]),
                    "role " + role + " points at '" + roleIcons[index] + "', which no icon defines");
                Assert.That(roleNames[index].ToLowerInvariant(), Is.EqualTo(role.ToString().ToLowerInvariant()),
                    "role " + index + " is " + role + " and the page calls it '" + roleNames[index] + "'");
            }

            // Und die zwei, die keine Rolle sind und trotzdem gezeichnet werden.
            Assert.That(known, Does.Contain("role.site"), "a construction site is drawn too");
            Assert.That(known, Does.Contain("role.unknown"), "an unknown role must stay visible");
        }

        /// <summary>
        /// THE LIVE PANEL NAMES A ROLE THE SAME WAY THE PLAYER DOES.
        /// <para>
        /// It did not. The panel carried its own map of numbers to names and
        /// four of its five combat entries were off by two — every rifleman in
        /// the session was labelled a tank, every anti-armour infantryman an
        /// artillery piece, and the two names it had for roles 10 and 11 belong
        /// to Radar and DefensePlatform. Nothing went red over it, and nothing
        /// could have: a second name table has nothing to disagree with unless
        /// something asks the enum.
        /// </para>
        /// <para>
        /// Both tables are generated from <see cref="UnitRole"/> now, so this
        /// test asserts the property that made the bug possible is gone — one
        /// vocabulary, not two that happen to match today.
        /// </para>
        /// </summary>
        [Test]
        public void TheLivePanelAndThePlayerNameARoleTheSameWay()
        {
            List<string> player = ArrayOf(Page(), "const ROLE_NAME = ");
            List<string> live = ArrayOf(LivePage.Build(new MatchSpec()), "const ROLE_NAME = ");

            Assert.That(live, Is.EqualTo(player),
                "the live panel and the player disagree about what a role is called");

            foreach (UnitRole role in (UnitRole[])Enum.GetValues(typeof(UnitRole)))
            {
                int index = (int)role;
                Assert.That(live.Count, Is.GreaterThan(index), "role " + role + " has no name in the live panel");
                Assert.That(live[index].ToLowerInvariant(), Is.EqualTo(role.ToString().ToLowerInvariant()),
                    "role " + index + " is " + role + " and the live panel calls it '" + live[index] + "'");
            }
        }

        /// <summary>Die Zeichenketten eines JS-Arrays hinter <paramref name="marker"/>.</summary>
        private static List<string> ArrayOf(string text, string marker)
        {
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "'" + marker + "' is missing");
            int open = text.IndexOf('[', start);
            int close = text.IndexOf(']', open);
            var values = new List<string>();
            foreach (Match match in Regex.Matches(text.Substring(open, close - open), @"'([^']*)'"))
            {
                values.Add(match.Groups[1].Value);
            }
            return values;
        }
    }
}
