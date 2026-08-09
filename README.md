# Nova.AiLab — KI-Simulationslabor

> **Das Labor ist aus dem Fork ([`arn-c0de/Project_Nova`](https://github.com/arn-c0de/Project_Nova))
> herausgelöst und liegt jetzt in einem eigenen Repository — neben dem
> Spiel-Checkout, nicht mehr darin.**
>
> Der Gewinn ist kein Ordnungsgewinn, sondern ein Messgewinn: Solange das Labor
> **in** einem Branch lag, konnte es nur diesen einen Branch messen. Jeder andere
> hätte erst mit dem Werkzeug bestückt werden müssen — und ein Branch, in den man
> das Messwerkzeug einbaut, ist nicht mehr der Branch, den man messen wollte.
> Von aussen misst es, was drüben ausgecheckt ist: **jeder Branch lässt sich
> gegentesten, ohne Hin- und Herbauen.** `git checkout` drüben genügt, hier
> ändert sich nichts.

## → [Gesamtübersicht](reports/README.md) · [Letzter Lauf](reports/latest.md) · [Verhaltensjournal](reports/behavior-log.md) · [Nächste Schritte](NEXT-STEPS.md)

| Wohin | Was dort steht |
|---|---|
| **[reports/README.md](reports/README.md)** | die Gesamtübersicht: jeder archivierte Laborlauf eine Zeile, der Verlauf innerhalb der aktuellen Definitionstabelle, Links in die Historie |
| **[reports/latest.md](reports/latest.md)** | der zuletzt vermessene Lauf vollständig — Partie, Kandidatenprofile, Gegentabelle, Belagerung, Bewegung. Ohne Browser lesbar |
| **[reports/behavior-log.md](reports/behavior-log.md)** | das Verhaltensjournal, **von Hand geführt**: was geändert wurde, was besser und was schlechter wurde, und was schon widerlegt ist. Vor jeder neuen Änderung lesen |
| **[NEXT-STEPS.md](NEXT-STEPS.md)** | was als nächstes ansteht — sortiert danach, **was ein Spieler in einer Partie merkt**, nicht nach Laborkennzahl. Dazu die begründete Liste dessen, was man nicht anfangen sollte |

Die interaktive Fassung derselben Zahlen ist `out/dashboard.html` — sie braucht
einen Browser, die drei Seiten oben nicht.

**Werkzeug, kein Beitrag — und seit dem Ausbau auch räumlich:**

```
ProjectNova - HASHKRIEG/
├── Project_Nova/        das Spiel, in irgendeinem Branch
└── Nova.AiLab/          dieses Labor, eigenes Repo
```

Drei Repositories, die dabei auseinanderzuhalten sind:

| Repository | Rolle |
|---|---|
| [`VibecodingGermany/Project_Nova`](https://github.com/VibecodingGermany/Project_Nova) | das Spiel selbst — Ziel jedes Pull Requests. Wird nur gefetcht, nie dorthin gepusht |
| [`arn-c0de/Project_Nova`](https://github.com/arn-c0de/Project_Nova) | mein Fork davon, üblicherweise der Checkout unter `Project_Nova/` — von hier geht ein PR nach oben |
| [`arn-c0de/Nova.AiLab`](https://github.com/arn-c0de/Nova.AiLab) | dieses Labor. Kein Beitrag zum Spiel, sondern das Werkzeug, das es vermisst |

Welcher Checkout gemessen wird, entscheidet die MSBuild-Eigenschaft
`NovaRepo` (Vorgabe: `../Project_Nova`). Sie bestimmt beides zugleich — welche
Quelldateien einkompiliert werden **und** welchen Commit die Artefakte als
Herkunft tragen. Die beiden können deshalb nicht auseinanderlaufen:

```bash
./lab.sh                                   # misst ../Project_Nova
./lab.sh --repo /pfad/zu/zweitem/checkout  # oder ein `git worktree`
NovaRepo=/pfad/zu/checkout ./lab.sh        # dasselbe über die Umgebung
```

Ein zweiter Checkout (oder ein `git worktree`) ist der bequeme Weg, zwei
Branches nebeneinander zu messen, ohne dauernd umzuschalten.

**Wem was gehört:** Das Labor gehört mir ([`LICENSE`](LICENSE)) — der Maintainer
und die Mitwirkenden von [Project Nova](https://github.com/VibecodingGermany/Project_Nova)
dürfen es benutzen, um Branches zu vermessen, nur nicht weitergeben. Was ich am
*Spiel* ändere und per Pull Request einreiche, richtet sich dagegen nach den
Bedingungen des Hauptrepos, und zwar nur der eingereichte Diff. Beide Richtungen
und ihre Grenze stehen in [`CONTRIBUTIONS.md`](CONTRIBUTIONS.md).

**Was im Spiel-Repo bleibt:** die In-Game-Debughilfen, weil sie Spielcode sind
— der Bezeichner im F3-Panel, das Aufdecken der Karte, der Zeitraffer. Die
liegen weiterhin auf [`lab/ai-simulation`](https://github.com/arn-c0de/Project_Nova/tree/lab/ai-simulation)
im Fork und gehören in keinen `feat/`-Branch. Plan und Begründung des Labors:
[`docs/feature-ideas/AiSimulationEnvironment.md`](https://github.com/arn-c0de/Project_Nova/blob/lab/ai-simulation/docs/feature-ideas/AiSimulationEnvironment.md).

**Was nicht mitversioniert wird:** `out/` — das ist der jeweils letzte rohe
Lauf, und er ist reproduzierbar. Die *verdichteten* Messblöcke unter
`reports/data/` sind die Quelle, aus der jeder Bericht jederzeit neu entsteht;
die sind versioniert. Nach einem Merge-Fenster des Maintainers ist eine alte
Messmenge ohnehin nicht mehr vergleichbar, und der Bericht sagt das selbst,
statt über die Grenze hinweg zu vergleichen.

**Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden
Spiel gesehen wurde, steht als ungesehen im PR-Text.

## Start

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"   # falls dotnet nicht im PATH ist

# eine KI-gegen-KI-Partie, mit Metriken und Artefakten
dotnet run --project Nova.AiLab -c Release -- match --trace-every 100 --out out/run1

# zwei Läufe desselben Specs, Hash-Ketten verglichen
dotnet run --project Nova.AiLab -c Release -- match --repeat 2 --hash-every 100

# die laufende Partie im Terminal mitsehen
dotnet run --project Nova.AiLab -c Release -- match --watch

# aufzeichnen und danach im Browser zurückspulen: out/run1/player.html öffnen
dotnet run --project Nova.AiLab -c Release -- match --view-every 25 --fog --out out/run1

# Seed-Matrix über alle Kerne, jeder 20. Lauf doppelt zur Selbstkontrolle
dotnet run --project Nova.AiLab -c Release -- sweep --seeds 24 --out out/sweep

# die Gegentabelle: 576 Duelle in Sekunden (Issues 01/02)
dotnet run --project Nova.AiLab -c Release -- duel --out out/duel

# die vier Bewegungsszenarien (Issue 03)
dotnet run --project Nova.AiLab -c Release -- movement --out out/movement

# alle Kandidatenprofile gegen die eingefrorene Referenz: out/compare/report.html
dotnet run --project Nova.AiLab -c Release -- compare --out out/compare

# gegen eine archivierte Ergebnismenge — verweigert bei fremdem Commit oder Definitionstabelle
dotnet run --project Nova.AiLab -c Release -- compare --against out/alt/resultset.json --out out/compare2

# vier Slots (die Karte hat vier Eckplätze)
dotnet run --project Nova.AiLab -c Release -- match --slots 4

dotnet test Nova.AiLab.Tests/Nova.AiLab.Tests.csproj -c Release

# alle vier Laufarten messen und alle Berichte schreiben — ein Kommando
./lab.sh

# nur die Berichte aus dem vorhandenen Lauf: dashboard.html + reports/
python3 Nova.AiLab/report/build_reports.py out

# nur die Markdown-Berichte neu rendern, ohne zu messen (nach Formatänderung)
python3 Nova.AiLab/report/build_reports.py --regenerate

# nur die eine Seite: out/dashboard.html
python3 Nova.AiLab/report/build_dashboard.py Nova.AiLab/out
```

## Zwei Fassungen desselben Laufs

| Fassung | Wo | Wofür |
|---|---|---|
| interaktiv | `out/dashboard.html` | Kurven mit Fadenkreuz, Heatmap mit Abstandsdetail, Scrubber — braucht einen Browser |
| lesbar | `reports/README.md`, `reports/latest.md`, `reports/runs/<id>.md` | dieselben Zahlen als Markdown: auf GitHub direkt lesbar, ohne Download, ohne Server |

`reports/data/<id>.json` ist die **Quelle**, die Markdown-Dateien sind Ableitung:
ein Lauf wird an seinem Fingerabdruck erkannt (zweimal derselbe Lauf ergibt keinen
zweiten Eintrag), und nach einer Formatänderung entsteht die ganze Historie mit
`--regenerate` neu, ohne dass etwas nachgemessen werden muss. `latest.md` ist immer
der zuletzt vermessene Lauf, `README.md` die Gesamtübersicht über alle.

Wechselt die Definitionstabelle, teilt sich die Historie: die Übersicht sagt das
selbst hin und zeichnet den Verlauf **nur** innerhalb der aktuellen Tabelle. Über
ein Merge-Fenster hinweg wird nicht verglichen, auch nicht als Kurve.

Neue Dateien unter `Nova.AiLab/` hält `.git/info/exclude` aus `git status`
heraus — ein neuer Bericht braucht deshalb `git add -f`, sonst fällt er still
unter den Tisch.

## Was hier liegt

Ein Ordner je Laufart — man findet alles über das Kommando, das man gerade fährt.
Alle Dateien liegen im selben Namespace `Nova.AiLab`; die Ordner gliedern, sie
trennen nicht. (Ein `Nova.AiLab.Movement` neben dem benutzten
`Nova.Simulation.Movement` wäre eine Namenskollision, die man sich einhandelt,
ohne etwas dafür zu bekommen.)

### `Match/` — eine Partie fahren

| Datei | Inhalt |
|---|---|
| `MatchSpec.cs` / `SpecFile.cs` | Eingabevertrag (§3.2) und sein JSON-Leser — unbekannte Schlüssel sind Fehler, keine Vorgabewerte |
| `CanonicalOpening.cs` | die D-077-Startaufstellung aus `MatchBootstrap`, Spawnreihenfolge inbegriffen |
| `MultiSlotAiHost.cs` | der Match-Host: `MatchRunner.InitializeMatch` von einem KI-Slot auf N verallgemeinert, sonst nichts |
| `CountingAiPeerTransport.cs` | zählt Intent-Verdikte — die einzige Stelle, an der `intentsRejected` ehrlich entsteht |
| `MatchRun.cs` | fährt eine Partie, liefert Outcome, Entscheidungstick, Hash-Kette, Trace |
| `RunArtifacts.cs` | `result.json`, `trace.ndjson`, `hashchain.json`, `view.ndjson`, `player.html` |

### `Metrics/` — messen, ohne einzugreifen

| Datei | Inhalt |
|---|---|
| `SlotMetrics.cs` / `TraceCollector.cs` | der Metrikkatalog aus §3.3, reiner Beobachter, nur Ganzzahlen |

### `View/` — hinsehen

| Datei | Inhalt |
|---|---|
| `ViewFrame.cs` / `ViewRecorder.cs` | die Sichtframes aus §3.4 — Tätigkeit, nicht nur Position; reiner Beobachter |
| `TerminalView.cs` | ANSI-Liveansicht, beantwortet „läuft gerade etwas schief?" |
| `HtmlPlayer.cs` | eine selbstständige Seite mit canvas: Scrubber, Einzeltick, Ebenen. Kein Build, kein Server |

### `Sweep/` — dieselbe Spec über viele Seeds

| Datei | Inhalt |
|---|---|
| `SeedSeries.cs` / `SweepRunner.cs` | Parallellauf mit Determinismus-Stichprobe (jeder 20. Lauf doppelt) |

### `Duel/` — die Gegentabelle

| Datei | Inhalt |
|---|---|
| `DuelArena.cs` / `DuelTable.cs` | AE-Parität, drei Abstände, beide Richtungen, Belagerung |

### `Movement/` — die vier Bewegungsszenarien

| Datei | Inhalt |
|---|---|
| `MovementScenarios.cs` | `arrival`, `blocking`, `standoff`, `detour` — Hindernisse sind Daten, nicht Code |

### `Compare/` — Kandidat gegen Referenz

| Datei | Inhalt |
|---|---|
| `LabProfiles.cs` | die Kandidatenprofile — heute die einzige Achse mit echter Varianz |
| `TournamentRunner.cs` | jeder Kandidat gegen die Referenz, in **beiden** Fraktionsrollen |
| `ResultSet.cs` / `ResultSetFile.cs` | Ergebnismenge mit Herkunft; verweigert den Vergleich, statt Unvergleichbares zu mischen |
| `ComparisonReport.cs` | der Bericht — Kennzahlen nebeneinander, **keine Rangliste** |
| `PrDraft.cs` | PR-Entwurf mit ausschliesslich Gemessenem; Beobachtungsabschnitt bleibt leer |

### `Cli/` — die Kommandozeile

| Datei | Inhalt |
|---|---|
| `Usage.cs` | der Hilfetext — die einzige Stelle, an der ein Modus in Worten steht |
| `Options.cs` | alle Flags; Spec-Datei als Basis, explizite Flags überschreiben sie |
| `MatchCommand.cs` … `CompareCommand.cs` | je ein Modus, je eine Datei: `match`, `sweep`, `duel`, `movement`, `compare` |

### Wurzel und `report/`

| Datei | Inhalt |
|---|---|
| `Program.cs` | nur `Main` und die Modus-Weiche — rund 50 Zeilen, sonst nichts |
| `lab.sh` | messen und berichten in einem Kommando; `--reports-only`, `--regenerate` |
| `report/lab_data.py` | liest die Artefakte aller vier Laufarten und verdichtet sie zu **einem** Datenblock — die gemeinsame Quelle beider Berichtsformen, dazu Herkunft und Fingerabdruck eines Laufs |
| `report/build_dashboard.py` | bettet diesen Block in die Seite `out/dashboard.html` — Kurven, Gegentabelle als Heatmap, Belagerung, Bewegung. Verdichtet nur, rechnet nichts dazu und vergibt keine Note |
| `report/dashboard.tpl.html` | die Seite dazu: eine Datei, kein Build, kein Server, kein Netzzugriff |
| `report/markdown_report.py` | derselbe Block als Markdown: ein Bericht je Lauf, eine Gesamtübersicht, Kurven als Mermaid. `assert_no_ranking()` hält maschinell fest, dass keine Tabellenzeile eine Note trägt |
| `report/build_reports.py` | der Einstieg: archiviert den Lauf unter `reports/data/`, schreibt Seite und Markdown-Satz, entfernt Berichte ohne Messblock. `--regenerate` rendert die Historie neu, ohne zu messen |
| `reports/` | das Ergebnis: `README.md` (Gesamtübersicht), `latest.md`, `runs/<id>.md`, `data/<id>.json` |

Das Testprojekt `../Nova.AiLab.Tests/` zieht diese Ordner mit **einem** Glob ein;
eine neue Datei steht damit automatisch unter Test. Ausgenommen sind nur
`Program.cs` und `Cli/` (Einstiegspunkt und sein Drumherum) sowie `bin/`, `obj/`
(generierter Code).

## Zwei Dinge, die man wissen muss, bevor man Zahlen liest

**Der Seed ändert die Partie nicht.** Kein Simulationssystem zieht aus dem
Kernel-PRNG; der Seed geht in Zustands-Hash und Snapshot, sonst nirgendwohin.
Ein Sweep über 24 Seeds ist *eine* Beobachtung. Der Sweep sagt das selbst hin,
wenn alle Läufe gleich ausgehen — nicht überlesen.

**Messen darf nichts kosten.** Trace-Collector und Intent-Zählung sind reine
Beobachter, und zwei Tests halten fest, dass ein Lauf mit und ohne sie dieselbe
Hash-Kette liefert. Wenn diese Tests je rot werden, sind alle damit erhobenen
Zahlen wertlos — nicht nur die neuen.

## Die eine Regel, die dieses Labor trägt

Der Host muss dieselbe Partie sein wie das Spiel. Ein abgedrifteter Harness
misst etwas, das es nicht gibt — und meldet dabei weiter saubere Zahlen.
`Nova.AiLab.Tests` prüft das gegen einen handgespiegelten `AiHost` aus
`SkirmishAiTests.cs` und vergleicht Zustands-Hashes, keine abgeschriebenen
Konstanten. Wenn diese Suite rot wird, ist nicht der Test kaputt, sondern die
Spiegelung: dann zuerst `MatchRunner.InitializeMatch` und `MatchBootstrap`
nachziehen, nicht die Erwartung anpassen.

Die Tick-Reihenfolge ist Vertrag, kein Implementierungsdetail. Neue Systeme
werden **eingeordnet, nicht angehängt**, und das Einordnen ist eine Absprache.
