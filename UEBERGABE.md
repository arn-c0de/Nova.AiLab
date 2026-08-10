# Übergabe — alles, was der nächste Agent wissen muss

**Stand:** 2026-08-10 · KI-Verhalten `r7.E34435F9` · Spiel-Checkout auf
`feat/ai-goal-system` (aus `main`, `5635009`) · Labor auf `main` · beide
Testsuiten grün (**715/715** im Spiel, **145/145** im Labor) · Referenzlauf
Entscheidung **3213**, Endzustand **`0xE002DD893916967B`**

> **Zwei Dinge, die seit der letzten Übergabe passiert sind und den Einstieg
> ändern.**
>
> 1. **Upstream ist weitergezogen** (endliche Aetheriumfelder D-102,
>    Bauvoraussetzungen, `Stop` löscht `AttackTarget`), und die Laborspiegelung
>    baute nicht mehr: `CombatSystem` und `FogOfWarSystem` haben neue
>    Konstruktor-Argumente bekommen. Nachgezogen in
>    `MultiSlotAiHost.Build`. **Das ist der Normalfall, nicht der Notfall** — §7
>    sagt, was zu tun ist, wenn die Laborsuite rot wird.
> 2. **`Stop` löscht `AttackTarget` jetzt** (PR #83). Damit ist Befund
>    [F001](findings/F001-stop-loescht-attacktarget-nicht.md) erledigt und die
>    Zielwahl je Einheit nicht mehr gesperrt — *entsperrt*, nicht *begründet*:
>    die vier Messungen aus V003 stehen weiterhin.

Dieses Dokument ist der Einstieg für jemanden, der **nichts** über dieses
Projekt weiss. Es sagt, worum es geht, was wem gehört, wo was liegt, wie
gemessen und getestet wird und welche Fallen schon Zeit gekostet haben.
**Was als nächstes gebaut wird, steht nicht hier, sondern in
[`ROADMAP.md`](ROADMAP.md)** — eine Liste, eine Nummerierung.

---

## 1 · Worum es überhaupt geht

**HashKrieg** ist ein Echtzeit-Strategiespiel (Unity, C#) von
`VibecodingGermany`. Wir sind dort **externe Beitragende**, keine Maintainer:
Zugang ausschliesslich über Fork → Pull Request. Unser Auftrag ist der
**Einheitenstrang** — KI, Bewegung, Kampfwerte, Fraktionsidentität.

Weil man einer KI beim Zusehen alles zutraut, ist daneben ein **Labor**
entstanden: `Nova.AiLab` fährt dieselbe Partie ohne Unity, in Sekunden statt in
gespielten Minuten, und schreibt jeden Tick mit. Es ist **Werkzeug, kein
Beitrag** — eigenes Repository, eigene Lizenz, nie Teil eines Pull Requests.

Drei Repositories, die auseinanderzuhalten sind:

| Repository | Rolle | Was wir dürfen |
|---|---|---|
| `VibecodingGermany/HashKrieg` | **das Spiel** (vormals `Project_Nova`) | nur fetchen und lesen. **Nie dorthin pushen** — der Weg ist ein PR |
| `arn-c0de/Project_Nova` | **mein Fork**, liegt lokal unter `Project_Nova/` | Topic-Branches pushen. **Nie auf `main`** |
| `arn-c0de/Nova.AiLab` | **das Labor**, liegt lokal unter `Nova.AiLab/` | alles, `main` inbegriffen — es ist mein eigenes |

## 2 · Die ersten zehn Minuten

```bash
cd "ProjectNova - HASHKRIEG"

# 1  Der Arbeitsvertrag. Ohne den ist jede Änderung ein Ratespiel.
cat CLAUDE.md

# 2  Wo die Zahlen heute stehen, und warum sie sich bewegt haben
cat Nova.AiLab/reports/latest.md
cat Nova.AiLab/reports/behavior-log.md      # von Hand geführt, Abschnitt „Widerlegt"

# 3  Was als nächstes ansteht
cat Nova.AiLab/ROADMAP.md

# 4  Einmal selbst messen (rund zwei Minuten)
cd Nova.AiLab && ./lab.sh
```

**Vor jeder neuen Idee zuerst ins Verhaltensjournal.** Dort steht je Änderung
ein Abschnitt „Widerlegt". Drei der bisherigen Versuche sind gebaut, gemessen
und wieder ausgebaut worden — eine Sackgasse, die niemand aufgeschrieben hat,
wird zuverlässig ein zweites Mal gelaufen.

## 3 · Wo was liegt

```
ProjectNova - HASHKRIEG/            ← Arbeitsverzeichnis, selbst KEIN git-Repo
├── CLAUDE.md                       der Arbeitsvertrag. Gilt immer, überschreibt Vorgaben
├── SCOPES HASHKRIEG - .../         Scope-Vorgaben des Maintainers — LESEN, NIE ÄNDERN
├── .claude/hooks/guard-push.py     blockt jeden Push, der nicht in den Fork gehört
│
├── Project_Nova/                   ← der Spiel-Checkout (Fork), heute auf `main`
│   ├── Assets/_Project/Scripts/    der ganze Spielcode
│   │   ├── AI/                     UNSER: SkirmishAiSystem, CombatStrength, WaveStrengthGate
│   │   ├── AI.Data/                UNSER: AiProfile, AiProfiles, AiBehaviorId
│   │   ├── Simulation/
│   │   │   ├── Movement/           UNSER
│   │   │   ├── Combat/             UNSER: WeaponProfiles, DamageMatrix, ArmorClass
│   │   │   ├── Factions/           UNSER
│   │   │   ├── Pathfinding/        uns seit v1.1.0 — IsWalkable-Semantik bleibt unberührt
│   │   │   ├── Definitions/        GETEILT: nur nach Absprache
│   │   │   ├── Replays|Snapshots|State/   GESPERRT: Inhaberentscheidung mit D-ID
│   │   │   └── Construction|Economy/      Netzstrang ab Sprint 16
│   │   ├── Gameplay/Match/         FREMD: MatchRunner, MatchConfig, MatchBootstrap
│   │   ├── Networking/             FREMD
│   │   └── Core|Data|Presentation/ nicht zugeteilt → fragen
│   ├── tools/Nova.SimRunner.Tests/ die grosse Suite (655). Neue Tests: ja. Baselines: nein
│   ├── CHANGELOG.md                genau EIN Eintrag je PR unter [Unreleased]
│   └── .dotnet/                    das SDK (8.0.318), falls `dotnet` nicht im PATH ist
│
└── Nova.AiLab/                     ← das Labor, eigenes Repo
    ├── START-HIER.md               eine Seite: wofür, warum, wie
    ├── ROADMAP.md                  die EINZIGE Nummerierung dessen, was als nächstes kommt
    ├── UEBERGABE.md                dieses Dokument
    ├── AGENTS.md                   Regelkreis, Exit-Codes, jedes Artefaktfeld
    ├── NEXT-STEPS.md               warum die Punkte so eingeordnet sind
    ├── KAMPFSTAERKE.md             Detailplan: Kampfpunkte, Wellengrösse, Ausbau
    ├── VERTEIDIGUNG.md             Detailplan: Sammeln abbrechen, wenn die Basis brennt
    ├── GOALS.md                    Detailplan: Goal-System, Flanke, Admin-Panel
│                               — mit einer Tabelle vorn, was davon gebaut ist
    ├── PLAYTEST-CHECKLIST.md       was in der GESPIELTEN Partie zu prüfen ist
    ├── lab.sh / lab-gui.sh         messen im Terminal / im Browser
    ├── Nova.AiLab/                 der Laborquelltext, ein Ordner je Laufart
    │   ├── Match|Sweep|Duel|Movement|Compare/   die fünf Modi
    │   ├── Metrics/                reine Beobachter — sie dürfen nichts kosten
    │   ├── View/                   Sichtframes, Spur, HtmlPlayer
    │   ├── report/                 Dashboard, Markdown-Berichte, GUI-Server
    │   └── Cli/                    je ein Modus, je eine Datei
    ├── Nova.AiLab.Tests/           die Laborsuite (129)
    ├── reports/                    latest.md, runs/, data/, behavior-log.md
    ├── out/                        der letzte ROHE Lauf — nicht versioniert
    ├── pr/                         PR-Texte zum Einfügen
    ├── findings/                   Befunde in fremdem Terrain: beschreiben, nicht reparieren
    └── notes/                      z. B. wie die Schadensquelle hergeleitet wird
```

**Zwei Dinge, über die man stolpert:**

- `Project_Nova/tools/Nova.AiLab.Tests/` existiert lokal, ist aber **nicht
  getrackt** — ein Rest aus der Zeit, als das Labor noch im Spiel-Checkout lag.
  Nicht committen, nicht mit `Nova.AiLab/Nova.AiLab.Tests/` verwechseln.
- Im Spiel-Checkout sind dauerhaft zwei Dateien geändert
  (`AssetMappingRegistry.asset`, `ProjectSettings/PackageManagerSettings.asset`).
  Das ist Unity-Rauschen und gehört in keinen Commit von uns.

## 4 · Was uns gehört — und was nicht

**Ohne Rückfrage editieren:** `AI/`, `AI.Data/`, `Simulation/Movement/`,
`Simulation/Combat/`, `Simulation/Factions/`, `Simulation/Pathfinding/`, neue
Dateien in `tools/Nova.SimRunner.Tests/`, `CHANGELOG.md`.

**Nicht anfassen — ein PR, der das berührt, wird zurückgegeben, nicht gemergt:**

| Bereich | Wem |
|---|---|
| `Networking/`, `Gameplay/Match/`, `Gameplay/UI/`, `Gameplay/Input/`, `tools/Nova.RelayServer/` | Netzstrang |
| `Simulation/Construction/`, `Simulation/Economy/` | Netzstrang ab Sprint 16 |
| `Simulation/Replays|Snapshots|State/` | Inhaberentscheidung mit D-ID |
| `Simulation/Definitions/` | **geteilt** — `WeaponDefinition`/`UnitDefinition` brauchen wir, `BuildingDefinition`/`SimDefinitions` nicht. Änderung wird **vorher gefragt** |
| `Core/`, `Data/`, `Presentation/`, `Simulation/{Systems,Vision,Commanders,Production,CommandsV1,Victory}/`, `.github/`, `quality/`, `docs/`, `ProjectSettings/` | nicht zugeteilt → fragen |

**Drei Verträge, die wir benutzen und nie ändern:** `ICommandTransport` /
`ICommandSubmissionReadiness`; Match-Fingerprint und Schemaversionen; **die
Tick-Reihenfolge der Systeme** — neue Systeme werden *eingeordnet*, nicht
angehängt, und das Einordnen ist eine Absprache. Bisher kam noch jede
Verhaltensänderung ohne neues System aus: alles liegt in `SkirmishAiSystem`.

## 5 · Die fünf harten Regeln

1. **Determinismus.** Unter `Simulation/` und `AI*`: kein `float`/`double`
   (Festkomma `SimFixed`), kein `System.Random` (nur der Sim-RNG, und den zieht
   heute kein System), keine Wanduhr, kein `Time.deltaTime`, **keine
   Abhängigkeit von der Iterationsreihenfolge** eines `Dictionary`/`HashSet`.
   Gleichstand über die niedrigere rohe Entity-Id. Zwei Rechner müssen bitgenau
   dasselbe rechnen, sonst bricht die Netzpartie ab — zu Recht.
2. **Verhalten und Baseline nie im selben PR.** Vier Dateien enthalten
   festgeschriebene Hashes (`SnapshotGoldenBytesTests`, `CommandGoldenBytesTests`,
   `SimRandomGoldenTests`, `Determinism10000Tests`). Dass sie bei einer
   Verhaltensänderung rot werden, **ist ihr Zweck**. Erwartungswerte anzupassen,
   damit die CI grün wird, ist der eine Fehler, gegen den die ganze Regel gebaut
   ist. Eine CI erzwingt es inzwischen.
3. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden
   Spiel gesehen wurde, steht als ungesehen im PR-Text. Der Abschnitt „Im
   laufenden Spiel gesehen" bleibt leer **und als leer erkennbar**.
4. **Keine Rangfolge.** Die Berichte legen Werte nebeneinander und vergeben
   keine Note; `assert_no_ranking()` hält das maschinell fest. Es gibt keine
   skalare Gütefunktion — und deshalb auch keinen automatischen Optimierer.
5. **Jede Verhaltensregel bringt eine Aus-Stellung mit und wird einseitig
   gemessen.** Eine Coderegel steckt im Binary und erreicht im Selbstspiel
   **beide** KIs; „zwei stärkere Armeen" ist dort von „schlechtere KI" nicht zu
   unterscheiden. Kosten: ein `int` und ein `if`.

## 6 · Wie gemessen wird

```bash
cd Nova.AiLab
export DOTNET_ROOT="$PWD/../Project_Nova/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"

./lab.sh                                   # alle vier Laufarten + alle Berichte
./lab-gui.sh                               # dasselbe im Browser, inkl. Branchwahl
./lab.sh --repo /pfad/zu/anderem/checkout  # einen anderen Stand messen
```

Einzeln, wenn man eine bestimmte Frage hat:

| Frage | Kommando | Antwort in |
|---|---|---|
| Hat sich das Verhalten geändert? | `match --hash-every 100 --out out/x` | `finalStateHash`, `decidedTick`; `hashchain.json` sagt **ab welchem Tick** |
| Rechnet es bitgleich? | `match --repeat 2` | **dem Exit-Code** |
| Besser oder nur anders? | `compare --out out/c` | `resultset.json`, `report.html` |
| Woran liegt es bei *dieser* Einheit? | `match --view-every 25 --fog --out out/x` | `player.html` |
| Was hatte sie **vor**? | derselbe Lauf | `goals.ndjson`, im Player unter „what the AI wants" |
| Was hätte sie getan, **wenn**? | `live --port 8787 --out out/x` | der Browser — und `intervened: true`, das den Lauf als Nicht-Messung kennzeichnet |

**Exit-Codes sind der Befund, nicht der Text:** `0` durchgelaufen, `1`
Bedienfehler, **`2` = nicht deterministisch → sofort aufhören.** Jede Zahl aus
so einem Lauf ist wertlos, auch die grünen.

Gemessen wird immer der Checkout, auf den `NovaRepo` zeigt (Vorgabe
`../Project_Nova`) — dieselbe Eigenschaft bestimmt die Quelldateien **und** die
Herkunftsangabe der Artefakte, damit die beiden nicht auseinanderlaufen können.

## 7 · Wie getestet wird

```bash
# Die grosse Suite im Spiel — 655 Tests, hier 11 s
cd Project_Nova
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test tools/Nova.SimRunner.Tests/Nova.SimRunner.Tests.csproj -c Release

# Die Laborsuite — 129 Tests, hier 12 s
cd ../Nova.AiLab
export DOTNET_ROOT="$PWD/../Project_Nova/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Nova.AiLab.Tests/Nova.AiLab.Tests.csproj -c Release

# Python und JavaScript im Labor
python3 -m py_compile Nova.AiLab/report/*.py
node --check Nova.AiLab/report/uikit/icons.js

# Berichte neu rendern, ohne zu messen (nach einer Formatänderung)
python3 Nova.AiLab/report/build_reports.py --regenerate
```

**Beide Suiten laufen, keine ersetzt die andere.** Die grosse prüft das Spiel,
die kleine prüft, dass das Labor dieselbe Partie fährt wie das Spiel — sie
vergleicht Zustands-Hashes gegen einen handgespiegelten Host, keine
abgeschriebenen Konstanten. **Wird die Laborsuite rot, ist nicht der Test
kaputt, sondern die Spiegelung:** dann zuerst `MatchRunner.InitializeMatch` und
`MatchBootstrap` nachziehen, nicht die Erwartung anpassen.

`global.json` im Spiel pinnt SDK `8.0.318` mit `rollForward: disable`. Das
mitgelieferte `.dotnet/` ist genau diese Version, deshalb geht es hier direkt;
mit einem abweichenden SDK muss `dotnet test` aus einem Arbeitsverzeichnis
**ausserhalb** des Checkouts laufen, sonst greift der Pin.

**Der Nachweis für eine verhaltensneutrale Umstellung ist kein Test, sondern
eine Zahl:** Entscheidungstick und Endzustands-Hash bleiben gleich, die
Artefakte sind byte-identisch bis auf `elapsedMilliseconds`.

## 8 · Git, Commits, Push

**Labor** (`Nova.AiLab/`): eigenes Repo, direkt auf `main` committen und pushen
ist in Ordnung — `git push origin main`.

**Spiel** (`Project_Nova/`): Topic-Branch (`feat/…`, `fix/…`, `refactor/…`),
`git push -u origin <branch>`, dann PR nach `upstream/main`. **Verboten**, auch
wenn die Rechte technisch da wären: jeder Push nach `upstream`, jeder Push auf
`main`/`master` eines Spiel-Repos, `git push` ohne explizites Ziel,
Force-Push, `--all`, `--mirror`, History-Rewrites, `gh pr merge`.

`.claude/hooks/guard-push.py` prüft das vor jedem Push. **Wenn der Hook
zuschlägt: nicht das Kommando umformulieren, bis es durchgeht** — dann stimmt
das Ziel nicht.

Stil, wie er in diesem Projekt gilt:

- **Commit-Messages auf Deutsch**, Betreff ASCII (`ae`/`oe`/`ue`/`ss`), ≤ 72
  Zeichen, Conventional-Commit-Präfix. Branchnamen bleiben englisch, Code,
  Bezeichner und Pfade auch.
- **Keine Claude-Attribution**, keine `Co-Authored-By`-Trailer.
- **Nicht sofort committen** — auf ausdrückliche Freigabe warten.
- **Vor jedem Push fragen und das Ziel nennen**: ausdrücklich *Labor*, *Fork*
  oder *Hauptrepo*. Commit, Push und PR sind drei getrennte Freigaben; keine
  gilt für den nächsten Schritt mit.
- Ein PR, der Verhalten ändert, beschreibt die **gespielte Beobachtung**.
  Grüner Test allein reicht nicht.

## 9 · Stand heute

Gebaut und in `upstream/main`: Score-Zielwahl (`r1`), Sammelpunkt und Wellen
(`r3`), Rückzug je Einheit (`r4`), Erreichbarkeitsdeckel (`r5`),
Stärke-Wellentor (`r6`, PR #72). Verworfen und **nicht noch einmal anfangen**:
`DefendBase` in der Fassung von V002, Zielen unterhalb der Angriffsschwelle
(V003), Rally-Punkt als Sammelbefehl (F002).

**Seit dieser Übergabe dazugekommen, noch nicht im Upstream:** das
**Goal-System als Form** (ROADMAP Punkt 2) auf `feat/ai-goal-system` — vier
benannte Module statt einer if-Kette, verhaltensneutral und byte-identisch
nachgewiesen, dazu zwei optionale Nähte, die der ausgelieferte Pfad nie füllt
(`IAiGoalObserver`, `IAiGoalOverride`). Im Labor darauf aufbauend
`goals.ndjson`, die Goal-Anzeige im Player und der `live`-Modus mit Eingriff.
**Noch nicht committet, noch nicht gepusht, kein PR.**

> **Der wichtigste offene Punkt:** `r6` ist gebaut und im ausgelieferten Spiel
> **wirkungslos**. `waveStrengthPoints: 1200` gegen eine Armeeobergrenze von 12
> lässt den Erreichbarkeitsdeckel greifen, übrig bleibt „sammle die ganze
> Armee". Das Tor entscheidet erst ab Obergrenze 13 (Allianz) bzw. 29 (Legion) —
> und die 12 steht in `MatchRunner.cs:254`, also im Netzstrang. **Das ist eine
> Rückfrage an den Maintainer, kein PR von uns**, und der ganze Stärkestrang
> hängt daran ([`ROADMAP.md`](ROADMAP.md) §4).

## 10 · Fallen, die schon Zeit gekostet haben

| Falle | Was man wissen muss |
|---|---|
| **Ein Angriffsbefehl ist unumkehrbar** | `Stop()` löscht `AttackTarget` nicht; freigegeben wird es nur durch den Tod des Ziels. Wer einer **stehenden** Einheit ein Ziel gibt, nimmt sie dauerhaft aus der Auto-Acquisition. Befund F001, blockiert die Zielwahl je Einheit |
| **Der Rally-Punkt ist die Spawn-Zelle** | Kein Sammelbefehl. Ihn auf den Sammelpunkt zu setzen wäre Teleportation. Befund F002 |
| **Befehlsrauschen kippt eine gute Idee** | `DefendBase` scheiterte an +23 % Intents durch Pendeln, nicht am Radius (8/16/24 gemessen). **Intents je 1.000 Ticks ist bei jeder Änderung die erste Zahl** |
| **Schweigen ist eine Wirkung** | Eine wartende Einheit bekommt absichtlich **keinen** Befehl. Gibt man ihr je Kadenz denselben, steigen die Aktionen je Minute von 23 auf 40, ohne dass sich etwas ändert |
| **Der Seed ist keine Achse** | Kein System zieht aus dem Kernel-PRNG. Ein Sweep über 24 Seeds ist *eine* Beobachtung — der Bericht sagt das selbst hin |
| **`unitsLost` und `lowPowerTicks` sind kumulativ** | Wer sie als Intervallwerte liest, macht aus einer flachen Wirtschaft eine einbrechende |
| **`overshootCells` ist die falsche Spalte** | Sie misst gegen die *nominale* Reichweite, die ohne Aufklärung nicht nutzbar ist. Gemeint ist `usableRangeOvershootCells` |
| **Reichweite ohne Aufklärung ist wirkungslos** | Eine Gruppe, die auf voller Reichweite stehenbleibt, richtet über 2.000 Ticks **null** Schaden an. Abstandhalten und Aufklärung nur als Paar |
| **Es gibt keine Blickrichtung** | Kein Facing in `Simulation/` — „Flanke" kann hier keinen Schadensbonus bedeuten |
| **`goal` im Ereignisprotokoll ist die Wegzelle** | Nicht das Vorhaben der KI. Beim Goal-System wird es auf `pathGoal` umbenannt |
| **Neue Dateien im Labor** | `.git/info/exclude` kann sie aus `git status` heraushalten; im Zweifel `git add -f` |

## 11 · Was man nicht tut

- Eine Baseline nachziehen, damit die CI grün wird.
- Ein Laborergebnis so formulieren, als sei es gespielt worden.
- Ein neues System registrieren oder die Tick-Reihenfolge ändern.
- `InstallDefenseModule` benutzen — wird deterministisch abgelehnt (G2/G4).
- Eine Zahl in `Simulation/Definitions/` ändern, ohne zu fragen.
- Einen Optimierer bauen. Es gibt keine skalare Gütefunktion, absichtlich.
- Nach `upstream` pushen oder auf ein `main` des Spiels.
- Etwas als fertig melden, das nicht gelaufen ist.

## 12 · Weiterlesen, in dieser Reihenfolge

1. [`CLAUDE.md`](../CLAUDE.md) — der Arbeitsvertrag, gilt über allem
2. [`START-HIER.md`](START-HIER.md) — das Labor in fünf Minuten
3. [`reports/behavior-log.md`](reports/behavior-log.md) — was schon versucht wurde
4. [`ROADMAP.md`](ROADMAP.md) — was als nächstes kommt und warum in dieser Folge
5. [`AGENTS.md`](AGENTS.md) — der Regelkreis und jedes Artefaktfeld
6. Der Detailplan zum Punkt, den du baust: [`VERTEIDIGUNG.md`](VERTEIDIGUNG.md),
   [`KAMPFSTAERKE.md`](KAMPFSTAERKE.md), [`GOALS.md`](GOALS.md)
7. [`PLAYTEST-CHECKLIST.md`](PLAYTEST-CHECKLIST.md) — bevor irgendetwas „fertig" heisst
