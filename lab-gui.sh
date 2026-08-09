#!/usr/bin/env bash
#
# lab-gui.sh — die Steuerseite des Labors: messen, ansehen, vergleichen.
#
# Startet einen kleinen lokalen Server und oeffnet die Seite. Dort laesst sich
# in einem Fenster:
#
#   * ein Branch auswaehlen, gegen den gemessen wird
#   * ein Lauf starten und im Protokoll mitlesen
#   * jeder gemessene Lauf im Player oeffnen — Laufrouten, Ereignisse, Einheiten
#   * die archivierte Historie durchsehen (reports/data/)
#   * zwei Laeufe nebeneinanderlegen
#
#   ./lab-gui.sh                 Standardport 8730
#   ./lab-gui.sh --port 9000     anderer Port
#   ./lab-gui.sh --repo <pfad>   einen anderen Checkout messen
#   ./lab-gui.sh --no-browser    nur starten, nichts oeffnen
#
# DER ARBEITSCHECKOUT WIRD NIE UMGESCHALTET. Ein anderer Branch wird in einem
# `git worktree` unter `.worktrees/` gemessen; der Checkout, in dem gearbeitet
# wird, bleibt stehen, wo er steht. Von hier aus wird nichts committet, nichts
# gepusht und nichts gemergt.
#
# NUR AN 127.0.0.1. Diese Gegenstelle startet Prozesse — sie hat im Netz nichts
# zu suchen, auch nicht im eigenen.
#
# WERKZEUG, KEIN BEITRAG. Ein gruener Laborlauf ist Diagnose, kein Nachweis:
# was nicht im laufenden Spiel gesehen wurde, steht als ungesehen im PR-Text.

set -euo pipefail

LAB="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER="$LAB/Nova.AiLab/report/gui_server.py"

PORT=8730
REPO="${NovaRepo:-$LAB/../Project_Nova}"
OPEN=1

while [[ $# -gt 0 ]]; do
    case "$1" in
        --port)       shift; PORT="${1:?--port braucht eine Zahl}" ;;
        --repo)       shift; REPO="${1:?--repo braucht einen Pfad}" ;;
        --no-browser) OPEN=0 ;;
        -h|--help)    sed -n '3,28p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "lab-gui.sh: unbekannte Option '$1' (--port, --repo, --no-browser, --help)" >&2; exit 2 ;;
    esac
    shift
done

if [[ ! -d "$REPO/Assets/_Project/Scripts" ]]; then
    echo "lab-gui.sh: '$REPO' ist kein Project-Nova-Checkout (kein Assets/_Project/Scripts)." >&2
    echo "            Mit --repo <pfad> oder NovaRepo=<pfad> auf den richtigen zeigen." >&2
    exit 2
fi
REPO="$(cd "$REPO" && pwd)"

# Das SDK liegt im Spiel-Checkout, nicht im Labor.
if [[ -d "$REPO/.dotnet" ]]; then
    export DOTNET_ROOT="$REPO/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
fi

# Einmal vorbauen, damit der erste Lauf aus der Seite heraus nicht wie ein
# Haenger aussieht, waehrend im Hintergrund uebersetzt wird.
echo "baue das Labor gegen $REPO …"
NovaRepo="$REPO" dotnet build "$LAB/Nova.AiLab/Nova.AiLab.csproj" -c Release >/dev/null

if (( OPEN )); then
    ( sleep 1; xdg-open "http://127.0.0.1:$PORT/" >/dev/null 2>&1 \
        || open "http://127.0.0.1:$PORT/" >/dev/null 2>&1 || true ) &
fi

exec python3 "$SERVER" --port "$PORT" --repo "$REPO"
