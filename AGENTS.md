# Nova.AiLab — Handreichung für Agenten

**Stand:** Messmenge in `out/` gemessen an Commit `3b3f27d` · Definitionstabelle
`0x6326FA3E56CFF5A3` · Etappen E0–E6 erledigt, E7 offen
**Gilt zusätzlich:** `../../CLAUDE.md` (Arbeitsvertrag), `../../AGENTS.md`, `README.md` nebenan,
Plan: [`docs/feature-ideas/AiSimulationEnvironment.md`](../../docs/feature-ideas/AiSimulationEnvironment.md)

Dieses Dokument beantwortet zwei Fragen: **wie ein Agent das Labor für automatische
Evaluierung benutzt**, und **welche Schritte als nächstes das KI-Verhalten verbessern.**

---

## 0. Vier Sätze, bevor irgendetwas läuft

1. **Werkzeug, kein Beitrag.** Alles unter `Nova.AiLab` und `out/` lebt auf
   `lab/ai-simulation` und gerät in keinen `feat/`-Branch. `feat/`-Branches werden
   frisch von `upstream/main` abgezweigt, nie aus `lab/` gecherry-pickt.
2. **Ein grüner Laborlauf ist Diagnose, kein Nachweis.** Was nicht im laufenden Spiel
   gesehen wurde, steht als ungesehen im PR-Text. Kein generierter Satz darf ein
   Laborergebnis so formulieren, als sei es gespielt worden.
3. **Verhalten und Baseline nie im selben PR.** Seit `e1a6a57` erzwingt das eine CI
   (`.github/workflows/baseline-guard.yml`), nicht mehr nur Disziplin — siehe §4.
4. **Gepusht wird nur in den Fork, nie auf `main`.** Commit, Push und PR sind drei
   getrennte Freigaben; keine gilt für den nächsten Schritt mit.

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
| Woran liegt es **bei dieser einen Einheit**? | derselbe Lauf | `player.html`: Einheit anklicken → Laufroute, Ereignisband, Detailfeld. Roh in `tracks.ndjson`, `events.ndjson`, `units.json` |

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
| `events.ndjson` | Ereignis | `spawn`, `death`, `damage`, `order`, `goal`, `moveStart/Stop`, `attackStart/Switch/Stop`, `harvest*`, `cargo*`, `site*`, `stuck`/`unstuck`, `retreat*` — mit exaktem Tick. `by` ist **hergeleitet**, `bySure` sagt wie sicher |
| `units.json` | Einheit | `detourPercent`, `blockedTicks`, `orderChanges`, `damageTaken`, `pathLengthCells` — die Zahl hinter „kein gegenseitiges Blockieren" |
| `duels.ndjson` | Duell (576) | `winner`, `decidedTick`, `noContact`, `parityWobbles`, `survivors*` |
| `movement.ndjson` | Szenario × Fraktion (8) | `usableRangeOvershootCells` (nicht `overshootCells` — siehe unten), `blockedUnits`, `arrived`, `travelledCells`, `wallGapCells` |
| `resultset.json` | Vergleichslauf | je Kandidat Siegquote, Mittelwerte, `changes`, plus Herkunft (Commit, Seeds, Hashes) |
| `dashboard.html` | — | alle vier Laufarten in einer Seite, interaktiv — für Menschen mit Browser |
| `reports/latest.md` | Lauf | **dieselben Zahlen als Markdown**, auf GitHub direkt lesbar: Kennzahlen, Gegentabelle, Belagerung, Bewegung, Kurven als Mermaid |
| `reports/README.md` | — | Gesamtübersicht: jeder archivierte Lauf eine Zeile, dazu der Verlauf innerhalb der aktuellen Definitionstabelle |
| `reports/data/<id>.json` | Lauf | der verdichtete Messblock mit Herkunft und Fingerabdruck — **die Quelle**, aus der die Berichte jederzeit neu entstehen |
| `reports/behavior-log.md` | Verhaltensänderung | **von Hand geführt, nicht generiert**: was genau geändert wurde, was besser und was schlechter wurde, und ein Abschnitt „Widerlegt" gegen doppelte Arbeit. **Vor jeder neuen Änderung lesen.** |

Beides schreibt ein Kommando: `python3 Nova.AiLab/report/build_reports.py
Nova.AiLab/out` (oder `./lab.sh`, das vorher misst).
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

Aus dem letzten vollständigen Lauf (`out/`, Commit `3b3f27d`). Diese Werte sind
der Ausgangspunkt jeder Verbesserung — wer sie verschiebt, muss sagen, um wieviel.

| Befund | Zahl | Quelle |
|---|---|---|
| Fernkämpfer laufen bis auf **0 Zellen** heran | Reichweite 20, Sicht 10, **Feuereröffnung bei 7** (Allianz; Legion 18/10/7) → nutzbarer Überlauf **7**, nicht 20 | `movement.ndjson`, `standoff`: `usableRangeOvershootCells` |
| Artillerie kann ihre Reichweite ohne Aufklärung nicht nutzen | 4 von 36 Siegen (Allianz), 2 von 36 (Legion); alle 100 kontaktlosen Duelle liegen auf Waffenreichweite | `duels.ndjson` |
| Belagerung: Explosiv liegt über dem Matrixwert, aber nicht um Faktor 12 | Legion gegen die eigene Barracks, je 6 Einheiten: `BasicInfantry` 632 Ticks für 360 AE, `AntiArmorInfantry` 52 für 1.200 AE. Reine Zeit 12,2× — **je AE aber 3,65×** gegen die 2,5× der Matrix | `duels.ndjson`, `siege: true` |
| Die Spawnreihenfolge kippt echte Paarungen | **5** Richtungsabweichungen (Spiegelpaarungen werden nicht mehr mit sich selbst verglichen) | `duels.ndjson`, beide Richtungen |
| Die KI wird **nie** abgelehnt | `intentsRejected` 0 von 1021 | `trace.ndjson` |
| Enge Stellen sind kein Problem | 16 Einheiten durch eine **Zwei-Zellen**-Engstelle, 0 Blockaden, Ankunft 158/178 | `movement.ndjson`, `blocking`: `wallGapCells` |

Die vorletzte Zeile ist die interessanteste für einen Agenten: **`intentsRejected` ist
heute strukturell 0**, weil die KI nur fünf brave Befehlsarten benutzt. Sobald E7/E8
mehr Befehle erzeugt, wird diese Spalte das Frühwarnsignal dafür, dass die KI gegen
Executor-Regeln anrennt — überall sonst ist das stumm, weil `Submit()` das Verdikt
absichtlich nicht auswertet.

---

## 6. Nächste Schritte für besseres KI-Verhalten

Reihenfolge ist nicht beliebig: **E7 vor E8**, weil ein neuer Befehlstyp ohne Ziel,
das ihn auslöst, nur Rauschen erzeugt.

### 6.1 Zuerst: der Doku-Fix, der nichts kostet

`SkirmishAiSystem` beruft sich in seiner Klassendoku auf GB-002 („kein Auto-Acquire"),
während `CombatSystem` unter D-087 Auto-Acquisition bereits implementiert. **Der Code
gilt**, die Doku ist veraltet — ebenso die zweite Stelle (`SkirmishAiSystem.cs:63`),
die behauptet, `SetRallyPoint` akzeptiere das Refinery nicht. Ein winziger PR in
`AI/`, verhaltensneutral, Baselines bleiben grün. Guter erster Beitrag für einen
Agenten, weil er den ganzen PR-Weg einmal durchläuft, ohne etwas zu riskieren.

### 6.2 E7 — Reaktive KI, Stufe 1 *(verhaltensändernd)*

> **Score-Targeting ist erledigt** — siehe [`reports/behavior-log.md`](reports/behavior-log.md)
> V001. Entscheidung 33 % früher, beide Seiten verlieren weniger, aber
> `early-push` fällt von 50 % auf 0 %. Vier von fünf Bausteinen stehen noch aus.

Fünf Bausteine, jeder mit einer Zahl, an der er gemessen wird:

| Baustein | Was er tut | Messgrösse vorher → nachher |
|---|---|---|
| **Score-Targeting** | ersetzt „HQ vor Gebäude vor Einheit" durch `DamageMatrix`-gewichtete Zielwahl | Verluste je Slot, Entscheidungstick, `armyHealthSum` |
| **`DefendBase`** | sichtbarer Feind nahe HQ → Armee heim | Ticks zwischen Feindsichtung am HQ und Ankunft der Armee |
| **`DefendField`** | Feind am eigenen Aetherium-Feld → Teilarmee hin, Harvester weichen aus | `harvesters`, `idleHarvesters`, `fieldReserveAE` |
| **`Retreat`** | *pro Einheit* unter Lebensschwelle, kein globales Ziel | `unitsLost` gegen `healthLost` |
| **`Farm`** | Credits unter Schwelle → Harvester bauen, Untätige aufs Feld | `credits`-Kurve, `idleHarvesters` |

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

### 6.3 E8 — fehlende Befehlsarten, je einer pro PR

Die KI benutzt 5 von 13 Befehlsarten. Nach Nutzen sortiert, **mit einer Korrektur
gegenüber dem Plan**:

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

### 6.4 Danach: E9 bis E11

- **E9 — Sidecar-Vorschlag, kein Code.** Aus den E7/E8-Auswertungen belegen, wo
  Zustandslosigkeit nachweislich schadet: Timer in Ticks, Aufklärungsgedächtnis,
  Squad-Identität, Schadens*rate* statt Schadenssumme. `MatchFingerprint` führt
  `SidecarSchemaVersion` bereits — der Platz ist reserviert, nur unbelegt. Die Anfrage
  wäre das Einlösen eines vorgesehenen Vertrags, keine Architekturänderung.
- **E10 — Goal-System mit Zustand.** Erst nach D-ID.
- **E11 — Teams.** Strukturell offen (8 Slots, 8 Team-Masken), inhaltlich blockiert:
  Freund/Feind liegt bei uns, geteilte Sicht und Niederlage je Seite beim Netzstrang.
  4-Slot-Freiforall im Labor geht sofort — endet heute allerdings im
  Zeitlimit-Unentschieden, weil `GetEnemyStartAreaCell` bei vier Basen für niemanden
  stimmt. Reproduzierbar, aber kein Befund.

### 6.5 Was nicht auf der Liste steht, und warum

- **Kartenvarianz** erst nach E7 — sonst tunt man gegen die gebrochene
  `GetEnemyStartAreaCell`-Annahme statt gegen das Verhalten.
- **Neue Legion-Waffenwerte** (Issue 01): Die Duell-Arena *misst* sie, die *Umsetzung*
  hängt an `Simulation/Definitions/` — geteilte Vertragsfläche, Absprache nötig.
  Blockiert, nicht vergessen.
- **Ein automatischer Optimierer.** Nicht vertagt, sondern nicht vorgesehen: Verfahren,
  die einen Skalar brauchen, sind mit Entscheidung 11 aus dem Plan.
- **`Decide()` entschlacken** (rund elf Listen je Entscheidungstick): gemessen, kein
  Problem — 143.000 Ticks/s über 24 Kerne. Es gibt keinen Grund, es anzufassen.
