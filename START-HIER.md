# Start hier — das Labor in fünf Minuten

Nova.AiLab fährt die KI-Partie von **HashKrieg** ohne Unity, tausendfach,
und schreibt auf, was dabei passiert. Es ist **Werkzeug, kein Beitrag**: eigenes
Repository, eigene Lizenz, kein Pull Request ins Spiel.

| | |
|---|---|
| **Wofür** | Herausfinden, ob eine Änderung am KI-Verhalten wirkt — in Sekunden statt in gespielten Partien |
| **Warum überhaupt** | Weil man einer KI beim Zusehen alles zutraut. Das Labor liefert Zahlen, die zwei Läufe vergleichbar machen, statt Eindrücke |
| **Wie** | `./lab.sh` messen, `reports/latest.md` lesen, `out/player.html` hinsehen |
| **Was als nächstes** | [`ROADMAP.md`](ROADMAP.md) — eine Liste, eine Nummerierung |

**Stand:** KI-Verhalten `r6.E34435F9`, Commit `c8e46af5`, Definitionstabelle
`0x6326FA3E56CFF5A3` · gemessen in [`reports/latest.md`](reports/latest.md)

---

## Wofür — die vier Fragen, die es beantwortet

| Frage | Kommando | Antwort steht in |
|---|---|---|
| Hat sich das Verhalten überhaupt geändert? | `match --hash-every 100` | `finalStateHash`, `decidedTick` — und `hashchain.json` sagt **ab welchem Tick** |
| Rechnen zwei Läufe bitgleich? | `match --repeat 2` | **dem Exit-Code.** `2` heisst sofort aufhören |
| Ist es besser oder nur anders? | `compare` | `resultset.json`, `report.html` |
| Woran liegt es bei *dieser* Einheit? | `match --view-every 25 --fog` | `player.html` — Einheit anklicken: Laufroute, Treffer, Tod, Ereignisband |

## Warum man den Zahlen glauben kann — vier Regeln

1. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden
   Spiel gesehen wurde, steht als ungesehen im PR-Text. Auch wenn es im Video
   überzeugend aussieht.
2. **Messen kostet nichts.** Trace und Intent-Zählung sind reine Beobachter;
   zwei Tests halten fest, dass ein Lauf mit und ohne sie dieselbe Hash-Kette
   liefert. Werden die rot, sind *alle* damit erhobenen Zahlen wertlos.
3. **Der Host ist dieselbe Partie wie das Spiel.** Ein abgedrifteter Harness
   misst etwas, das es nicht gibt, und meldet dabei weiter saubere Zahlen.
   `Nova.AiLab.Tests` prüft die Spiegelung gegen Zustands-Hashes, nicht gegen
   abgeschriebene Konstanten.
4. **Keine Rangfolge.** Die Berichte stellen Werte nebeneinander und vergeben
   keine Note; `assert_no_ranking()` hält das maschinell fest. Es gibt keine
   skalare Gütefunktion, und für „sieht im Spiel richtig aus" gibt es keine
   Kennzahl.

Zwei Dinge, die man vor der ersten Tabelle wissen muss: **der Seed ändert die
Partie nicht** (kein System zieht aus dem Kernel-PRNG — ein Sweep über 24 Seeds
ist *eine* Beobachtung), und **`unitsLost` ist kumulativ**, nicht je Intervall.

## Wie — sechs Kommandos

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"   # falls nötig

./lab.sh                 # alles messen, alle Berichte schreiben — der Normalfall
./lab-gui.sh             # dasselbe im Browser: Branch wählen, messen, hinsehen
./lab.sh --repo /pfad    # einen anderen Checkout messen (oder NovaRepo=…)

dotnet run --project Nova.AiLab -c Release -- match --view-every 25 --fog --out out/run1
dotnet run --project Nova.AiLab -c Release -- duel     --out out/duel      # 576 Duelle
dotnet run --project Nova.AiLab -c Release -- compare  --out out/compare   # Kandidat gegen Referenz
```

Gemessen wird immer der Checkout, auf den `NovaRepo` zeigt (Vorgabe
`../Project_Nova`) — dieselbe Eigenschaft bestimmt die Quelldateien **und** die
Herkunftsangabe der Artefakte, damit die beiden nicht auseinanderlaufen.

Danach lesen, in dieser Reihenfolge: [`reports/latest.md`](reports/latest.md)
(wo die Zahlen stehen) → [`reports/behavior-log.md`](reports/behavior-log.md)
(warum sie sich bewegt haben, **von Hand geführt**) →
[`out/dashboard.html`](out/dashboard.html) (dieselben Zahlen interaktiv).

> [!IMPORTANT]
> **Vor jeder neuen Idee zuerst ins Verhaltensjournal.** Dort steht je Änderung
> ein Abschnitt „Widerlegt". Eine Sackgasse, die niemand aufgeschrieben hat,
> wird ein zweites Mal gelaufen — drei der bisherigen Versuche sind genau so
> entstanden.

## Was als nächstes passiert

Kurzfassung; die vollständige, begründete und einzige Nummerierung steht in
[`ROADMAP.md`](ROADMAP.md).

| Als nächstes | Was der Spieler merken soll |
|---|---|
| **Sammeln abbrechen, wenn die Basis brennt** | Sie lässt ihr HQ nicht mehr zusammenschiessen, während neun Einheiten wartend daneben stehen |
| **Nachschub hinter der laufenden Welle** | Neue Einheiten laufen dem Gefecht hinterher, statt auf die nächste Welle zu warten — und bei gebrochener Welle gar nicht |
| **Wellengrösse nach Lage** | Kleine Stösse gegen kleine Gruppen, Grossangriff gegen eine verteidigte Basis. Nicht immer dieselbe Wellengrösse |
| **Basis ausbauen, Fahrzeuge kaufen** | Die KI gibt ihre Punkte aus, statt auf 17.000 AE zu sitzen |
| **Formationsangriff / Flanke** | Der Angriff kommt nicht mehr auf einer Linie, wenn zwei Wege billiger sind |
| **Goal-System und Admin-Panel** | Man kann im Labor sehen, *was* sie gerade vorhat, und es übersteuern — [`GOALS.md`](GOALS.md) |

## Wo was steht

| Datei | Wofür man sie aufmacht |
|---|---|
| **[`ROADMAP.md`](ROADMAP.md)** | Was gebaut ist, was als nächstes kommt, was Rückfrage an den Maintainer ist. **Die einzige Nummerierung** |
| [`reports/latest.md`](reports/latest.md) · [`reports/README.md`](reports/README.md) | Der letzte Lauf vollständig · alle Läufe als Übersicht |
| [`reports/behavior-log.md`](reports/behavior-log.md) | Das Verhaltensjournal: besser, schlechter, widerlegt. Von Hand |
| [`AGENTS.md`](AGENTS.md) | Der Regelkreis für eine Verhaltensänderung, Exit-Codes, alle Artefaktfelder |
| [`NEXT-STEPS.md`](NEXT-STEPS.md) | **Warum** die Punkte in der Roadmap so stehen — sieben Beobachtungen aus gespielten Partien |
| [`KAMPFSTAERKE.md`](KAMPFSTAERKE.md) | Der Detailplan hinter Kampfpunkten, Wellengrösse, Nachschub, Ausbau, Schwierigkeitsgraden |
| [`VERTEIDIGUNG.md`](VERTEIDIGUNG.md) | Der Detailplan „Sammeln abbrechen, wenn die eigene Basis angegriffen wird" |
| [`GOALS.md`](GOALS.md) | Der Detailplan des modularen Goal-Systems und des Admin-Panels |
| [`PLAYTEST-CHECKLIST.md`](PLAYTEST-CHECKLIST.md) | Was in der gespielten Partie zu prüfen ist — die einzige Stelle, die „fertig" sagen darf |
| [`LAUFROUTEN.md`](LAUFROUTEN.md) | Wie die Laufroutenaufzeichnung funktioniert (gebaut) |
| [`README.md`](README.md) | Das Labor selbst: Aufbau, jede Datei, jedes Kommando |
| [`CONTRIBUTIONS.md`](CONTRIBUTIONS.md) · [`LICENSE`](LICENSE) | Die Grenze zwischen Labor und Spiel, und wer was damit darf |
| [`findings/`](findings) · [`pr/`](pr) · [`notes/`](notes) | Befunde in fremdem Terrain (beschreiben, nicht reparieren) · PR-Texte zum Einfügen · Notizen wie die hergeleitete Schadensquelle |

## Drei Sätze, die man nicht vergessen darf

1. **Verhalten und Baseline nie im selben PR.** Eine rote Baseline ist ihr
   Zweck, kein Defekt; die neue Baseline kommt in einen eigenen PR mit altem
   Wert, neuem Wert und Begründung.
2. **Ins Spiel wird nur über den Fork gepusht, nie auf dessen `main`.** Commit,
   Push und PR sind drei getrennte Freigaben — keine gilt für den nächsten
   Schritt mit. (Dieses Repo hier ist mein eigenes; die Regel gilt für
   `arn-c0de/Project_Nova` → `VibecodingGermany/HashKrieg`.)
3. **Was nicht gelaufen ist, wird nicht als fertig gemeldet.** Der Abschnitt
   „Im laufenden Spiel gesehen" bleibt leer und als leer erkennbar, bis ein
   Mensch gespielt hat.
