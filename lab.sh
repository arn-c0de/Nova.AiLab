#!/usr/bin/env bash
#
# lab.sh — ein Laborlauf und seine Berichte, in einem Kommando.
#
# Faehrt alle vier Laufarten nach `Nova.AiLab/out/` und schreibt danach
# `out/dashboard.html` sowie den Markdown-Satz unter `Nova.AiLab/reports/`
# (latest.md, runs/<id>.md, README.md). Der Berichtsteil laeuft auch allein:
#
#   ./lab.sh                     messen und berichten
#   ./lab.sh --reports-only      nur berichten, nichts messen
#   ./lab.sh --regenerate        nur neu rendern, nichts einlesen
#   ./lab.sh --repo <pfad>       einen anderen Checkout messen
#
# DAS LABOR LIEGT AUSSERHALB DES SPIELS. Es misst, was drueben ausgecheckt
# ist — jeder Branch, ohne dort eingebaut zu werden. Ein Messwerkzeug, das man
# auf jeden Branch mitschleppen muss, kann nur den eigenen anschauen. Mit
# --repo (oder NovaRepo=<pfad>) zeigt dasselbe Labor auf einen zweiten
# Checkout oder ein `git worktree`, sodass zwei Braches nebeneinander messbar
# sind, ohne hin und her zu schalten.
#
# WERKZEUG, KEIN BEITRAG. Ein gruener Laborlauf ist Diagnose, kein Nachweis:
# was nicht im laufenden Spiel gesehen wurde, steht als ungesehen im PR-Text.

set -euo pipefail

LAB="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$LAB/out"
REPORT="$LAB/Nova.AiLab/report/build_reports.py"

MEASURE=1
REGENERATE=0
REPO="${NovaRepo:-$LAB/../Project_Nova}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --reports-only) MEASURE=0 ;;
        --regenerate)   MEASURE=0; REGENERATE=1 ;;
        --repo)         shift; REPO="${1:?--repo braucht einen Pfad}" ;;
        -h|--help)      sed -n '3,22p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "lab.sh: unbekannte Option '$1' (--reports-only, --regenerate, --repo, --help)" >&2; exit 2 ;;
    esac
    shift
done

if [[ ! -d "$REPO/Assets/_Project/Scripts" ]]; then
    echo "lab.sh: '$REPO' ist kein Project-Nova-Checkout (kein Assets/_Project/Scripts)." >&2
    echo "        Mit --repo <pfad> oder NovaRepo=<pfad> auf den richtigen zeigen." >&2
    exit 2
fi
REPO="$(cd "$REPO" && pwd)"
export NovaRepo="$REPO"

cd "$LAB"

# Das SDK liegt im Spiel-Checkout, nicht im Labor.
if [[ -d "$REPO/.dotnet" ]]; then
    export DOTNET_ROOT="$REPO/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
fi

if (( MEASURE )); then
    echo "Labor: $LAB"
    echo "misst: $REPO  ($(git -C "$REPO" rev-parse --abbrev-ref HEAD 2>/dev/null || echo 'kein git') @ $(git -C "$REPO" rev-parse --short HEAD 2>/dev/null || echo '?'))"
    echo

    # Reihenfolge wie in AGENTS.md §1. Exit-Code 2 heisst NON-DETERMINISTIC —
    # dann ist jede Zahl aus diesem Lauf wertlos, auch die gruenen, und es wird
    # nicht weitergerechnet. `set -e` bricht deshalb hier absichtlich ab.
    dotnet run --project Nova.AiLab -c Release -- \
        match --trace-every 50 --hash-every 500 --view-every 25 --fog --out "$OUT/match"
    dotnet run --project Nova.AiLab -c Release -- duel     --out "$OUT/duel"
    dotnet run --project Nova.AiLab -c Release -- movement --out "$OUT/movement"
    dotnet run --project Nova.AiLab -c Release -- compare  --out "$OUT/compare"
fi

if (( REGENERATE )); then
    python3 "$REPORT" --regenerate
else
    python3 "$REPORT" "$OUT"
fi

echo
echo "Berichte: $LAB/reports/ (README.md, latest.md, runs/)"
