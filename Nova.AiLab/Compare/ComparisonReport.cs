using System;
using System.Globalization;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// The comparison report (plan sections 3.6 and 3.10): one self-contained
    /// HTML page, same make as the view player.
    /// <para>
    /// <b>THE LAB DOES NOT RANK. IT LAYS THINGS SIDE BY SIDE.</b> There is
    /// deliberately no scalar score to sort by (decision 11), because a single
    /// number reliably rewards the wrong thing: an AI that wins 5% more often
    /// by burying the enemy in builders is not a better AI, and for "looks
    /// right in the running game" there is no metric at all. The columns sit
    /// next to each other, deviation from the reference is coloured, and a
    /// HUMAN picks — with the view window open beside it.
    /// </para>
    /// <para>
    /// Because there is no ranking, READABILITY IS THE PRODUCT, not a
    /// nicety: the choice has to fall in minutes, not in an hour of reading
    /// tables. Hence one row per candidate, the changed values named in the
    /// row, and a link straight into that candidate's recorded run.
    /// </para>
    /// <para>
    /// Colour marks DEVIATION, never GOODNESS. More credits is not better than
    /// fewer; a faster decision is not better than a slower one. The colour
    /// says "this differs, look here".
    /// </para>
    /// </summary>
    public static class ComparisonReport
    {
        public const string FileName = "report.html";

        public static string Build(ResultSet set, string referenceProfileId)
        {
            CandidateResult reference = null;
            foreach (CandidateResult c in set.Candidates)
            {
                if (string.Equals(c.ProfileId, referenceProfileId, StringComparison.Ordinal)) reference = c;
            }

            var html = new StringBuilder(16 * 1024);
            html.Append(HeadAndStyle);

            html.Append("<header><h1>Nova AI Lab — candidate comparison</h1><div class=\"sub\">")
                .Append(set.Candidates.Count).Append(" candidates · ")
                .Append(set.Seeds.Length).Append(" seeds · budget ").Append(set.TickBudget)
                .Append(" ticks · commit ").Append(Escape(Short(set.Commit)))
                .Append(" · definitions 0x")
                .Append(set.DefinitionsHash64.ToString("X16", CultureInfo.InvariantCulture))
                .Append("</div>")
                .Append("<div class=\"warn\">DIAGNOSIS, never proof — nothing here was seen in the running game. ")
                .Append("There is no ranking on purpose: read the columns, then look at the run.</div>")
                .Append("</header>\n");

            if (set.Seeds.Length > 1)
            {
                html.Append("<div class=\"note\">The seed axis is empty today: no simulation system draws from ")
                    .Append("the kernel PRNG, so all ").Append(set.Seeds.Length)
                    .Append(" seeds play the identical match. The variance in this table comes from the ")
                    .Append("profiles and from the two faction seatings, not from the seeds.</div>\n");
            }

            html.Append("<main><table>\n<thead><tr>")
                .Append("<th>candidate</th><th>changed against reference</th>")
                .Append("<th>win %</th><th>W/L/D</th><th>decided tick</th>")
                .Append("<th>credits</th><th>army</th><th>lost</th>")
                .Append("<th>intents</th><th>rejected</th><th>run</th>")
                .Append("</tr></thead>\n<tbody>\n");

            foreach (CandidateResult c in set.Candidates)
            {
                bool isReference = ReferenceEquals(c, reference);
                html.Append(isReference ? "<tr class=\"ref\">" : "<tr>");
                html.Append("<td class=\"name\">").Append(Escape(c.ProfileId))
                    .Append(isReference ? " <span class=\"tag\">reference</span>" : "").Append("</td>");
                html.Append("<td class=\"changes\">")
                    .Append(c.DifferencesFromReference.Count == 0
                        ? "<span class=\"dim\">—</span>"
                        : Escape(string.Join(" · ", c.DifferencesFromReference)))
                    .Append("</td>");

                Cell(html, c.WinPercent, reference?.WinPercent, isReference, suffix: "%");
                html.Append("<td>").Append(c.Wins).Append('/').Append(c.Losses).Append('/').Append(c.Draws).Append("</td>");
                Cell(html, c.AverageDecidedTick, reference?.AverageDecidedTick, isReference);
                Cell(html, c.AverageCredits, reference?.AverageCredits, isReference);
                Cell(html, c.AverageArmySize, reference?.AverageArmySize, isReference);
                Cell(html, c.AverageUnitsLost, reference?.AverageUnitsLost, isReference);
                Cell(html, c.IntentsSubmittedSum, reference?.IntentsSubmittedSum, isReference);
                Cell(html, c.IntentsRejectedSum, reference?.IntentsRejectedSum, isReference);

                html.Append("<td>");
                if (!string.IsNullOrEmpty(c.SampleRunDirectory))
                {
                    html.Append("<a href=\"").Append(Escape(c.SampleRunDirectory))
                        .Append("/player.html\">view</a>");
                }
                else
                {
                    html.Append("<span class=\"dim\">—</span>");
                }
                html.Append("</td></tr>\n");
            }

            html.Append("</tbody></table>\n");

            // ---- second table: how the match FELT, not who won ----
            //
            // Deliberately a SEPARATE table rather than six more columns on
            // the first one. These read against a different question, and a
            // twenty-column table is a table nobody reads — readability is the
            // product here, because there is no ranking to fall back on.
            html.Append("<h2>game feel</h2>\n")
                .Append("<div class=\"note\">Strength and speed are above. These columns ask how the match ")
                .Append("PLAYED: did the army gather and strike, or trickle; did anything happen when it ")
                .Append("was shot at; did it act at a human rate; did the set produce more than one match. ")
                .Append("<b>The exchange ratio only means something one-sided</b> — every candidate here ")
                .Append("plays the reference, which is exactly that setting.</div>\n");

            html.Append("<table>\n<thead><tr>")
                .Append("<th>candidate</th>")
                .Append("<th>exchange /100</th><th>combat intervals</th><th>largest jump</th>")
                .Append("<th>reaction ticks</th><th>unanswered</th><th>APM</th><th>endings</th>")
                .Append("</tr></thead>\n<tbody>\n");

            foreach (CandidateResult c in set.Candidates)
            {
                bool isReference = ReferenceEquals(c, reference);
                html.Append(isReference ? "<tr class=\"ref\">" : "<tr>");
                html.Append("<td class=\"name\">").Append(Escape(c.ProfileId))
                    .Append(isReference ? " <span class=\"tag\">reference</span>" : "").Append("</td>");

                Cell(html, c.AverageExchangeRatio, reference?.AverageExchangeRatio, isReference);
                Cell(html, c.AverageCombatIntervals, reference?.AverageCombatIntervals, isReference);
                Cell(html, c.AverageLargestLossJump, reference?.AverageLargestLossJump, isReference);
                Cell(html, c.AverageReactionLatency, reference?.AverageReactionLatency, isReference);
                Cell(html, c.AverageUnansweredDamage, reference?.AverageUnansweredDamage, isReference);
                Cell(html, c.AverageActionsPerMinute, reference?.AverageActionsPerMinute, isReference);
                Cell(html, c.ReplayValue, reference?.ReplayValue, isReference);

                html.Append("</tr>\n");
            }

            html.Append("</tbody></table>\n");

            html.Append("<section class=\"legend\">")
                .Append("<p><b>Reading the feel table.</b> <i>exchange /100</i> is enemy entities lost per ")
                .Append("100 own, <i>-1</i> when the candidate lost nothing. <i>combat intervals</i> and ")
                .Append("<i>largest jump</i> describe the shape of the loss curve: many intervals losing one ")
                .Append("unit each is a trickle, few intervals losing many is a battle — the same total, a ")
                .Append("very different match. <i>reaction ticks</i> is the mean delay between a unit ")
                .Append("losing health and that unit being sent somewhere else, <i>-1</i> when it never ")
                .Append("happened; <i>unanswered</i> counts the damage that got no answer at all. ")
                .Append("<i>APM</i> is intents read as a human rate. <i>endings</i> is how many different ")
                .Append("matches the whole set produced — 1 means the seeds change nothing.</p>")
                .Append("<p><b>A staging rule is supposed to raise the deciding tick.</b> Waiting is the ")
                .Append("point of it. Judged on the first table alone it looks like a regression, which is ")
                .Append("how two earlier changes were read (journal V002, V003, methodology finding M001).</p>")
                .Append("</section>\n");

            html.Append("<section class=\"legend\">")
                .Append("<p><b>How to read this.</b> Colour marks DEVIATION from the reference row, not ")
                .Append("goodness — more credits is not better than fewer, a faster decision is not better ")
                .Append("than a slower one. Nothing here is summed into a score, and nothing is sorted by ")
                .Append("merit; a single number would reliably reward the wrong behaviour.</p>")
                .Append("<p><b>Every candidate played both factions</b> against the frozen reference. ")
                .Append("Alliance and Legion are deliberately asymmetric, and playing both also cancels ")
                .Append("the spawn-order advantage.</p>")
                .Append("<p><b>rejected</b> is the underestimated column: it shows where the AI ran into ")
                .Append("executor rules, which is silent everywhere else.</p>")
                .Append("<p><b>Then open the run.</b> A win rate does not explain that half the army was ")
                .Append("stuck on a building corner — the view window does.</p>")
                .Append("</section>\n");

            html.Append("</main>\n</body>\n</html>\n");
            return html.ToString();
        }

        /// <summary>A metric cell, coloured by how far it sits from the reference.</summary>
        private static void Cell(StringBuilder html, long value, long? referenceValue, bool isReference, string suffix = "")
        {
            string cls = "";
            string delta = "";

            if (!isReference && referenceValue.HasValue)
            {
                long r = referenceValue.Value;
                long difference = value - r;
                if (difference != 0)
                {
                    // Integer percent of the reference; 0 reference falls back
                    // to "differs" without inventing a ratio.
                    long percent = r != 0 ? difference * 100 / Math.Abs(r) : 0;
                    cls = Math.Abs(percent) >= 20 ? "big" : "small";
                    delta = r != 0
                        ? $"<span class=\"d\">{(difference > 0 ? "+" : "")}{percent}%</span>"
                        : "<span class=\"d\">≠</span>";
                }
            }

            html.Append("<td class=\"num ").Append(cls).Append("\">")
                .Append(value).Append(suffix).Append(delta).Append("</td>");
        }

        private static string Short(string commit) =>
            commit != null && commit.Length > 8 ? commit.Substring(0, 8) : commit ?? "unknown";

        private static string Escape(string text) => text == null
            ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        /// <summary>Refused comparison: the reason, not a table (plan section 3.7).</summary>
        public static string BuildRefusal(string reason, ResultSet attempted)
        {
            var html = new StringBuilder(2048);
            html.Append(HeadAndStyle);
            html.Append("<header><h1>Nova AI Lab — comparison refused</h1>")
                .Append("<div class=\"warn\">These result sets are not comparable, so no table is shown. ")
                .Append("Mixing them would look exactly like a valid comparison.</div></header>\n");
            html.Append("<main><section class=\"legend\"><p><b>Reason:</b> ").Append(Escape(reason)).Append("</p>");
            if (attempted != null)
            {
                html.Append("<p>Attempted set: spec v").Append(attempted.SpecVersion)
                    .Append(", commit ").Append(Escape(Short(attempted.Commit)))
                    .Append(", definitions 0x")
                    .Append(attempted.DefinitionsHash64.ToString("X16", CultureInfo.InvariantCulture))
                    .Append(", ").Append(attempted.Seeds.Length).Append(" seeds.</p>");
            }
            html.Append("<p>Re-measure the archived set on the current build instead of comparing across ")
                .Append("the gap. A profile archive survives a merge window; a code archive does not.</p>")
                .Append("</section></main>\n</body>\n</html>\n");
            return html.ToString();
        }

        private const string HeadAndStyle = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Nova AI Lab — candidate comparison</title>
<style>
  :root { color-scheme: dark; }
  body { margin:0; background:#0d1117; color:#c9d1d9;
         font:13px/1.6 ui-monospace,SFMono-Regular,Menlo,monospace; }
  header { padding:12px 16px; border-bottom:1px solid #21262d; }
  h1 { font-size:15px; margin:0 0 3px; font-weight:600; }
  h2 { font-size:13px; margin:26px 0 8px; font-weight:600; color:#e6edf3; }
  .sub { color:#8b949e; font-size:12px; }
  .warn { color:#d29922; font-size:12px; margin-top:6px; }
  .note { margin:12px 16px 0; padding:8px 12px; border-left:3px solid #d29922;
          background:#1c1a12; color:#c9d1d9; font-size:12px; }
  main { padding:14px 16px 32px; }
  table { border-collapse:collapse; width:100%; font-size:12px; }
  th,td { padding:5px 9px; border-bottom:1px solid #21262d; text-align:right; white-space:nowrap; }
  th { color:#8b949e; font-weight:600; text-align:right; position:sticky; top:0; background:#0d1117; }
  th:first-child, td:first-child, th:nth-child(2), td:nth-child(2) { text-align:left; }
  td.changes { white-space:normal; color:#8b949e; font-size:11px; max-width:280px; }
  tr.ref { background:#161b22; }
  tr.ref td { border-bottom:1px solid #30363d; }
  .tag { color:#58a6ff; font-size:10px; border:1px solid #1f6feb; border-radius:3px; padding:0 4px; }
  .name { color:#e6edf3; }
  .dim { color:#484f58; }
  td.num .d { color:#8b949e; font-size:10px; margin-left:5px; }
  td.small { color:#d29922; }
  td.big { color:#f85149; }
  .legend { margin-top:18px; color:#8b949e; font-size:12px; max-width:70ch; }
  .legend b { color:#c9d1d9; }
  a { color:#58a6ff; }
</style>
</head>
<body>
";
    }
}
