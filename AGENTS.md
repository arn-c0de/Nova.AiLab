# Nova.AiLab — Handreichung für Agenten

**Stand:** KI-Verhalten `r8.1E6E7AE3`, Commit `1d330a05`
(`feat/ai-goal-system-r8`), Definitionstabelle `0xD5F219A3F68088FF` ·
Referenzlauf [`20260810-1858-1d330a05`](reports/latest.md): Entscheidung
**6.490**, Endzustand **`0x0F892EFC042D6514`** · **die Zahlen in §5 stammen
aus genau diesem Lauf**
**Gilt zusätzlich:** `../CLAUDE.md` (Arbeitsvertrag), `../Project_Nova/AGENTS.md`,
[`README.md`](README.md) nebenan
**Erst lesen, wenn man neu hier ist:** [`UEBERGABE.md`](UEBERGABE.md) (der
vollständige Einstieg über beide Repositories) und [`START-HIER.md`](START-HIER.md)

Dieses Dokument beantwortet **eine** Frage: wie ein Agent das Labor für
automatische Evaluierung benutzt. **Was als nächstes gebaut wird, steht in
[`ROADMAP.md`](ROADMAP.md)** — eine Liste, eine Nummerierung.

---

## 0. Vier Sätze, bevor irgendetwas läuft

1. **Werkzeug, kein Beitrag.** Das Labor liegt seit dem Ausbau in einem
   **eigenen Repository** neben dem Spiel-Checkout und gerät in keinen
   `feat/`-Branch. `feat/`-Branches werden frisch von `upstream/main`
   abgezweigt. Im Spiel-Repo bleiben nur die In-Game-Debughilfen, auf
   `lab/ai-simulation`.
2. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden Spiel
   gesehen wurde, steht als ungesehen im PR-Text. Kein generierter Satz darf ein
   Laborergebnis so formulieren, als sei es gespielt worden.
3. **Verhalten und Baseline nie im selben PR.** Seit `e1a6a57` erzwingt das eine CI
   (`.github/workflows/baseline-guard.yml`), nicht mehr nur Disziplin — siehe §4.
4. **Ins Spiel wird nur über den Fork gepusht, nie auf dessen `main`.** Commit,
   Push und PR sind drei getrennte Freigaben; keine gilt für den nächsten Schritt
   mit. Das Labor ist ein eigenes Repo — dort ist `main` frei.

---

## 1. Der Regelkreis

Ein Agent, der KI-Verhalten ändert, braucht auf vier Fragen eine maschinenlesbare
Antwort. Alle vier kosten zusammen unter zehn Sekunden.

| Frage | Kommando | Antwort steht in |
|---|---|---|
| Hat sich das Verhalten überhaupt geändert? | `match --hash-every 100 --out <dir>` | `result.json` → `finalStateHash`, `decidedTick`; `hashchain.json` → **ab welchem Tick** es auseinanderläuft |
| Ist die Änderung deterministisch? | `match --repeat 2 --hash-every 100` | **Exit-Code** (siehe unten), nicht der Text |
| Ist sie besser oder nur anders? | `compare --out <dir>` | `resultset.json`, `report.html`, je Kandidat ein PR-Entwurf |
| Woran liegt es? | `match --view-every 25 --fog --out <dir>` | `player.html` + `view.ndjson`, dazu `dashboard.html` |
| Woran liegt es **bei dieser einen Einheit**? | derselbe Lauf | `player.html`: Einheit anklicken → Laufroute, Ereignisband, Detailfeld mit ihrem aktuellen Verhalten (Ziel, Angreifer, Ernte, Rückweg). Roh in `tracks.ndjson`, `events.ndjson`, `units.json` |
| **Was hat die KI vorgehabt** — und wie weit ist sie vom nächsten Vorhaben? | derselbe Lauf | `goals.ndjson`, im Player unter „what the AI wants": Goal, seit wann, welches davor, die Zahlen der Bedingung und der Abstand zur nächsten Schwelle |
| Und **was hätte sie getan, wenn** …? | `live --port 8787 --out <dir>` | die laufende Partie im Browser: anhalten, einzeln takten, einer Auswahl ein Goal aufzwingen. Schreibt `overrides.ndjson` und ein `result.json` mit `intervened: true` |
| Und **auf einem anderen Branch**? | `./lab-gui.sh` | die Steuerseite: Branch auswählen, messen, gegen einen früheren Lauf legen. Der Arbeitscheckout wird nie umgeschaltet — gemessen wird in einem `git worktree` |

Vorspann für alle Kommandos, falls `dotnet` nicht im PATH ist:

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
```

### Exit-Codes — die einzige Stelle, an der der Rückgabewert selbst ein Befund ist

| Code | Bedeutung | Was ein Agent tun muss |
|---:|---|---|
| `0` | Lauf durch | weiterlesen in den Artefakten |
| `1` | Bedienfehler (unbekannter Modus, kaputte Spec) | Kommando korrigieren |
| `2` | **`NON-DETERMINISTIC`** bzw. **`SWEEP INVALID`** | **Sofort stoppen.** Zwei Läufe desselben Specs sind auseinandergelaufen. Das ist kein Flake, sondern geteilter Zustand zwischen parallelen Matches oder ein Determinismusbruch im Verhalten. Jede Zahl aus diesem Lauf ist wertlos, auch die grünen. |

Ein Agent, der `compare` oder `sweep` fährt, **prüft `$?` und bricht bei `2` ab**,
statt die Tabelle zu lesen. Jeder zwanzigste Sweep-Lauf wird zur Selbstkontrolle
doppelt gefahren — genau dafür.

### Zwei stille Fehlerarten, die kein Exit-Code meldet

- **`COMPARISON REFUSED`** auf stdout: Die Ergebnismenge wurde an einem anderen
  Commit oder gegen eine andere Definitionstabelle gemessen. Der Bericht zeigt dann
  **den Grund statt einer Tabelle**. Das ist das gewünschte Verhalten, kein Defekt —
  nach einem Merge-Fenster wird neu vermessen, nicht über die Grenze hinweg verglichen.
- **`orders refused — this row is not a measurement`**: Eine Zeile im Duell- oder
  Bewegungsbericht, deren Befehle abgelehnt wurden. Nicht als Ergebnis lesen.
- **`intervened: true` in `result.json`**: In diesem Lauf hat jemand über das
  Admin-Panel eingegriffen. Er sagt, was die KI *hätte* tun können, nie was sie
  tut — er wird nicht archiviert und nicht mit einem Messlauf verglichen. Der
  eine erlaubte Vergleich ist der gegen den **eigenen eingriffsfreien Zwilling**:
  gleicher Seed, gleiche Spec, einmal mit und einmal ohne Eingriff.

---

## 2. Was maschinenlesbar herauskommt

Alles ist NDJSON oder JSON, **ausschliesslich Ganzzahlen** — kein Float verlässt die
Simulation, Positionen sind Q16.16-Rohwerte. Zwei Läufe sind damit rechnerisch
vergleichbar statt schätzungsweise.

| Datei | Eine Zeile / ein Objekt je | Für den Agenten interessant |
|---|---|---|
| `result.json` | Lauf | `outcome`, `winnerSlot`, `decidedTick`, `finalStateHash`, `definitionsHash64` |
| `hashchain.json` | *n* Ticks | erster abweichender Eintrag = Tick, ab dem sich Verhalten ändert |
| `trace.ndjson` | Metriktick | 21 Kennzahlen je Slot plus `buildingsByRole[9]` |
| `view.ndjson` | Sichtframe | Position, Tätigkeit, Ziel, Fog-Ebene, **Entity-ID** (zehnte Spalte) — für `player.html` |
| `tracks.ndjson` | Tick | Positionsspur je Einheit, verlustfrei: `a` absolut, `d` Delta, `x` beendet, `k` Keyframe |
| `events.ndjson` | Ereignis | `spawn`, `death`, `damage`, `order`, `pathGoal` (**hiess bis 2026-08-10 `goal`** — es ist die Wegzelle, nicht das Vorhaben; das Vorhaben steht in `goals.ndjson`), `moveStart/Stop`, `attackStart/Switch/Stop`, `harvest*`, `cargo*`, `site*`, `stuck`/`unstuck`, `retreat*` — mit exaktem Tick. `by` ist **hergeleitet**, `bySure` sagt wie sicher |
| `goals.ndjson` | Entscheidung je Sitz | **was die KI vorhatte, an der Stelle aufgeschrieben, wo sie es entschieden hat**: je Kadenz eine Zeile mit der Haltung der Armee (`a`, 12 Spalten) und je beurteilter Einheit ihr Goal samt der Zahlen, die die Bedingung gewogen hat (`u`, 10 Spalten). Keine Herleitung — die anderen Dateien tragen Zustand, diese trägt die **Absicht** |
| `units.json` | Einheit | `detourPercent`, `blockedTicks`, `orderChanges`, `damageTaken`, `pathLengthCells` — die Zahl hinter „kein gegenseitiges Blockieren" |
| `duels.ndjson` | Duell (576) | `winner`, `decidedTick`, `noContact`, `parityWobbles`, `survivors*` |
| `movement.ndjson` | Szenario × Fraktion (8) | `usableRangeOvershootCells` (nicht `overshootCells` — siehe unten), `blockedUnits`, `arrived`, `travelledCells`, `wallGapCells` |
| `resultset.json` | Vergleichslauf | je Kandidat Siegquote, Mittelwerte, `changes`, plus Herkunft (Commit, Seeds, Hashes) |
| `dashboard.html` | — | alle vier Laufarten in einer Seite, interaktiv — für Menschen mit Browser |
| `reports/latest.md` | Lauf | **dieselben Zahlen als Markdown**, auf GitHub direkt lesbar: Kennzahlen, Gegentabelle, Belagerung, Bewegung, Kurven als Mermaid |
| `reports/README.md` | — | Gesamtübersicht: jeder archivierte Lauf eine Zeile, dazu der Verlauf innerhalb der aktuellen Definitionstabelle |
| `reports/data/<id>.json` | Lauf | der verdichtete Messblock mit Herkunft und Fingerabdruck — **die Quelle**, aus der die Berichte jederzeit neu entstehen |
| `reports/behavior-log.md` | Verhaltensänderung | **von Hand geführt, nicht generiert**: was genau geändert wurde, was besser und was schlechter wurde, und ein Abschnitt „Widerlegt" gegen doppelte Arbeit. **Vor jeder neuen Änderung lesen.** |

Beides schreibt ein Kommando: `python3 Nova.AiLab/report/build_reports.py out`
(oder `./lab.sh`, das vorher misst).
`--regenerate` rendert die ganze Historie neu, ohne etwas nachzumessen. Ein Lauf
wird an seinem Fingerabdruck erkannt: zweimal berichten ergibt keinen zweiten
Eintrag. Ein **Agent liest `reports/latest.md`**, wenn er wissen will, wo die
Zahlen heute stehen — dort steht der letzte Lauf vollständig und ohne Browser.

**Zwei Felder, bei denen der naheliegende Name der falsche ist.** `overshootCells`
misst gegen die *nominale* Waffenreichweite — die Einheit kann sie ohne Aufklärung
gar nicht nutzen (Sicht 10, Artillerie 20). Die Zahl, die Verhaltensarbeit
zurückholen kann, ist `usableRangeOvershootCells`, gerechnet gegen die Entfernung,
auf der tatsächlich zum ersten Mal Schaden fiel. Und `unitsLost` / `lowPowerTicks`
sind **kumulativ seit Tick 0**, nicht je Intervall — wer sie als Intervallwerte
liest, macht aus einer flachen Wirtschaft eine einbrechende.

**Der Seed ist keine Achse.** Kein Simulationssystem zieht aus dem Kernel-PRNG; der
Seed geht in Zustands-Hash und Snapshot, sonst nirgendwohin. Ein Sweep über 24 Seeds
ist *eine* Beobachtung. Ein Agent, der Varianz braucht, findet sie heute nur im
Profil — `sweep` sagt das selbst hin, wenn alle Läufe gleich ausgehen.

---

## 3. Die Schleife für eine Verhaltensänderung

```bash
# 1  Referenz festhalten, BEVOR etwas geändert wird
dotnet run --project Nova.AiLab -c Release -- match --hash-every 100 --out out/ref
dotnet run --project Nova.AiLab -c Release -- duel     --out out/ref/duel
dotnet run --project Nova.AiLab -c Release -- movement --out out/ref/movement

# 2  Verhalten ändern — in AI/, AI.Data/, Combat/, Movement/, Pathfinding/, Factions/

# 3  Determinismus zuerst. Bei Exit 2 ist hier Schluss.
dotnet run --project Nova.AiLab -c Release -- match --repeat 2 --hash-every 100; echo "exit=$?"

# 4  Wirkung messen und gegen die Referenz halten
dotnet run --project Nova.AiLab -c Release -- match --hash-every 100 --out out/new
diff <(jq -r '.entries[]|"\(.tick) \(.stateHash)"' out/ref/hashchain.json) \
     <(jq -r '.entries[]|"\(.tick) \(.stateHash)"' out/new/hashchain.json) | head

# 5  Suite. 87 Labortests + die grosse Suite; die vier Baselines dürfen rot werden.
dotnet test Nova.AiLab.Tests/Nova.AiLab.Tests.csproj -c Release
dotnet test tools/Nova.SimRunner.Tests/Nova.SimRunner.Tests.csproj -c Release

# 6  Eintrag ins Verhaltensjournal — VOR der nächsten Änderung, nicht danach
$EDITOR reports/behavior-log.md
```

**Schritt 6 ist nicht Kosmetik.** `reports/behavior-log.md` trägt je Änderung die
genauen Werte, die Folgen in **beide** Richtungen und einen Abschnitt „Widerlegt".
Wer eine Idee hat, liest ihn zuerst: eine Sackgasse, die niemand aufgeschrieben
hat, wird zuverlässig ein zweites Mal gelaufen. Ein Eintrag ohne Abschnitt
„Schlechter" ist verdächtig, nicht sauber.

**Der erste abweichende Kettenglied-Tick ist die wertvollste Zahl der Schleife.** Er
sagt, *ab wann* zwei Stände auseinanderlaufen — eine rote Baseline sagt nur *dass*.

### Was ein Verhaltens-PR über sich selbst wissen muss

`compare` schreibt je Kandidat einen PR-Entwurf mit ausschliesslich Gemessenem: alte
und neue Kennzahlen, verwendete Seeds, betroffene Dateien und welche der vier
Baseline-Dateien dadurch rot wird. **Der Abschnitt „Im laufenden Spiel gesehen"
bleibt leer und ist als leer erkennbar.** Ein Agent füllt ihn nicht — er darf ihn
nur ausfüllen, wenn ein Mensch tatsächlich gespielt hat.

---

## 4. Fünf Dinge, die ein Agent hier nicht tun darf

| Verboten | Warum, und was stattdessen |
|---|---|
| Eine **Baseline** anpassen, damit CI grün wird | Der eine Fehler, gegen den die ganze Regel gebaut ist. Verhaltens-PR **ohne** Baseline, neue Baseline in einem eigenen PR mit altem Wert, neuem Wert, Begründung. Seit `e1a6a57` lehnt `check_baseline_guard.py` einen PR maschinell ab, der eine der vier Dateien **und** `Scripts/{AI,AI.Data,Core,Data,Simulation,Networking,Gameplay/Match}/` oder `Assets/_Project/Data/` zugleich anfasst. Override nur per Maintainer-Label `baseline-reset-approved`. |
| Erwarten, dass eine **KI-Änderung die Baselines rot macht** | Tut sie nicht — gemessen in V001. Die vier Dateien erwähnen `SkirmishAi` mit keiner Zeile, ihre Szenarien fahren kein KI-System. Ein Verhaltens-PR in `AI/` kann vollständig grün sein. Die Trennungsregel gilt trotzdem, der Guard führt `Scripts/AI/` in seinen Pfaden — nur die Begründung „wäre ohnehin rot" trägt hier nicht. Und: `SkirmishAiTests` prüft Ausgang und Sieger, **nicht** den Entscheidungstick; es blieb grün, während sich die Partie um 4.260 Ticks verschob. |
| Eine **Gesamtnote** bilden, sortieren, „bestes Profil" wählen | Entscheidung 11: eine einzelne Zahl belohnt zuverlässig das Falsche. Eine KI, die 5 % häufiger gewinnt, weil sie den Gegner mit Bauarbeitern zumüllt, ist keine bessere KI. Ein Test prüft, dass im Bericht weder „score" noch „rank" steht. Der Agent legt nebeneinander, ein Mensch wählt. |
| **„geprüft" / „funktioniert"** schreiben, gestützt auf einen Laborlauf | Diagnose ≠ Nachweis. Formulierung: „im Labor gemessen: …, im laufenden Spiel nicht geprüft". |
| **Fremdes Terrain** reparieren, weil das Labor dort einen Fehler findet | Befund unter `findings/` ablegen: Beobachtung, Pfad, Eigentümer, Seed + MatchSpec zur Reproduktion, `match.replay`, Fundstelle. Der Weg nach draussen ist **Mail oder Issue, kein PR**. |
| Ein **neues System registrieren** oder die Tick-Reihenfolge ändern | Determinismus hängt nicht nur daran, *was* ein System rechnet, sondern *wann*. Neue Systeme werden eingeordnet, nicht angehängt — und das Einordnen ist eine Absprache. Reaktionsverhalten gehört deshalb **in `SkirmishAiSystem`**, das zwischen `Combat` und `Victory` bereits registriert ist; damit wird `MatchRunner` gar nicht angefasst. |

---

## 5. Was die Zahlen heute sagen

Aus dem letzten vollständigen Lauf: [`20260810-1858-1d330a05`](reports/latest.md),
Commit `1d330a05`, Definitionstabelle `0xD5F219A3F68088FF`. Diese Werte sind der
Ausgangspunkt jeder Verbesserung — wer sie verschiebt, muss sagen, um wieviel.

> [!WARNING]
> **Die Historie beginnt hier faktisch neu.** Upstream hat die
> Definitionstabelle bewegt (`0x6326FA3E56CFF5A3` → `0xD5F219A3F68088FF`,
> fünf endliche Aetheriumfelder D-102 und die Bauvoraussetzungen). Die
> siebzehn älteren archivierten Läufe sind damit **nicht** mehr
> vergleichbar, und der Bericht lehnt den Vergleich von sich aus ab
> (`COMPARISON REFUSED`). Das ist gewolltes Verhalten, kein Defekt.

| Befund | Zahl | Quelle |
|---|---|---|
| Fernkämpfer laufen bis auf **0 Zellen** heran | Reichweite 20, Sicht 10, **Feuereröffnung bei 7** (Allianz; Legion 18/10/7) → nutzbarer Überlauf **7 von 7**, nicht 20 | `movement.ndjson`, `standoff`: `usableRangeOvershootCells` |
| Artillerie kann ihre Reichweite ohne Aufklärung nicht nutzen | **100 von 576** Duellen enden ohne einen einzigen Schuss; jede Artilleriezeile der Gegentabelle trägt das Kontaktzeichen | `duels.ndjson` |
| Belagerung: Explosiv liegt über dem Matrixwert, aber nicht um Faktor 12 | Legion gegen die eigene Barracks, je 6 Einheiten: `BasicInfantry` 632 Ticks für 360 AE, `AntiArmorInfantry` 52 für 1.200 AE. Reine Zeit 12,2× — **je AE aber 3,65×** gegen die 2,5× der Matrix | `duels.ndjson`, `siege: true` |
| Die Spawnreihenfolge kippt echte Paarungen | **5** Richtungsabweichungen (Spiegelpaarungen werden nicht mehr mit sich selbst verglichen) | `duels.ndjson`, beide Richtungen |
| Die KI wird **nie** abgelehnt | `intentsRejected` **0 von 461** | `trace.ndjson` |
| Enge Stellen sind kein Problem | 16 Einheiten durch eine **Zwei-Zellen**-Engstelle, 0 Blockaden, Ankunft 158/178 | `movement.ndjson`, `blocking`: `wallGapCells` |
| **`DefendHome` greift, und sparsam** | **31 Beurteilungen in 8 Entscheidungen**, ausschliesslich Legion, Tick 2.820–4.460 — und dafür nur 25 Befehle auf **eine** statische Zielzelle | `goals.ndjson` |
| Aktionen je Minute bleiben unter menschlichem Niveau | Allianz **41,4**, Legion **30,1** Intents je 1.000 Ticks (bei 10 Hz also 24,8 bzw. 18,1 Aktionen/Minute) | `trace.ndjson` |

**`intentsRejected` ist weiterhin strukturell 0**, weil die KI fünf brave
Befehlsarten benutzt. Sobald ein Punkt der Roadmap mehr Befehlsarten erzeugt
(`SetRallyPoint`, `ReturnCargo`, `Repair`), wird diese Spalte das
Frühwarnsignal dafür, dass die KI gegen Executor-Regeln anrennt — überall
sonst ist das stumm, weil `Submit()` das Verdikt absichtlich nicht auswertet.

**Und die Zeile, die man beim Lesen dieses Laufs im Kopf behalten muss:** die
Legion verliert ihn, mit 60 gegen 25 Verlusten. `DefendHome` verschiebt die
Entscheidung von 3.213 auf 6.490 Ticks, wendet die Niederlage aber nicht ab —
die einseitige Messung dazu steht in [V008](reports/behavior-log.md), und sie
ist der Grund, warum dieser Lauf eine Aufnahme ist und kein Beleg.

---

## 6. Codewissen, das jede Verhaltensänderung braucht

> [!IMPORTANT]
> **Was als nächstes gebaut wird, steht in [`ROADMAP.md`](ROADMAP.md).** Hier
> stand bis 2026-08-10 eine dritte Liste (E7–E11), die sich mit den Tabellen in
> `NEXT-STEPS.md` und `KAMPFSTAERKE.md` überschnitten hat, ohne mit ihnen
> übereinzustimmen. Sie ist in die Roadmap eingegangen. Was hier bleibt, ist das,
> was man **am Code nachgesehen** haben muss, bevor man ihn anfasst — der Teil,
> der einen Agenten davor bewahrt, eine gemessene Sackgasse ein zweites Mal zu
> laufen.

### 6.1 Erledigt: der Doku-Fix im Spielcode

Hier stand ein Auftrag, zwei veraltete Kommentare in `SkirmishAiSystem` zu
richten (angebliches „kein Auto-Acquire" trotz D-087, angeblich abgelehnter
`SetRallyPoint` am Refinery). **Am 2026-08-10 nachgeprüft: beide Stellen sind
korrekt** — `SkirmishAiSystem.cs:77` nennt D-087 ausdrücklich, und der Kommentar
bei `:365` sagt richtig, dass der Rally-Punkt akzeptiert würde und die
Micro-Entscheidung eine Verhaltenswahl ist. Nichts zu tun.

### 6.2 Die Bausteine der reaktiven KI — Stand

| Baustein | Stand |
|---|---|
| **Goal-System als Form** | **gebaut**, verhaltensneutral: vier benannte Module (`Retreat`, `Attack`, `Hold`, `Advance`) mit fester Priorität statt einer if-Kette. Entscheidungstick und Endzustand unverändert, Artefakte byte-identisch bis auf `elapsedMilliseconds`, kein `Revision`-Bump, **kein Profilfeld** — Prioritäten im Profil würden `ProfileHash` und damit `aiBehaviorId` in `result.json` bewegen und genau den Nachweis kaputt machen |
| **Score-Zielwahl** | **gebaut** (`r1`, V001): Entscheidung 33 % früher, beide Seiten verlieren weniger, aber `early-push` fällt von 50 % auf 0 % |
| **`Retreat`** je Einheit | **gebaut** (`r4`, V005), ohne Lebens-Hysterese — MS-1-Einheiten heilen nie |
| **`DefendBase`** | **gebaut und verworfen** (V002), zweiter Anlauf als ROADMAP 9 — mit Kampfpunkten als Mass für „echte Bedrohung", nicht mit anderem Radius |
| **`DefendField`** (Feind am eigenen Feld) | offen, in der Roadmap noch nicht eingeordnet |
| **`Farm`** (Credits unter Schwelle) | offen; die Bank ist heute nicht leer, sondern zu voll → ROADMAP 6 |

**Der Zustand steckt schon in der Welt.** Ein Snapshot-Block wäre eine
Inhaberentscheidung; vorher lohnt der Weg ohne. Die stehenden Befehle der eigenen
Einheiten — `TargetGridPos`, `AttackTarget`, `HarvestFieldId`, `IsReturningCargo` —
speichern, was die KI zuletzt wollte, an einer Stelle, die ohnehin serialisiert wird.
Damit ist Hysterese ohne Sidecar möglich. Die heutige KI nutzt das bereits, nur zur
Doppelbefehl-Unterdrückung.

**Zwei Fallen, im Code nachgesehen, nicht aus dem Plan zitiert:**

> **Das Score-Targeting muss eigene Einheiten selbst ausfiltern.**
> `UnitCommandStateView.ValidateDomain` hat **keinen** `case` für `AttackTarget` — der
> fällt auf `default: return CommandResultCode.Applied`. Und die Feuerphase im
> `CombatSystem` prüft Bewaffnung, Reichweite, Sichtbarkeit und Cooldown, **nicht den
> Besitzer**. Ein expliziter Angriffsbefehl auf eine eigene Einheit feuert also. Die
> Auto-Acquisition filtert streng feindlich, der Befehlsweg nicht.

> **Explizite Befehle werden nie überschrieben** („explicit orders are never
> retargeted"). Das Score-Targeting hat damit Vorrang vor der Automatik und übernimmt
> die Verantwortung, nicht schlechter zu zielen als sie. Es reicht nicht, gleich gut
> zu sein — es muss besser sein, sonst ist die Änderung ein Rückschritt.

Ganzzahlig bleiben: Nutzwerte 0..1000, Gewichte als `int`, Gleichstand nach fester
Zielreihenfolge bzw. niedrigerer Entity-Id. `NoFloatInSimulationTests` prüft mit.

**Fertig ist E7 erst mit einem Spielbericht** aus einer echten Partie, inklusive eines
Falls, in dem die Reaktion falsch war. Das hängt am Linux-Build, der Bringschuld des
Netzstrangs ist und aussteht. Bis dahin bleibt der Beobachtungsabschnitt sichtbar
leer, und der PR sagt selbst, dass er unfertig ist.

### 6.3 Die acht Befehlsarten, die die KI nicht benutzt

Die KI benutzt 5 von 13. Nach Nutzen sortiert, **mit einer Korrektur gegenüber
dem Plan** — und je Befehl höchstens ein PR, weil ein neuer Befehlstyp ohne ein
Ziel, das ihn auslöst, nur Rauschen erzeugt:

| Rang | Befehl | Begründung |
|---:|---|---|
| 1 | `SetRallyPoint` | **Sofort nutzbar.** `ValidateSetRallyPoint` prüft über `IsProducerRole` aus der Definitionstabelle, und der Harvester trägt in beiden Fraktionen `producerRole: Refinery`. Nachschub sammelt sich, statt einzeln zu sterben — und das Harvester-Micromanagement der heutigen KI verliert seinen Grund. |
| 2 | `Retreat`-Begleiter `Stop` | beendet einen Rückzug sauber, statt ihn ins nächste Gefecht laufen zu lassen |
| 3 | `ReturnCargo` | Harvester bei Gefahr mit Teilladung heimschicken — greift direkt in `DefendField` |
| 4 | `Repair` | Builder repariert Gebäude, 10 HP/Tick |
| 5 | `CancelProduction` / `CancelConstruction` / `Sell` | Notliquidität und Kurswechsel, wenn die Lage kippt |
| — | ~~`InstallDefenseModule`~~ | **Nicht anfangen.** `ValidateDomain` gibt für diesen Kind unbedingt `RejectedPrerequisitesNotMet` zurück: Verteidigungsmodule sind G2/G4-Inhalt laut `mvp-v1.json`. Der Plan führt ihn als Position 1 — der Code lehnt ihn deterministisch ab. Eine KI, die ihn benutzt, produziert nur `intentsRejected`. |

Die Zahlen 10 HP/Tick, 50 % und 75 % stimmen mit dem Code überein, sind dort aber als
„Q-040 candidate" provisorisch markiert.

### 6.4 Was Gedächtnis bräuchte — und deshalb Rückfrage ist

- **Sidecar-Block.** Timer in Ticks, Aufklärungsgedächtnis, Squad-Identität,
  Schadens*rate* statt Schadenssumme. `MatchFingerprint` führt
  `SidecarSchemaVersion` bereits — der Platz ist reserviert, nur unbelegt. Eine
  Anfrage dafür löst einen vorgesehenen Vertrag ein und ist keine
  Architekturänderung; gestellt wird sie trotzdem, nicht umgesetzt
  ([ROADMAP §4](ROADMAP.md)).
- **Goal-System.** Braucht **keinen** Sidecar, solange es zustandslos bleibt: die
  Goals werden je Kadenz abgeleitet, der Verlauf lebt in der Aufzeichnung des
  Labors. So ist es geplant → [`GOALS.md`](GOALS.md). Erst ein Goal, das sich
  etwas *merken* muss, wäre D-ID.
- **Teams.** Strukturell offen (8 Slots, 8 Team-Masken), inhaltlich blockiert:
  Freund/Feind liegt bei uns, geteilte Sicht und Niederlage je Seite beim
  Netzstrang. 4-Slot-Freiforall im Labor geht sofort — endet heute allerdings im
  Zeitlimit-Unentschieden, weil `GetEnemyStartAreaCell` bei vier Basen für
  niemanden stimmt. Reproduzierbar, aber kein Befund.

### 6.5 Nicht anfangen → [`ROADMAP.md`](ROADMAP.md) §5

Die begründete Liste steht dort, damit sie an einer Stelle steht. Zwei Punkte
gehören ins Codewissen und bleiben deshalb hier:

- **`InstallDefenseModule`** wird von `ValidateDomain` **unbedingt** abgelehnt
  (G2/G4-Inhalt laut `mvp-v1.json`). Eine KI, die ihn benutzt, produziert nur
  `intentsRejected`. Ob das **Gebäude** `DefensePlatform` platzierbar ist — es ist
  bewaffnet, 20 Schaden, 10 Reichweite —, ist dagegen **ungeprüft**.
- **`Decide()` entschlacken** (rund elf Listen je Entscheidungstick): gemessen,
  kein Problem — 143.000 Ticks/s über 24 Kerne. Es gibt keinen Grund, es
  anzufassen. Das ist auch die Kostengrenze für neue Goal-Module: eine
  Allokation je Modul und Kadenz wäre eine.
