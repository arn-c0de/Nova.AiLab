# Goals — was die KI vorhat, sichtbar und übersteuerbar

**Notiert am:** 2026-08-10 · **Status: Teile 1 und 3 gebaut** (Goal-System,
Panel lesend, Live-Modus mit Eingriff), **Teil 2 (Flanke) offen**, und die
**Vorausschau** aus §6.1 ebenfalls · **Stand:** KI-Verhalten `r7.E34435F9`,
Commit `5635009` ·
**Vorher lesen:** [`ROADMAP.md`](ROADMAP.md) §2 (Punkte 2, 8b, L3),
[`reports/behavior-log.md`](reports/behavior-log.md) V002–V007,
[`AGENTS.md`](AGENTS.md) §3–§4

> [!IMPORTANT]
> **Was davon steht, und wo der Bau vom Plan abweicht.** Der Plan bleibt
> unverändert stehen — er ist der Grund, aus dem gebaut wurde, und nach dem Bau
> kann man ihn nicht mehr rekonstruieren. Was daraus geworden ist:
>
> | Geplant | Stand |
> |---|---|
> | §2 Goal-System, verhaltensneutral | **gebaut.** `GoalKind` in `AI.Data/`, vier Module in `SkirmishAiSystem`. Entscheidungstick **3213** und Endzustand **`0xE002DD893916967B`** unverändert, Artefakte byte-identisch bis auf `elapsedMilliseconds` |
> | §2 „Priorität aus dem Profil, 0 = Modul aus" | **anders gebaut, und der Unterschied trägt den Nachweis.** Ein angehängtes Profilfeld bewegt `ProfileHash` und damit `aiBehaviorId` in jedem `result.json` — der Schritt wäre dann nicht byte-identisch gewesen. Die Reihenfolge steht fest im Code; die Aus-Stellung bringt jedes Modul mit, das eine **Regel** bekommt |
> | §3 `goals.ndjson`, delta-kodiert | **gebaut, nicht delta-kodiert.** Eine Zeile je Sitz und Kadenz: die kanonische Partie ergibt rund 2.000 Zeilen, und die Zahlen neben dem Goal ändern sich ohnehin in jeder Zeile — nur die Goal-Spalte liesse sich überhaupt verdichten |
> | §6.1 Panel, lesend | **gebaut, im vorhandenen Player** statt als zweite Karte: Goal, seit wann, welches davor, die Zahlen der Bedingung, die Kipppunkte. Die als „derived" markierte Nachrechnung der Wellenschwelle ist damit **weg**, wo eine Aufzeichnung vorliegt |
> | §6.1 Vorausschau | **offen.** Der Weg dahin ist inzwischen billiger als geplant: kein Snapshot-Fork nötig, sondern erneutes Abspielen von Tick 0 mit demselben Eingriffsprotokoll — deterministisch, also exakt, und bei 5.800 Ticks/s interaktiv |
> | §6.2 Endpunkte in `report/gui_server.py` | **anders gebaut.** Die Partie ist C#; halten kann sie nur der Prozess, dem der Kernel gehört. `live --port` bringt seinen eigenen Server mit (nur Standardbibliothek, nur `127.0.0.1`), `gui_server.py` behält seine Aufgabe |
> | §6.3 Goal-Maske als Eingabe, §6.4 `intervened` | **gebaut.** `IAiGoalOverride` als Konstruktor-Argument, `overrides.ndjson`, `intervened: true`. **Die Bedingung aus §8 ist geprüft:** Sitzung plus Protokoll erneut gefahren ergibt bitgleich dasselbe — mit Gegenprobe, dass ein Eingriff die Partie überhaupt verändert |
> | §5 Flanke | **offen**, unverändert. Braucht **L2** |

Dieses Dokument plant drei Dinge, die zusammengehören:

1. **Ein modulares Goal-System** in der KI — benannte Ziele mit Priorität und
   Aus-Stellung, statt einer Kette von if-Zweigen. Zuerst **ohne**
   Verhaltensänderung.
2. **Formationsangriff / Flanke** als erstes Goal, das es heute nicht gibt — und
   nur dann, wenn die KI aus ihrer eigenen Sicht ausrechnet, dass es die
   Siegchance verbessert.
3. **Ein Admin-Panel im Labor**, das je Sitz und je Einheit zeigt, was sie
   vorhat, was sie vorhatte, was sie als nächstes tut — und das den Eingriff
   erlaubt, ohne die Messbarkeit zu zerstören.

---

## 1 · Was heute existiert, und was nicht

**Es gibt keinen Goal-Begriff.** `SkirmishAiSystem` ist eine reine Funktion des
committeten Zustands. Zwei Zwischenwerte tragen die ganze Kampfentscheidung:

| Typ | Inhalt | Lebensdauer |
|---|---|---|
| `ArmyPosture` | `Engages`, `TargetRaw`, Marschzelle, Sammelzelle, `WaveReady` | eine Kadenz, abgeleitet, nicht gespeichert |
| `UnitAssignment` | `EntityRaw`, `AttackTargetRaw`, Marschzelle | eine Kadenz |

Daraus folgen vier Dinge, die dieser Plan nicht wegdefinieren darf:

- **Kein Gedächtnis.** Das einzige, was die KI über die Kadenz hinweg „weiss",
  ist der **stehende Befehl der Einheit** (`TargetGridPos`, `AttackTarget`,
  `IsMoving`, `HarvestFieldId`, `IsReturningCargo`) — und den benutzt sie zur
  Unterdrückung doppelter Befehle. Ein Sidecar-Block wäre Inhaberentscheidung.
- **Keine Wahrscheinlichkeiten.** Kein `System.Random`, und der Sim-RNG wird von
  keinem System gezogen. Zwei Läufe derselben Spec sind bitgleich. Was es gibt,
  sind **ganzzahlige Scores** (`TargetDamageWeight` und die drei anderen): eine
  Rangfolge von Kandidaten, keine Verteilung.
- **Keine Blickrichtung.** In `Simulation/` existiert kein Facing, keine
  Orientierung, keine Panzerungsseite. Was „Flanke" hier heissen kann, ist
  dadurch eng begrenzt — §5.
- **Ein Namenskonflikt, der auffliegt, sobald das Panel steht.** Das
  Ereignis `goal` in `events.ndjson` ist heute die **Wegzelle**
  (`GoalGridPos`), nicht das Vorhaben. Das Panel nennt das Neue `aiGoal`, und
  das alte Ereignis wird laborseitig auf `pathGoal` umbenannt. Zwei
  verschiedene Dinge unter einem Wort sind im Panel ein Anzeigefehler, den
  niemand als solchen erkennt.

**Was es dagegen schon gibt und was dieser Plan benutzt:** die Kampfpunkte aus
`AI/CombatStrength.cs` (`r6`), das Wellentor als eigene testbare Funktion
(`AI/WaveStrengthGate.cs`), die Sichtframes und die Positionsspur je Tick im
Labor, und einen Player, in dem eine Einheit schon anklickbar ist.

## 2 · Das Goal-System

**Ein Goal ist ein Name für eine Bedingung und ihre Wirkung.** Zwei reine
Funktionen, kein Feld:

```
bool           Trifft zu?   (Einheit, Haltung, Profil, Sichtlisten) → bool
UnitAssignment Wirkung      (Einheit, Haltung, Profil)              → Befehl oder Schweigen
int            Priorität    aus dem Profil, 0 = Modul aus
```

Je Kadenz und Einheit wird **ein** Goal gewählt: das zutreffende mit der
höchsten Priorität, bei Gleichstand in fester Modulreihenfolge. Das ist genau,
was die heutige if-Kette tut — nur benannt, einzeln testbar und einzeln
abschaltbar.

> [!NOTE]
> **Der Hysterese-Ersatz steckt schon in `IsRetreating` und ist beim
> Modularisieren leicht zu verlieren:** eine Einheit, die **bereits zur
> Sammelzelle läuft**, gilt weiter als auf Rückzug, auch wenn gerade keine
> Bedrohung in Reichweite ist. Der stehende Befehl ist das Gedächtnis — genau die
> Stelle, an der eine „saubere" reine Bedingung das Verhalten kaputt macht,
> ohne dass ein Test es merkt.

### 2.1 Der Katalog

`GoalKind` liegt als Enum in `AI.Data/`, damit Sim, Labor und Panel dieselben
Namen benutzen. **Ein Enum-Wert ist kein Zustand** — er wird je Kadenz neu
ausgerechnet und nirgends gespeichert.

| Goal | Trifft zu, wenn | Wirkung | Stand |
|---|---|---|---|
| `Rueckzug` | Leben unter `RetreatHealthPercent`, Wellen an — **und** entweder eine Bedrohung innerhalb `RetreatDangerCells` **oder** sie läuft schon zur Sammelzelle | Marsch zur **Sammelzelle** (nicht zur Basis), Ziel = nächster Verfolger | **echt seit `r4`** |
| `Angreifen` | die Welle marschiert, oder die Einheit ist schon draussen | Marsch zur Zielzelle, Ziel = Armeeziel | **echt seit `r1`/`r3`** |
| `Sammeln` | am Sammelpunkt **angekommen** und stehend | **kein Befehl** — absichtlich, siehe unten | **echt seit `r3`** |
| `Aufmarschieren` | sonst, als Nachschub auf dem Weg | Marsch zum Sammelpunkt, **kein** Angriffsziel (F001) | **echt seit `r3`** |
| `Ernten` · `Bauen` · `Produzieren` | Wirtschaftsschritte in `Decide()` | Feldbefehl, Platzierung, Warteschlange | **echt, seit es die KI gibt** |
| `BasisVerteidigen` | Kampfpunkte sichtbarer Feinde am eigenen HQ über `defendHomeThreatPoints` | Sammeln abbrechen, Marsch heim | geplant, [VERTEIDIGUNG.md](VERTEIDIGUNG.md) · Roadmap 1 und 9 |
| `Nachschub` | draussen steht eine intakte Welle | sofort losmarschieren statt warten | geplant, [KAMPFSTAERKE §6](KAMPFSTAERKE.md) · Roadmap 3 |
| `Flankieren` | zwei Anmarschwege sind billiger als einer | halbe Welle auf den zweiten Weg | geplant, §5 · Roadmap 8b |
| `Abstandhalten` · `Aufklaeren` | Fernkämpfer über der Feuerdistanz; Sicht kleiner als Waffenreichweite | halten statt weiterlaufen; ein Späher voraus | geplant, Roadmap 10 |

> [!IMPORTANT]
> **`Sammeln` wirkt durch Schweigen, und das ist die empfindlichste Stelle des
> ganzen Systems.** Eine stehende Einheit, die je Kadenz denselben Befehl
> bekommt, treibt die Aktionen je Minute von 23 auf 40, ohne dass sich etwas
> ändert — das ist der Fehlermodus, an dem `DefendBase` gescheitert ist
> (V002). Ein Goal, dessen Wirkung „nichts sagen" ist, darf deshalb nicht als
> Sonderfall wirken, sondern muss ein gleichberechtigtes Modul sein. Die erste
> Zahl bei jeder Änderung hier: **Intents je 1.000 Ticks.**

### 2.2 Eingeführt wird es verhaltensneutral

Die Module drücken **exakt** die heutigen Bedingungen aus, in der heutigen
Reihenfolge. Der Nachweis ist keine Testzusicherung, sondern eine Zahl:
Entscheidungstick und Endzustands-Hash bleiben gleich, die Artefakte sind
byte-identisch bis auf `elapsedMilliseconds`. Kein `Revision`-Bump, kein
Journaleintrag nötig.

Das ist derselbe Zug, der den Umbau auf Absichten je Einheit auswertbar gemacht
hat: **die Form ist billig, die Regel ist teuer.** Wer zuerst eine Regel und
dabei die Form ändert, kann hinterher nicht sagen, welche der beiden gewirkt
hat.

### 2.3 Determinismusregeln, die jedes Modul einhält

- ganzzahlig, kein `float`/`double`, auch nicht in einer Zwischenrechnung
- keine Wanduhr, kein `Time.deltaTime`, kein `System.Random`
- Scans aufsteigend, Gleichstand über die **niedrigere rohe Entity-Id**
- keine Abhängigkeit von der Iterationsreihenfolge eines `Dictionary`/`HashSet`
- kein neues System, keine Änderung der Tick-Reihenfolge — alles bleibt in
  `SkirmishAiSystem`

## 3 · Warum es kein Gedächtnis bekommt

Ein „vorheriges Goal" im Sinne eines gespeicherten Feldes wäre Zustand neben der
Welt, also Sidecar, also Inhaberentscheidung — und es wäre ausserdem unnötig:

> **Der Verlauf lebt in der Aufzeichnung des Labors, nicht in der Simulation.**

Das Labor schreibt ohnehin je Tick mit. Ein `goals.ndjson` (je Kadenz je Einheit
das gewählte Goal, delta-kodiert wie `tracks.ndjson`) liefert den Verlauf
vollständig, und die Sim bleibt eine reine Funktion. Damit ist das Panel ohne
D-ID-Entscheidung baubar.

| Was das Panel zeigt | Woher es kommt | Was es ist |
|---|---|---|
| **aktuelles Goal** | im Tick berechnet | exakt |
| **vorheriges Goal** und der Tick des Wechsels | `goals.ndjson`, Aufzeichnung | exakt |
| **nächstes Goal** | Vorausschau: Fork vom Snapshot, N Ticks rechnen | **exakt**, keine Schätzung — die KI ist deterministisch |
| **Neigung** der Zielwahl | die vier Score-Gewichte je Kandidat, als Anteil | eine **Rangfolge**, ausdrücklich keine Wahrscheinlichkeit |
| **Kipppunkte** | Differenz zur nächsten Schwelle, in der Einheit der Bedingung | exakt, weil jede Bedingung ein ganzzahliger Vergleich ist |

Beispiele für Kipppunkte, wie sie im Panel stehen sollen — sie sagen mehr über
das Nächste als jede Prozentzahl:

```
noch 140 Punkte, bis die Welle marschiert        (WaveStrengthGate.Threshold − gatheredStrength)
ab 33 Leben dreht sie ab                          (RetreatHealthPercent × MaxHealth / 100)
noch 3 Zellen, bis sie als „am Sammelpunkt" gilt  (StagingToleranceCells − Abstand)
noch 1 freier Kopf, dann bindet die Punktregel    (TargetArmySize − lebend − in Warteschlange)
```

Die Vorausschau ist bezahlbar: eine ganze Partie über 3.520 Ticks rechnet in
604 ms, also rund 5.800 Ticks je Sekunde. 200 Ticks Vorausschau kosten etwa
35 ms — interaktiv, auch auf dem Telefon.

## 4 · Was das Goal-System nicht ist

- **Kein Verhaltensbaum, keine Zustandsmaschine.** Beides bringt Zustand mit.
  Hier wählt eine Prioritätsliste über reinen Funktionen.
- **Keine Zwischenschicht.** Die Module sind Methoden derselben Klasse, keine
  Interfaces mit Registrierung. Elf Listen je Entscheidungstick sind gemessen
  und kein Problem (143.000 Ticks/s über 24 Kerne) — eine Allokation je Modul
  und Kadenz wäre eins.
- **Keine Gesamtnote.** Ein Goal hat eine Priorität, keine Güte. Es gibt keine
  skalare Bewertung, aus der man ein Verhalten ableiten könnte (Entscheidung 11).

## 5 · Flankieren — und was es hier heissen kann

**Was es nicht heissen kann.** Die Simulation kennt keine Blickrichtung: kein
Rückenschaden, keine Panzerungsseite, kein Umfassungsbonus. Wer Flanke als
Schadensbonus plant, plant gegen den Code.

**Was übrig bleibt, und es ist nicht wenig — drei Wirkungen:**

| Wirkung | Warum sie echt ist | Belegt? |
|---|---|---|
| **Weniger Zeit im Feuer** | Der Umweg führt aus der Reichweite der gesehenen Feindgruppe heraus. Dieselbe Rechnung wie „Umweg gegen gerade Linie" ([NEXT-STEPS §2](NEXT-STEPS.md)) | messbar mit **L2**, heute nicht gemessen |
| **Aufgeteiltes gegnerisches Feuer** | Die D-087-Auto-Acquisition nimmt das **nächste** Ziel. Zwei Anmarschrichtungen können die Feuerkonzentration teilen | **Behauptung.** Genau das prüft L2, und wenn es nicht trägt, fällt der halbe Nutzen weg |
| **Ein Sekundärziel fällt nebenbei** | Die zweite Hälfte läuft an Harvestern und Refinery vorbei, die weich, wichtig und abseits stehen | hängt an Roadmap 4 (zweites lohnendes Ziel) |

### 5.1 Die Bedingung, ganzzahlig

Geteilt wird nur, wenn die KI aus ihrer eigenen Sicht rechnet, dass es besser
ist — „aus ihrer Perspektive" heisst hier: **ausschliesslich aus der committed
Team-Sicht**, ohne Blick in fremde Karten.

```
Kosten(Weg)    = Σ  Waffenschaden je Tick der sichtbaren Feinde,
                    in deren Reichweite der Weg verläuft
                  × Ticks, die die Gruppe darin steht
Gewinn(Flanke) = Kosten(ein Weg) − Kosten(Weg A) − Kosten(Weg B)

Flankieren, wenn   Gewinn(Flanke) > FlankMinGainPoints
                   und  S(Hälfte A) >= S_ziel(Feindgruppe an A)
                   und  S(Hälfte B) >= S_ziel(Feindgruppe an B)
                   und  S(Welle)    >= FlankMinGroupPoints
```

Die zweite und dritte Zeile sind die Sicherung gegen den bekannten Fehlermodus:
**eine geteilte Welle sind zwei kleinere Wellen** — also das Förderband, das
`r3` beseitigt hat. Geteilt wird nur, wenn **jede** Hälfte für sich stark genug
ist, gemessen an der Feindgruppe an ihrem eigenen Anmarschpunkt (§5a in
[KAMPFSTAERKE.md](KAMPFSTAERKE.md) liefert `S_ziel`).

Geteilt wird **nach Punkten, nicht nach Köpfen**: absteigend nach Kampfwert
verteilen, bei Gleichstand die niedrigere rohe Entity-Id — dann sind beide
Hälften ähnlich stark und die Zuordnung ist reproduzierbar.

### 5.2 Aus-Stellung und Startwerte

| Wert | Aus | Startwert zum Messen | Bedeutung |
|---|---:|---:|---|
| `flankMinGroupPoints` | `0` | `1200` | unter dieser Wellenstärke wird nie geteilt |
| `flankMinGainPoints` | — | `200` | Mindestersparnis, damit sich der Umweg lohnt |
| `flankMaxDetourCells` | `0` | `24` | Obergrenze des Umwegs, gegen absurde Bögen |

`flankMinGroupPoints: 0` schaltet das Modul ab und ergibt bitgenau das heutige
Verhalten. Ohne diese Stellung ist die Regel im Selbstspiel nicht messbar (M001).

### 5.3 Woran es scheitern darf

- **Das Austauschverhältnis fällt.** Dann hat das Teilen mehr gekostet als der
  Umweg gebracht hat, und die Flanke ist widerlegt — nicht die Rechnung, die
  Idee.
- **L2 zeigt kein aufgeteiltes Feuer.** Dann bleibt nur „weniger Zeit im Feuer",
  und das leistet Roadmap 8a (eine Route statt der Luftlinie) schon allein,
  billiger und ohne Teilung.
- **Intents je 1.000 Ticks steigen.** Zwei Marschzellen statt einer heisst
  doppelt so viele Befehlsgruppen. Steigt die Zahl ohne besseres Spiel, ist es
  V002 in neuer Form.

## 6 · Das Admin-Panel

**Ort:** das Labor, nicht das Spiel. Es wird nicht per PR eingereicht.

### 6.1 Was es zeigt

Eine aufklappbare Karte **neben** der Karte, in derselben Machart wie die
Anzeigetafel je Sitz — `player.html` bleibt eine einzelne kopierbare Datei ohne
Build und ohne Netzzugriff, die Oberflächensprache kommt aus
`report/uikit/`.

| Bereich | Inhalt |
|---|---|
| **Sitz** | Auswahl über die Anzeigetafel: Haltung der Armee, Wellenstärke gegen Schwelle, Kipppunkt bis zum Losmarschieren, Ziel und dessen Score-Neigung |
| **Einheit** | Auswahl per Klick auf die Karte (Mehrfachauswahl über Rahmen): Rolle, Besitzer, Leben, Kampfwert — dazu **aktuelles Goal**, die Bedingung, die es ausgelöst hat, **mit den Zahlen**, die eingegangen sind |
| **Verlauf** | vorheriges Goal und der Tick des Wechsels, aus `goals.ndjson` |
| **Vorausschau** | was sie in den nächsten N Ticks tatsächlich tut — exakt, per Fork vom Snapshot |
| **Neigung** | Zielkandidaten mit Punkten und Anteil, beschriftet als *Neigung*. Das Wort „Wahrscheinlichkeit" kommt im Panel nicht vor |
| **Kipppunkte** | die Differenz zur nächsten Schwelle, in der Einheit der Bedingung |
| **Eingriff** | Goal setzen für Sitz, Auswahl oder einzelne Einheit; Gültigkeit „bis Widerruf" oder N Ticks; freigeben |

### 6.2 Der Live-Modus

Heute ist `player.html` eine **Aufzeichnung**: pausieren und zurückspulen geht,
eingreifen nicht. Der Eingriff braucht einen Laufmodus, der die Partie hält:

```
dotnet run --project Nova.AiLab -c Release -- live --port 8787
```

`report/gui_server.py` bleibt, was es ist — nur Standardbibliothek, nur an
`127.0.0.1` — und bekommt die Endpunkte dazu:

| Endpunkt | Wirkung |
|---|---|
| `GET /live/state?tick=` | Sichtframe, Goals, Kipppunkte |
| `POST /live/pause` · `/live/step?ticks=` · `/live/speed` | Zeitkontrolle |
| `POST /live/lookahead?ticks=` | Fork vom Snapshot, rechnen, Ergebnis zurück, Fork verwerfen |
| `POST /live/override` · `/live/release` | Goal erzwingen bzw. freigeben |

### 6.3 Wie ein Eingriff wirkt, ohne die KI zustandsbehaftet zu machen

Zwei Wege, und der Unterschied ist nicht kosmetisch:

| Weg | Wie | Warum er nicht reicht / reicht |
|---|---|---|
| **Befehl in den Ingress**, wie ein Spieler | Das Labor reicht einen Marsch- oder Angriffsbefehl ein | Übersteuert den **Befehl**, nicht das **Vorhaben**: in der nächsten Kadenz überschreibt die KI ihn wieder. Als Notlösung brauchbar, als Panel-Funktion irreführend |
| **Goal-Maske als Eingabe** | Der Host übergibt vor der Entscheidung „für diese Einheiten gilt Goal X". Die KI liest sie wie das Profil | **Der richtige Weg.** Der Eingriff ist Teil der *Eingabe*, nicht des Zustands. Die KI bleibt eine reine Funktion, es entsteht kein Sidecar, und der Lauf bleibt reproduzierbar |

Die Maske ist der einzige Teil dieses Plans, der **Spielcode** berührt:
ein optionaler Parameter in `AI/`, den der ausgelieferte Pfad
(`MatchRunner`) nie füllt. Leere Maske = heutiges Verhalten, byte-identisch
nachweisbar. `Gameplay/Match/` wird nicht angefasst.

> [!IMPORTANT]
> **Jeder Eingriff wird aufgezeichnet, sonst ist der Lauf wertlos.**
> `overrides.ndjson` hält Tick und Nutzlast jedes Eingriffs. Lauf plus
> Eingriffsprotokoll erneut gefahren muss bitgleich dasselbe ergeben — das ist
> die Bedingung, unter der das Panel überhaupt gebaut werden darf, und der
> erste Test, der dazugehört.

### 6.4 Ein Lauf mit Eingriff ist kein Messlauf

- `result.json` trägt `intervened: true`.
- Der Lauf wird **nicht** unter `reports/` archiviert. Eine Historie, in der
  jemand mitgespielt hat, vergleicht Unvergleichbares — dieselbe Regel, aus der
  `COMPARISON REFUSED` schon existiert.
- Erlaubt und interessant ist der Vergleich gegen den **eigenen
  eingriffsfreien Zwilling**: derselbe Seed, dieselbe Spec, einmal mit und
  einmal ohne Eingriff. Das ist die Frage „was wäre passiert, wenn sie geflankt
  hätte" — und die einzige, die das Panel messbar beantwortet.
- Ein Eingriff ist **kein Nachweis für Verhalten**. Er zeigt, was die KI *hätte*
  tun können, nicht was sie tut.

### 6.5 Reihenfolge, in der das Panel entsteht

| Schritt | Was | Braucht |
|---|---|---|
| 1 | `goals.ndjson` und die Goal-Anzeige im vorhandenen Player — lesend, auf Aufzeichnungen | Roadmap 2 (echte Goals) |
| 2 | Kipppunkte und Score-Neigung im Detailfeld | Schritt 1 |
| 3 | Vorausschau per Fork vom Snapshot | Schritt 1 |
| 4 | Live-Modus: laufende Partie, Pause, Einzeltick | — |
| 5 | Goal-Maske, Eingriff, `overrides.ndjson`, Zwillingsvergleich | Schritte 1–4 |

Schritt 1 bis 3 arbeiten auf **fertigen** Läufen und sind damit sofort
nützlich — die Ansicht lässt sich auf alte Läufe neu legen, ohne nachzumessen
(`player --out out`). Erst Schritt 4 braucht einen neuen Betriebsmodus.

## 7 · Scope

| Was | Wo | Wessen |
|---|---|---|
| Goal-Module, `GoalKind`, Prioritäten, Flanke | `AI/`, `AI.Data/` | **uns** — PR-Strang mit Aus-Stellung je Modul |
| Goal-Maske als optionale Eingabe | `AI/` | **uns**; der ausgelieferte Pfad füllt sie nie |
| Panel, Live-Modus, `goals.ndjson`, `overrides.ndjson`, L2-Szenario | `Nova.AiLab/` | **Labor**, kein PR ins Spiel |
| `MatchRunner`, `Gameplay/UI/`, `Gameplay/Input/` | — | **fremdes Terrain**, wird nicht angefasst |
| Tick-Reihenfolge, `ICommandTransport`, Match-Fingerprint | — | benutzt, nie geändert |
| Die vier Baseline-Dateien | — | unangetastet |

## 8 · Woran der ganze Plan scheitern darf

| Teil | Gilt als widerlegt, wenn |
|---|---|
| **Goal-System** | Die verhaltensneutrale Fassung ist **nicht** byte-identisch. Dann drückt der Katalog nicht das aus, was die KI heute tut — und die Namen wären eine Erfindung, keine Beschreibung |
| **Flanke** | Austauschverhältnis fällt, oder L2 zeigt kein aufgeteiltes Feuer |
| **Panel, lesend** | Es zeigt etwas, das im Lauf nicht passiert ist. Ein Anzeigefehler in einem Diagnosewerkzeug ist schlimmer als eine fehlende Anzeige |
| **Panel, Eingriff** | Lauf plus `overrides.ndjson` ergibt nicht bitgleich dasselbe. Dann ist der Eingriff nicht Eingabe, sondern versteckter Zustand |

Und die Regel über allen: **Ein grüner Laborlauf ist Diagnose, kein Nachweis.**
Ein Panel, in dem alles richtig aussieht, ist erst recht keiner.
