using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nova.AI.Data;

namespace Nova.AiLab.Tests
{
    /// <summary>
    /// E4 acceptance suite: the comparison report, the opponent archive and the
    /// PR draft.
    /// <para>
    /// Two properties carry this stage, and neither is about arithmetic. The
    /// report must REFUSE a comparison whose provenance does not match, because
    /// a wrong comparison looks exactly like a right one. And the PR draft must
    /// never claim an observation nobody made — the repository's most important
    /// rule is "nichts als fertig melden, was nicht gelaufen ist", and a tool
    /// that makes slipping past it convenient would be worse than no tool.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ComparisonTests
    {
        private static ResultSet Set(
            ulong[] seeds = null, string commit = "abc123", int tickBudget = 3000, int slotCount = 2)
        {
            ResultSet set = ResultSet.Create(seeds ?? new ulong[] { 1, 2 }, tickBudget, slotCount, commit);
            set.Candidates.Add(new CandidateResult
            {
                ProfileId = "ms1-canonical", Matches = 2, Wins = 1, Losses = 1,
                DecidedMatches = 2, DecidedTickSum = 20000, CreditsAtEndSum = 1000,
            });
            set.Candidates.Add(new CandidateResult
            {
                ProfileId = "late-push", Matches = 2, Wins = 2,
                DecidedMatches = 2, DecidedTickSum = 18000, CreditsAtEndSum = 1400,
                DifferencesFromReference = new List<string> { "armySize 12→20" },
            });
            return set;
        }

        // ================================================================
        // (a) THE REFUSAL — the product, not an error path
        // ================================================================

        [Test]
        public void IdenticalProvenanceIsComparable()
        {
            Assert.That(Set().WhyNotComparableWith(Set()), Is.Null);
        }

        [TestCase("specVersion")]
        [TestCase("profileSchemaVersion")]
        [TestCase("commit")]
        [TestCase("tickBudget")]
        [TestCase("slotCount")]
        [TestCase("definitionsHash64")]
        [TestCase("seeds")]
        public void AnArchiveMissingAProvenanceFieldIsRefused_NotDefaulted(string field)
        {
            // The failure this guards against is the quiet one: a missing field
            // used to fall back to THIS BUILD's value, so an archive that never
            // recorded its spec version compared as if it matched. A truncated
            // or hand-edited archive has to refuse, because a wrong comparison
            // looks exactly like a right one.
            string json = Set().ToJson();
            string stripped = StripProperty(json, field);
            Assert.That(stripped, Is.Not.EqualTo(json), $"the fixture must actually contain '{field}'");

            Assert.That(() => ResultSetFile.Parse(stripped, "<stripped>"),
                Throws.TypeOf<FormatException>().With.Message.Contains(field),
                $"an archive without '{field}' must refuse, never inherit the current build's value");
        }

        [Test]
        public void AnArchiveWithAnEmptyCommitIsRefused()
        {
            // A set retires with the commit it was measured at (plan 3.7), so a
            // set that cannot name one cannot be compared against anything.
            string json = Set(commit: "abc123").ToJson().Replace("\"commit\": \"abc123\"", "\"commit\": \"\"");

            Assert.That(() => ResultSetFile.Parse(json, "<empty-commit>"),
                Throws.TypeOf<FormatException>().With.Message.Contains("commit"));
        }

        /// <summary>Removes one top-level property from the hand-written JSON.</summary>
        private static string StripProperty(string json, string name)
        {
            var kept = new List<string>();
            int depth = 0;
            foreach (string line in json.Split('\n'))
            {
                string trimmed = line.Trim();
                bool startsProperty = trimmed.StartsWith($"\"{name}\":", StringComparison.Ordinal);
                if (startsProperty && depth <= 1)
                {
                    // "seeds" is a one-line array in this writer, so dropping
                    // the line drops the whole property.
                    continue;
                }
                kept.Add(line);
                depth += CountOf(line, '{') + CountOf(line, '[');
                depth -= CountOf(line, '}') + CountOf(line, ']');
            }
            return string.Join("\n", kept);
        }

        private static int CountOf(string text, char c)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if (ch == c) count++;
            }
            return count;
        }

        [Test]
        public void ADifferentDefinitionsTableRefusesTheComparison()
        {
            ResultSet mine = Set();
            ResultSet archived = Set();
            archived.DefinitionsHash64 ^= 1UL;

            string reason = mine.WhyNotComparableWith(archived);
            Assert.That(reason, Is.Not.Null);
            Assert.That(reason, Does.Contain("definitions hash"),
                "a changed unit table means no number here means what it meant there");
        }

        [Test]
        public void ADifferentSeedListRefusesTheComparison()
        {
            string reason = Set(new ulong[] { 1, 2 }).WhyNotComparableWith(Set(new ulong[] { 1, 3 }));
            Assert.That(reason, Does.Contain("seed list"));
        }

        [Test]
        public void ADifferentCommitRefusesTheComparison()
        {
            // A merge window shifts behaviour, so a frozen set retires with its
            // commit. The answer is to re-measure the archive, not to compare
            // across the gap (plan section 3.7).
            string reason = Set(commit: "aaa").WhyNotComparableWith(Set(commit: "bbb"));
            Assert.That(reason, Does.Contain("commit"));
            Assert.That(reason, Does.Contain("re-measure"));
        }

        [Test]
        public void ADifferentTickBudgetOrSlotCountRefusesTheComparison()
        {
            Assert.That(Set(tickBudget: 3000).WhyNotComparableWith(Set(tickBudget: 9000)),
                Does.Contain("tick budget"));
            Assert.That(Set(slotCount: 2).WhyNotComparableWith(Set(slotCount: 4)),
                Does.Contain("slot count"));
        }

        [Test]
        public void ARefusedComparisonRendersTheReasonInsteadOfATable()
        {
            string html = ComparisonReport.BuildRefusal("definitions hash differs", Set());

            Assert.That(html, Does.Contain("refused"));
            Assert.That(html, Does.Contain("definitions hash differs"));
            Assert.That(html, Does.Not.Contain("<tbody>"),
                "a refused comparison must not render a table that looks like a valid one");
        }

        // ================================================================
        // (b) THE ARCHIVE ROUND TRIP
        // ================================================================

        [Test]
        public void AnArchivedSetReadsBackWithItsProvenanceIntact()
        {
            ResultSet original = Set();
            ResultSet reloaded = ResultSetFile.Parse(original.ToJson());

            Assert.That(reloaded.SpecVersion, Is.EqualTo(original.SpecVersion));
            Assert.That(reloaded.ProfileSchemaVersion, Is.EqualTo(original.ProfileSchemaVersion));
            Assert.That(reloaded.DefinitionsHash64, Is.EqualTo(original.DefinitionsHash64));
            Assert.That(reloaded.Commit, Is.EqualTo(original.Commit));
            Assert.That(reloaded.Seeds, Is.EqualTo(original.Seeds));
            Assert.That(reloaded.Candidates.Count, Is.EqualTo(original.Candidates.Count));

            Assert.That(original.WhyNotComparableWith(reloaded), Is.Null,
                "a set must still be comparable with itself after a round trip through the archive");
        }

        // ================================================================
        // (c) THE REPORT: no ranking
        // ================================================================

        [Test]
        public void TheReportShowsEveryCandidateAndRanksNone()
        {
            string html = ComparisonReport.Build(Set(), "ms1-canonical");

            Assert.That(html, Does.Contain("ms1-canonical"));
            Assert.That(html, Does.Contain("late-push"));
            Assert.That(html, Does.Contain("armySize 12→20"), "the row must name what the candidate changed");
            Assert.That(html, Does.Contain("reference"), "the yardstick row is marked as such");

            // No scalar score COLUMN: a single number reliably rewards the
            // wrong thing (decision 11). The prose is allowed — and required —
            // to say so; what must not exist is a column to sort by.
            string header = html.Substring(html.IndexOf("<thead>", StringComparison.Ordinal),
                html.IndexOf("</thead>", StringComparison.Ordinal)
                - html.IndexOf("<thead>", StringComparison.Ordinal)).ToLowerInvariant();
            Assert.That(header, Does.Not.Contain("score"), "there must be no score column");
            Assert.That(header, Does.Not.Contain("rank"), "there must be no rank column");
            Assert.That(header, Does.Not.Contain("best"));

            // Rows keep the candidate order they were measured in; a report
            // sorted by merit would be a ranking wearing a table's clothes.
            int referenceRow = html.IndexOf("ms1-canonical", StringComparison.Ordinal);
            int candidateRow = html.IndexOf("late-push", StringComparison.Ordinal);
            Assert.That(referenceRow, Is.LessThan(candidateRow),
                "row order follows the candidate list, not a score");

            Assert.That(html, Does.Contain("DEVIATION"), "colour marks deviation, not goodness");
        }

        [Test]
        public void TheReportIsSelfContained()
        {
            string html = ComparisonReport.Build(Set(), "ms1-canonical");

            Assert.That(html, Does.Not.Contain("http://"));
            Assert.That(html, Does.Not.Contain("https://"));
            Assert.That(html, Does.Not.Contain("<script src"));
        }

        [Test]
        public void TheReportSaysTheSeedAxisIsEmptyWhenSeveralSeedsRan()
        {
            string html = ComparisonReport.Build(Set(new ulong[] { 1, 2, 3 }), "ms1-canonical");
            Assert.That(html, Does.Contain("seed axis is empty"),
                "several seed rows must not read as several observations while nothing draws from the PRNG");
        }

        // ================================================================
        // (d) THE PR DRAFT — the absolute limit
        // ================================================================

        [Test]
        public void ThePrDraftLeavesTheObservationSectionEmptyAndSaysSo()
        {
            string draft = PrDraft.Build(Set(), "ms1-canonical", "late-push");

            Assert.That(draft, Does.Contain("## Im laufenden Spiel gesehen"));
            Assert.That(draft, Does.Contain("LEER GELASSEN"),
                "the section must be recognisably empty, not quietly absent");
            Assert.That(draft, Does.Contain("Nicht im laufenden Spiel geprüft"),
                "the draft must spell out what to write when nothing was played");
        }

        [Test]
        public void ThePrDraftNeverClaimsAnObservation()
        {
            // The HTML comments deliberately contain phrases like "im laufenden
            // Spiel geprueft" as INSTRUCTIONS to the author. What must never
            // appear is such a phrase in the VISIBLE text, where it would read
            // as a claim.
            string visible = System.Text.RegularExpressions.Regex.Replace(
                PrDraft.Build(Set(), "ms1-canonical", "late-push"),
                "<!--.*?-->", string.Empty,
                System.Text.RegularExpressions.RegexOptions.Singleline).ToLowerInvariant();

            Assert.That(visible, Does.Contain("gemessen"), "the visible text must still hold the measurements");

            foreach (string claim in PrDraft.ForbiddenClaims)
            {
                Assert.That(visible, Does.Not.Contain(claim.ToLowerInvariant()),
                    $"the draft asserts '{claim}' in its visible text — a lab run is diagnosis, never proof");
            }
        }

        [Test]
        public void ThePrDraftCarriesTheReproductionConditionsAndTheBaselineWarning()
        {
            string draft = PrDraft.Build(Set(), "ms1-canonical", "late-push");

            Assert.That(draft, Does.Contain("ComputeDefinitionsHash64"));
            Assert.That(draft, Does.Contain("Commit"));
            Assert.That(draft, Does.Contain("Seeds:"));
            foreach (string file in PrDraft.BaselineFiles)
            {
                Assert.That(draft, Does.Contain(file), "every baseline file must be named");
            }
            Assert.That(draft, Does.Contain("eigenen PR"),
                "a behaviour PR that also resets a baseline does not get merged");
        }

        [Test]
        public void ThePrDraftWarnsWhenTheSeedListPromisesMoreThanItDelivers()
        {
            string draft = PrDraft.Build(Set(new ulong[] { 1, 2, 3 }), "ms1-canonical", "late-push");
            Assert.That(draft, Does.Contain("Seeds spielen dieselbe Partie"));
        }

        // ================================================================
        // (e) CANDIDATES
        // ================================================================

        [Test]
        public void EveryCandidateDiffersFromTheReferenceInANamedWay()
        {
            foreach (AiProfile candidate in LabProfiles.Candidates)
            {
                List<string> differences = LabProfiles.DifferencesFromReference(candidate);
                if (candidate.ProfileId == LabProfiles.Reference.ProfileId)
                {
                    Assert.That(differences, Is.Empty, "the reference differs from itself in nothing");
                    continue;
                }

                Assert.That(differences, Is.Not.Empty,
                    $"candidate {candidate.ProfileId} is identical to the reference and would measure nothing");
            }
        }

        [Test]
        public void CandidateIdsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (AiProfile candidate in LabProfiles.Candidates)
            {
                Assert.That(seen.Add(candidate.ProfileId), Is.True, $"duplicate candidate id {candidate.ProfileId}");
            }
        }

        [Test]
        public void TheCadenceCandidateActuallyTunesTheCadence()
        {
            // fast-cadence exists to prove E6 opened something that was closed:
            // DecisionTickInterval was a const nobody could reach.
            Assert.That(LabProfiles.TryGet("fast-cadence", out AiProfile fast), Is.True);
            Assert.That(fast.DecisionTickInterval, Is.Not.EqualTo(LabProfiles.Reference.DecisionTickInterval));
        }

        // ================================================================
        // (f) THE TOURNAMENT
        // ================================================================

        [Test]
        public void ATournamentPlaysEveryCandidateInBothFactionSeatings()
        {
            ulong[] seeds = SeedSeries.Derive(0xA17E57DE57UL, 1);
            var candidates = new List<AiProfile> { LabProfiles.Reference };
            Assert.That(LabProfiles.TryGet("late-push", out AiProfile latePush), Is.True);
            candidates.Add(latePush);

            ResultSet set = TournamentRunner.Run(
                candidates, seeds, tickBudget: 4000, outputDirectory: null, commit: "test", maxParallelism: 2);

            Assert.That(set.Candidates.Count, Is.EqualTo(2));
            foreach (CandidateResult c in set.Candidates)
            {
                Assert.That(c.Matches, Is.EqualTo(2),
                    "one match per faction seating — Alliance and Legion are deliberately asymmetric, " +
                    "and playing both also cancels the spawn-order advantage");
                Assert.That(c.Wins + c.Losses + c.Draws, Is.EqualTo(c.Matches));
            }

            Assert.That(set.Commit, Is.EqualTo("test"));
            Assert.That(set.Seeds, Is.EqualTo(seeds));
        }

        [Test]
        public void ATournamentIsReproducible()
        {
            ulong[] seeds = SeedSeries.Derive(0xA17E57DE57UL, 1);
            var candidates = new List<AiProfile> { LabProfiles.Reference };

            ResultSet first = TournamentRunner.Run(candidates, seeds, 3000, null, "test", 1);
            ResultSet second = TournamentRunner.Run(candidates, seeds, 3000, null, "test", 1);

            Assert.That(second.Candidates[0].Wins, Is.EqualTo(first.Candidates[0].Wins));
            Assert.That(second.Candidates[0].DecidedTickSum, Is.EqualTo(first.Candidates[0].DecidedTickSum));
            Assert.That(second.Candidates[0].CreditsAtEndSum, Is.EqualTo(first.Candidates[0].CreditsAtEndSum));
        }
    }
}
