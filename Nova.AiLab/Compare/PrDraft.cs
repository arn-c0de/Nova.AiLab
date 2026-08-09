using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// Drafts the PR text for a before/after comparison (plan section 3.10).
    /// <para>
    /// ONE LIMIT IS ABSOLUTE: <b>the draft contains only what was measured.</b>
    /// The section for the played observation stays EMPTY and is recognisably
    /// empty; no generated sentence phrases a lab result as if it had been seen
    /// in the running game. "Nichts als fertig melden, was nicht gelaufen ist"
    /// is the repository's most important rule, and a tool that makes it
    /// convenient to slip past would be worse than no tool at all.
    /// </para>
    /// <para>
    /// That is also why this class writes no summary sentence, no "improves",
    /// no "regression". It lists numbers and names files. The judgement is the
    /// author's.
    /// </para>
    /// </summary>
    public static class PrDraft
    {
        /// <summary>
        /// One draft per candidate, so the file name carries the candidate.
        /// A single <c>pr-draft.md</c> would be overwritten by whichever
        /// candidate was written last.
        /// </summary>
        public static string FileNameFor(string candidateProfileId) => $"pr-draft-{candidateProfileId}.md";

        /// <summary>The four files any behaviour change turns red — by design, not by defect.</summary>
        public static readonly string[] BaselineFiles =
        {
            "tools/Nova.SimRunner.Tests/SnapshotGoldenBytesTests.cs",
            "tools/Nova.SimRunner.Tests/CommandGoldenBytesTests.cs",
            "tools/Nova.SimRunner.Tests/SimRandomGoldenTests.cs",
            "tools/Nova.SimRunner.Tests/Determinism10000Tests.cs",
        };

        public static string Build(ResultSet set, string referenceProfileId, string candidateProfileId)
        {
            CandidateResult reference = Find(set, referenceProfileId);
            CandidateResult candidate = Find(set, candidateProfileId);

            var md = new StringBuilder(4096);
            md.Append("<!-- Entwurf aus dem KI-Labor. Enthält AUSSCHLIESSLICH Gemessenes.\n")
              .Append("     Der Abschnitt \"Im laufenden Spiel gesehen\" ist absichtlich leer und\n")
              .Append("     muss von Hand gefüllt werden — oder ausdrücklich leer bleiben. -->\n\n");

            md.Append("## Was & Warum\n\n");
            md.Append("<!-- 1-3 Sätze, von Hand. Das Labor schreibt hier nichts hinein: warum eine\n")
              .Append("     Änderung richtig ist, ist eine Begründung und keine Messung. -->\n\n");

            if (candidate == null)
            {
                md.Append("> Kein Kandidat `").Append(candidateProfileId).Append("` in der Ergebnismenge.\n");
                return md.ToString();
            }

            md.Append("## Gemessen\n\n");
            md.Append("Kandidat `").Append(candidate.ProfileId).Append('`');
            if (candidate.DifferencesFromReference.Count > 0)
            {
                md.Append(" — geändert gegenüber `").Append(referenceProfileId).Append("`: ")
                  .Append(string.Join(", ", candidate.DifferencesFromReference));
            }
            md.Append(".\n\n");

            md.Append("| Kennzahl | ").Append(referenceProfileId).Append(" | ")
              .Append(candidate.ProfileId).Append(" |\n|---|---:|---:|\n");
            Row(md, "Siegrate", reference?.WinPercent, candidate.WinPercent, "%");
            md.Append("| Partien (S/N/U) | ")
              .Append(reference == null ? "—" : $"{reference.Wins}/{reference.Losses}/{reference.Draws}")
              .Append(" | ").Append($"{candidate.Wins}/{candidate.Losses}/{candidate.Draws}").Append(" |\n");
            Row(md, "Entscheidungstick (Mittel)", reference?.AverageDecidedTick, candidate.AverageDecidedTick, "");
            Row(md, "Credits am Ende (Mittel)", reference?.AverageCredits, candidate.AverageCredits, "");
            Row(md, "Armeegröße am Ende (Mittel)", reference?.AverageArmySize, candidate.AverageArmySize, "");
            Row(md, "Verlorene Einheiten (Mittel)", reference?.AverageUnitsLost, candidate.AverageUnitsLost, "");
            Row(md, "Intents gesendet", reference?.IntentsSubmittedSum, candidate.IntentsSubmittedSum, "");
            Row(md, "Intents abgelehnt", reference?.IntentsRejectedSum, candidate.IntentsRejectedSum, "");
            md.Append('\n');

            // Die vier Spielgefühl-Spalten stehen in einer EIGENEN Tabelle,
            // weil sie eine andere Frage beantworten als die oben: nicht wer
            // gewinnt, sondern wie sich die Partie anfühlt. Eine Regel, die
            // absichtlich wartet, sieht in der ersten Tabelle wie ein
            // Rückschritt aus — genau so wurden V002 und V003 gelesen.
            md.Append("### Spielgefühl\n\n");
            md.Append("| Kennzahl | ").Append(referenceProfileId).Append(" | ")
              .Append(candidate.ProfileId).Append(" |\n|---|---:|---:|\n");
            Row(md, "Austauschverhältnis (Feindverluste je 100 eigene)",
                reference?.AverageExchangeRatio, candidate.AverageExchangeRatio, "");
            Row(md, "Gefechtsintervalle (mit Verlusten)",
                reference?.AverageCombatIntervals, candidate.AverageCombatIntervals, "");
            Row(md, "Grösster Verlustsprung in einem Intervall",
                reference?.AverageLargestLossJump, candidate.AverageLargestLossJump, "");
            Row(md, "Reaktionslatenz (Ticks Schaden → neuer Marschbefehl)",
                reference?.AverageReactionLatency, candidate.AverageReactionLatency, "");
            Row(md, "Unbeantworteter Schaden (Ereignisse)",
                reference?.AverageUnansweredDamage, candidate.AverageUnansweredDamage, "");
            Row(md, "Aktionen pro Minute", reference?.AverageActionsPerMinute, candidate.AverageActionsPerMinute, "");
            Row(md, "Verschiedene Partieausgänge über die Menge",
                reference?.ReplayValue, candidate.ReplayValue, "");
            md.Append('\n');
            md.Append("`-1` heisst \"in dieser Menge nicht messbar\" (keine eigenen Verluste bzw. keine ")
              .Append("einzige Reaktion), nicht `0`.\n")
              .Append("Das Austauschverhältnis ist **nur einseitig** aussagekräftig — jeder Kandidat hier ")
              .Append("spielt gegen die Referenz, genau diese Anordnung.\n\n");

            md.Append("Bedingungen des Laufs — ohne sie ist keine Zahl oben reproduzierbar:\n\n");
            md.Append("- Spec-Version ").Append(set.SpecVersion)
              .Append(", Profil-Schema ").Append(set.ProfileSchemaVersion).Append('\n');
            md.Append("- Tickbudget ").Append(set.TickBudget)
              .Append(", ").Append(set.SlotCount).Append(" Slots, jeder Kandidat in **beiden** Fraktionsrollen\n");
            md.Append("- `ComputeDefinitionsHash64()` = 0x")
              .Append(set.DefinitionsHash64.ToString("X16", CultureInfo.InvariantCulture)).Append('\n');
            md.Append("- Commit ").Append(set.Commit).Append('\n');
            md.Append("- Seeds: ");
            for (int i = 0; i < set.Seeds.Length; i++)
            {
                if (i > 0) md.Append(", ");
                md.Append("`0x").Append(set.Seeds[i].ToString("X", CultureInfo.InvariantCulture)).Append('`');
            }
            md.Append('\n');
            if (set.Seeds.Length > 1)
            {
                md.Append("- **Hinweis zur Seedmenge:** Kein Simulationssystem zieht heute aus dem Kernel-PRNG. ")
                  .Append("Alle ").Append(set.Seeds.Length)
                  .Append(" Seeds spielen dieselbe Partie; die Streuung oben kommt aus den Profilen und den ")
                  .Append("beiden Fraktionsrollen, nicht aus den Seeds.\n");
            }
            md.Append('\n');

            md.Append("## Im laufenden Spiel gesehen\n\n");
            md.Append("<!-- LEER GELASSEN — und zwar absichtlich.\n\n")
              .Append("     Das Labor kann diesen Abschnitt nicht füllen. Ein Laborlauf ist Diagnose,\n")
              .Append("     kein Nachweis: hier gehört hinein, was in einer echten Partie zu sehen war,\n")
              .Append("     einschließlich eines Falls, in dem das Verhalten falsch war, mit Einschätzung\n")
              .Append("     warum das akzeptabel ist.\n\n")
              .Append("     Wenn nicht gespielt wurde, bleibt genau das hier stehen:\n")
              .Append("     \"Nicht im laufenden Spiel geprüft.\" Der PR ist dann unfertig und sagt es. -->\n\n");

            md.Append("## Baselines\n\n");
            md.Append("Eine Verhaltensänderung macht diese vier Dateien rot. **Das ist ihr Zweck, kein Defekt** —\n")
              .Append("dieser PR ändert sie nicht:\n\n");
            foreach (string file in BaselineFiles) md.Append("- `").Append(file).Append("`\n");
            md.Append("\nDie neue Baseline kommt in einen **eigenen PR** mit altem Wert, neuem Wert und\n")
              .Append("Begründung. Ein PR, der Verhalten ändert und im selben Zug eine Baseline neu setzt,\n")
              .Append("wird nicht gemergt.\n\n");

            md.Append("## Checkliste\n\n");
            md.Append("- [ ] `dotnet test tools/Nova.SimRunner.Tests` lokal gelaufen — Ergebnis eintragen\n");
            md.Append("- [ ] Zeile unter `[Unreleased]` in CHANGELOG.md\n");
            md.Append("- [ ] Beobachtungsabschnitt oben gefüllt **oder** ausdrücklich als ungespielt markiert\n");

            return md.ToString();
        }

        private static void Row(StringBuilder md, string label, long? reference, long value, string suffix)
        {
            md.Append("| ").Append(label).Append(" | ")
              .Append(reference.HasValue ? reference.Value + suffix : "—")
              .Append(" | ").Append(value).Append(suffix).Append(" |\n");
        }

        private static CandidateResult Find(ResultSet set, string profileId)
        {
            foreach (CandidateResult c in set.Candidates)
            {
                if (string.Equals(c.ProfileId, profileId, StringComparison.Ordinal)) return c;
            }
            return null;
        }

        /// <summary>Sanity guard used by the tests: the draft must not claim an observation.</summary>
        public static IReadOnlyList<string> ForbiddenClaims { get; } = new[]
        {
            "im Spiel gesehen", "im laufenden Spiel geprüft", "getestet im Spiel",
            "verified in game", "played and confirmed",
        };
    }
}
