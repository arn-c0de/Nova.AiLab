namespace Nova.AiLab
{
    /// <summary>
    /// The help text. Its own file because it is the one place where a mode is
    /// described in words rather than code — a new flag is wrong here long
    /// before it is wrong anywhere else.
    /// </summary>
    internal static class Usage
    {
        public const string Text =
            "Nova.AiLab — local AI simulation lab (diagnosis only, never proof)\n" +
            "\n" +
            "  match [options]        run one AI-vs-AI match\n" +
            "  sweep [options]        run a seed matrix across all cores\n" +
            "  duel [options]         measure the counter-table: every role pairing, three distances,\n" +
            "                         both directions, plus the siege echelon\n" +
            "  movement [options]     the four movement scenarios: arrival, blocking, standoff, detour\n" +
            "  compare [options]      run every candidate profile against the frozen reference and\n" +
            "                         write report.html, resultset.json and a PR draft\n" +
            "\n" +
            "Spec:\n" +
            "  --spec <file>          JSON MatchSpec (plan section 3.2); flags below override it\n" +
            "  --seed <ulong>         match seed, decimal or 0x-hex (default 0xA17E57DE57)\n" +
            "  --slots <n>            slot count, 2..4 seats on the canonical map (default 2)\n" +
            "  --ticks <n>            tick budget (default 27000 = VictorySystem.TimeLimitTick)\n" +
            "  --trace-every <n>      metric sample every n ticks (default 0 = off)\n" +
            "  --hash-every <n>       state hash every n ticks (default 0 = end state only)\n" +
            "  --view-every <n>       view frame every n ticks (default 0 = off)\n" +
            "  --fog                  record the fog layer with each view frame\n" +
            "  --profile <id>         named lab profile for every AI slot (see compare for the ids)\n" +
            "  --profile0 <id>        named lab profile for slot 0 only — with --profile1 this is a\n" +
            "  --profile1 <id>        ONE-SIDED match: with the new behaviour against without it\n" +
            "\n" +
            "match:\n" +
            "  --repeat <n>           run the same spec n times and compare the hash chains\n" +
            "  --watch                draw the running match in the terminal (implies --view-every 20)\n" +
            "  --out <dir>            write result.json, trace.ndjson, hashchain.json, view.ndjson, player.html\n" +
            "\n" +
            "sweep:\n" +
            "  --seeds <n>            number of seeds, derived from --seed (default 8)\n" +
            "  --out <dir>            one subdirectory per seed\n" +
            "  --parallel <n>         max concurrent matches (default: processor count)\n" +
            "\n" +
            "duel:\n" +
            "  --units <n>            units the expensive side fields; the AE budget follows (default 6)\n" +
            "  --ticks <n>            tick budget per duel (default 3000)\n" +
            "  --out <dir>            write duels.ndjson\n" +
            "  --parallel <n>         max concurrent duels (default: processor count)\n" +
            "\n" +
            "compare:\n" +
            "  --seeds <n>            seeds per candidate (default 1 — the seed axis is empty today)\n" +
            "  --ticks <n>            tick budget per match (default 27000)\n" +
            "  --out <dir>            write report.html, resultset.json, pr-draft-<candidate>.md and one run each\n" +
            "  --against <file>       compare against an archived resultset.json instead of the built-in reference\n" +
            "  --parallel <n>         max concurrent candidates (default: processor count)\n" +
            "\n" +
            "movement:\n" +
            "  --group <n>            units per group (default 8)\n" +
            "  --ticks <n>            tick budget per scenario (default 3000)\n" +
            "  --out <dir>            write movement.ndjson\n";
    }
}
