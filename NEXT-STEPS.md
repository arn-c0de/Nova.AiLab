# Nächste Schritte am KI-Verhalten — sortiert danach, was ein Spieler merkt

**Stand:** KI-Verhalten **`r4.779A1B5B`** · Referenzpartie Tick **9.164**,
Endzustand **`0x8054A759F73E1F81`**, gemessen auf `feat/ai-waves-and-retreat`
über dem neuen `upstream/main` (Journal V006 — die älteren Zahlen stammen von
vor dem Merge-Fenster und sind nicht mit diesen vergleichbar) ·
Messgrundlage: [`reports/latest.md`](reports/latest.md) ·
Historie: [`reports/behavior-log.md`](reports/behavior-log.md) ·
Für die gespielte Partie: [`PLAYTEST-CHECKLIST.md`](PLAYTEST-CHECKLIST.md)

> [!NOTE]
> **Die nächsten fünf PRs sind ausgeplant:** [`KAMPFSTAERKE.md`](KAMPFSTAERKE.md)
> — Nachschub hinter der laufenden Welle, Abbruch bei gebrochener Welle, Ausbau
> bis zur Fahrzeugfabrik, und darunter **eine** ganzzahlige Kampfwertformel, an
> der später der Schwierigkeitsgrad als Zahlensatz hängt. Enthält die Fortsetzung
> der Wellenregel, die der Spieler in [Journal B002](reports/behavior-log.md)
> selbst benannt hat.

> [!NOTE]
> **Punkt 1 und Punkt 3 sind gebaut** (Journal V004 und V005), Punkt 5 ist
> **gestrichen** (Befund F002: `SetRallyPoint` ist die Spawn-Zelle, kein
> Sammelbefehl). Die Beschreibungen unten sind der Stand **vor** diesen
> Änderungen und bleiben als Begründung stehen; was daraus wurde, steht in der
> PR-Tabelle am Ende und im Journal. Gespielt ist nach wie vor nur `r2` —
> **alles seither ist ungesehen.**

Diese Liste ist **nicht** nach Aufwand oder nach Laborkennzahl sortiert, sondern
danach, was in einer Partie *Mensch gegen KI* auffällt. Eine Verbesserung, die
nur eine Zahl in `latest.md` verschiebt, gehört ans Ende; eine, die man in der
ersten Minute sieht, an den Anfang.

> [!IMPORTANT]
> Jeder Punkt hier ist **verhaltensändernd**. Das heisst jedes Mal: Referenz
> vorher sichern, Determinismus zuerst prüfen (Exit 2 = sofort stoppen),
> `AiBehaviorId.Revision` bumpen, Journaleintrag mit **besser und schlechter**
> schreiben. Der Ablauf steht in [`AGENTS.md`](AGENTS.md) §3.

---

## 0 · Eine Ursache, eine Bauform — vor allen sechs Punkten

Die sechs Punkte unten sind sechs Beobachtungen, aber nicht sechs Ursachen.
Schritt (6) in `SkirmishAiSystem.cs` hat genau **eine** Form: *ein* Ziel und
*eine* Marschzelle für die **ganze** Armee, das Ganze hinter einem Gate auf
`AttackSquadThreshold`. Daraus folgen fünf der sechs Symptome fast mechanisch.

| Symptom | Folgt aus |
|---|---|
| 1 · Armee tröpfelt | jede neue Einheit bekommt sofort denselben globalen Marschbefehl |
| 2 · immer dieselbe Linie | *eine* Zielzelle aus `GetEnemyStartAreaCell` |
| 3 · kein Rückzug | ein globales Ziel kann „diese eine Einheit dreht ab" nicht ausdrücken |
| 4 · kein Zielen unter sechs Einheiten | der ganze Block hängt am Schwellen-Gate |
| — · `DefendBase` (V002) | globaler Zielwechsel je Kadenz → Pendeln, +23 % Intent-Rauschen |

Wer die Punkte einzeln in diese Form hineinbaut, baut jedes Mal einen
Sonderfall an ein Konstrukt, das den Fall nicht ausdrücken kann. Deshalb steht
vor Punkt 1 eine **Formänderung ohne Verhaltensänderung**.

### Die Bauform: Absicht je Einheit statt Befehl für die Armee

Schritt (6) wird zu einer reinen Funktion in drei Stufen — weiterhin
zustandslos, weiterhin **in `SkirmishAiSystem`**: kein neues System, keine
Änderung der Tick-Reihenfolge (`AGENTS.md` §4), `MatchRunner` bleibt unberührt.

```
ArmyPosture posture = ResolvePosture(...);          // abgeleitet, nicht gespeichert
foreach (Einheit u)  UnitIntent i = ResolveUnitIntent(u, posture);
GroupAndSubmit(intents);                            // gleiche Befehle → ein Intent
```

`UnitIntent` ist ein kleiner Struct (`Kind: Attack | MoveTo | Hold`,
`targetRaw`, `cellX/cellY`). Das Gruppieren ist keine Kosmetik, sondern die
Absicherung gegen den Fehlermodus, an dem V002 gescheitert ist: **die Intents
je 1.000 Ticks dürfen nicht steigen.** Diese Zahl ist bei jedem der Schritte
unten die erste, die man anschaut — sie hat `DefendBase` gekippt, bevor die
Siegquoten überhaupt etwas sagten.

### Hysterese ohne Sidecar

Ein Sidecar-Block wäre Inhaberentscheidung und ist damit gesperrt. Er wird
nicht gebraucht: **der stehende Befehl der Einheit ist das Gedächtnis**, und er
wird ohnehin serialisiert (`TargetGridPos`, `AttackTarget`, `IsMoving`,
`CurrentHealth`, `HarvestFieldId`, `IsReturningCargo`).

- Eine Einheit **ist auf Rückzug**, wenn `TargetGridPos` die Sammel- bzw.
  Basiszelle ist **und** `CurrentHealth < exitHealthPercent`. Eintritt bei
  25 %, Austritt erst bei 60 % — echte Hysterese, kein Timer, kein neues Feld.
- Eine Einheit **wartet**, wenn ihre Zelle innerhalb `stagingToleranceCells`
  um den Sammelpunkt liegt.
- Eine **Welle ist unterwegs**, wenn mindestens eine eigene Kampfeinheit näher
  am Ziel steht als der Sammelpunkt. Abgeleitet, nicht gemerkt.

Save/Restore reproduziert das von selbst, weil nichts davon neben der Welt
liegt. Ganzzahlig, aufsteigende Scans, Gleichstand über die niedrigere rohe
Entity-Id — die Regeln aus §4 des Arbeitsvertrags bleiben unangetastet.

### Neue Profilwerte

Alle `int`, alle in `AiProfile`: `waveSize`, `stagingDistanceCells`,
`stagingToleranceCells`, `retreatEnterHealthPercent`,
`retreatExitHealthPercent`, `engageRangeSlackCells`.

Ein angehängtes Feld ändert `AiBehaviorId.ProfileHash` und damit den
angezeigten Bezeichner — das ist der Zweck des Hashes, kein Nebeneffekt.
`AiProfileTests` bekommt seine Zusicherungen im selben PR (keine der vier
Baseline-Dateien). `AiProfile.SchemaVersion` bleibt bei 1: Felder anhängen ist
keine Bedeutungsänderung.

### Drei Fallen, im Code nachgesehen

> **Ein expliziter Angriffsbefehl ausserhalb der Reichweite macht die Einheit
> passiv.** `CombatSystem` Phase 2 überspringt jede Einheit mit gültigem
> `AttackTarget`, Phase 3 *hält* einen Befehl, dessen Ziel ausser Reichweite
> oder unsichtbar ist. Wer also Punkt 4 baut („zielen ab der ersten Einheit"),
> muss explizite Ziele **auf Waffenreichweite + `engageRangeSlackCells`
> begrenzen** — sonst steht die Einheit da und feuert nicht, wo die
> Auto-Acquisition geschossen hätte. Das wäre ein Rückschritt, kein Fortschritt.

> **Der Rally-Punkt ist lesbar.** `ProductionSystem.TryGetProducer(raw, out
> entryCount, out rallyXRaw, out rallyYRaw)` gibt ihn heraus — die
> Doppelbefehl-Unterdrückung für Punkt 5 ist also zustandslos machbar. Beim
> allerersten Mal existiert die Producer-Zeile eventuell noch nicht;
> `SetRallyPoint` legt sie an, danach ist der Wert lesbar. Es entsteht kein
> dauerhaftes Rauschen. `ValidateSetRallyPoint` verlangt eigenes,
> **fertiggestelltes** Produktionsgebäude und ein Ziel auf der Karte.

> **Eigene Einheiten filtert nur die Zielwahl.** `ValidateDomain` hat für
> `AttackTarget` keinen Case, die Feuerphase prüft Reichweite und Sicht, nie
> den Besitzer. Der Filter in `FindBestVisibleEnemyByScore` ist die einzige
> Stelle, die das verhindert — er muss in jede neue Zielwahl mitwandern.

### Was zuerst passiert: PR 0, verhaltensneutral

Schritt (6) bekommt die neue Form **mit exakt den heutigen Regeln**. Der
Nachweis, dass die Umstellung sauber war, ist kein Test, sondern eine Zahl:
Entscheidungstick **8.715** und Endzustand **`0x5D8FB2D45FFD16B6`** bleiben
gleich, die Artefakte sind byte-identisch bis auf `elapsedMilliseconds`. Kein
`Revision`-Bump, kein Journaleintrag nötig — und danach ist jeder Schritt
unten klein genug, um einzeln gemessen zu werden. Genau das hat V002
überhaupt erst auswertbar gemacht.

---

## 1 · Die Armee tröpfelt einzeln in den Tod

**Was der Spieler sieht.** Kein Angriff, sondern ein Förderband: Soldat läuft
los, stirbt, nächster Soldat läuft los, stirbt. Man kann sich mit drei Einheiten
an den Weg stellen und die halbe Partie lang einen nach dem anderen abräumen.

**Warum.** Schritt (6) schickt in *jeder* Entscheidung **alle** Kampfeinheiten
zum selben Zielpunkt. Eine gerade fertiggestellte Einheit steht an der Kaserne,
bekommt 20 Ticks später denselben Marschbefehl und läuft allein über die halbe
Karte hinterher. Es gibt keinen Sammelpunkt und keine Wellen — ein Griff nach
`Regroup`, `Staging` oder `Rally` findet im ganzen System **nichts**.

**Messbar im Labor.** Verluste 70/97 bei nur 8.715 Ticks. Die Verlustkurve im
Bericht steigt in gleichmässigen kleinen Stufen statt in wenigen Sprüngen —
genau die Signatur des Nachtröpfelns statt einer Schlacht.

**Was zu bauen ist.** Ein Sammelpunkt zwischen eigener Basis und Ziel; neue
Einheiten laufen dorthin und **warten**, bis die Wellengrösse steht. Erst dann
marschiert die Welle geschlossen. Zwei neue Profilwerte (`waveSize`,
`stagingDistanceCells`), die Logik in `SkirmishAiSystem`.

**Wenn es wirkt:** dieselbe Zahl toter Einheiten, aber in weniger, grösseren
Sprüngen — und weniger Verluste je zerstörtem Gegner. Am Sichtfenster
nachprüfbar, nicht nur an der Tabelle.

**Scope:** `AI/`, `AI.Data/` — beides uns. Keine Rückfrage nötig.

---

## 2 · Sie rennen immer aufs Headquarter, immer die gerade Linie

> **Gespielt beobachtet** ([Journal B001](reports/behavior-log.md)): *„Rennen
> immer auf Headquarter … laufen einfach straight line, anstatt diese zu
> umlaufen, solange Leben sparen sich mehr rentiert als der Umweg, den sie
> nehmen müssten."*

**Was der Spieler sieht.** Nach zwei Partien weiss man, wohin die KI läuft und
auf welcher Linie. Man stellt sich hin und räumt sie ab.

**Warum — zwei Ursachen, die zufällig dasselbe Ziel ergeben.**

1. **Der HQ-Kurzschluss.** Ist das feindliche HQ sichtbar, bricht die Zielwahl
   sofort ab und liefert es zurück; die Marschzelle wird die HQ-Zelle. Das war
   eine bewusste Entscheidung aus V001 — „eine Siegbedingung ist keine
   Vorliebe" — und sie ist als Regel richtig und als Verhalten falsch.
2. **`GetEnemyStartAreaCell`** liefert das **entfernteste Aetherium-Feld**,
   wenn gar nichts sichtbar ist. Das liegt neben der Basis. Beide Wege zeigen
   also auf denselben Punkt.

Dazu: Die Annahme „beim entferntesten Feld steht die Feindbasis" ist bei vier
Slots ohnehin falsch — die 4-Slot-Partie endet deshalb im
Zeitlimit-Unentschieden.

**Was zu bauen ist — in dieser Reihenfolge:**

1. **Den Kurzschluss durch ein Gewicht ersetzen.** Hoch genug, dass ein
   freiliegendes HQ gewinnt; nicht so hoch, dass ein *verteidigtes* alles andere
   überstimmt. Ein Profilwert, `targetHqWeight`, und der `return` fällt weg.
2. **Ein zweites lohnendes Ziel zulassen.** Harvester und Refinery sind weich,
   wichtig und stehen abseits. Das Score-Targeting kann sie längst bewerten —
   es kommt nur nie dazu, weil Punkt 1 vorher abbricht.
3. **Die Annäherung nach Kosten wählen.** Die Regel steht schon in der
   Beobachtung und ist ganzzahlig rechenbar:

   > **Umweg nehmen, solange der Umweg billiger ist als die Verluste auf der
   > geraden Linie.**

   Konkret: erwarteter Schaden entlang der Luftlinie — Summe des Waffenschadens
   sichtbarer Feinde, in deren Reichweite die Linie verläuft, mal der Ticks, die
   man darin steht — gegen die Mehrkosten des Umwegs in Zellen. Kein Zufall,
   kein Gedächtnis, nur committed State. `Simulation/Pathfinding/` gehört uns
   seit v1.1.0, Flow-Field und `CostField` inbegriffen.

**Wenn es wirkt:** Der Spieler kann sich nicht mehr an eine Stelle stellen. Das
merkt man sofort und in keiner Kennzahl.

**Scope:** `AI/` uns, `Pathfinding/` uns (13–15). Die `IsWalkable`-Semantik
selbst wird **nicht** angefasst — das ist die Auflage aus v1.1.0.

---

## 3 · Wer die KI angreift, merkt nichts davon

**Was der Spieler sieht.** Man schiesst ihre Harvester ab, und sie erntet
weiter. Man beschiesst ihre Basis, und die Armee marschiert unbeirrt weiter
vorwärts. Angeschlagene Einheiten kämpfen bis zum letzten Lebenspunkt.

**Warum.** Es gibt keinen Rückzug und keine Verteidigung. `Retreat` existiert
nirgends im System, und `DefendBase` wurde gebaut, gemessen und **verworfen**
(→ Journal V002): Es kostete auf jeder Achse, weil die Armee zwischen Front und
Basis pendelte.

**Was zu bauen ist:**

- **`Retreat` zuerst, und zwar als Filter über Einheiten, nicht als Ziel.** Eine
  Einheit unter der Lebensschwelle bekommt einen Move-Intent Richtung Basis. Als
  globales Ziel würde sich die ganze Armee wegen eines angeschlagenen Spähers
  zurückziehen. Zwei Profilwerte mit Hysterese (`enterHealthPercent: 25`,
  `exitHealthPercent: 60`), damit dieselbe Einheit nicht im Wechseltakt kehrt
  macht.
- **`DefendBase` erst danach, und nur mit Hysterese.** V002 hat gezeigt: ohne
  Vorsprungsregel beim Zielwechsel ist ein zweiter Anlauf sinnlos. Am Radius
  liegt es nicht — 8, 16 und 24 wurden gemessen.

**Wenn es wirkt:** Angeschlagene Einheiten drehen ab, statt zu verpuffen. Das
ist die sichtbarste einzelne Verhaltensänderung dieser ganzen Liste.

**Scope:** `AI/`, `AI.Data/` — uns.

---

## 4 · Unter sechs Einheiten zielt die KI überhaupt nicht

**Was der Spieler sieht.** Früh im Spiel schiessen ihre Soldaten auf den
nächstbesten Gegenstand — auch auf einen unbewaffneten Harvester, während ein
Panzer daneben steht.

**Warum.** Gemessen beim Schreiben des Zielverhaltenstests: Unterhalb von
`AttackSquadThreshold` reicht die KI **keinen einzigen** `AttackTarget` ein. Was
die Einheiten tragen, ist reine D-087-Auto-Acquisition, und die nimmt das
*nächste* Ziel, nicht das gefährlichste. Vier von fünf schossen auf den
Harvester; in dem Tick, in dem die sechste Einheit fertig wurde, sprangen alle
fünf auf den Panzer.

**Gebaut, viermal gemessen, zurückgenommen — Journal V003.** „Die Schwelle
regelt nur noch den Vormarsch" klang nach einem kleinen Eingriff und kostet auf
jeder Achse: die beste von vier Fassungen liegt 11 % später und bei 10 % mehr
Verlusten je Slot als gar kein Zielen. Die Ursache ist strukturell und liegt
nicht in der Zielformel:

> **Ein Angriffsbefehl ist unumkehrbar.** `AttackTarget` wird nur vom Befehl,
> von der Auto-Acquisition **in ein leeres Feld** und vom Tod des Ziels
> geschrieben — `Stop()` löscht es nicht. Wer einer *stehenden* Einheit ein
> Ziel gibt, nimmt sie dauerhaft aus der Automatik; läuft das Ziel aus der
> Reichweite, hält sie den Befehl und feuert nicht mehr. Oberhalb der Schwelle
> ist derselbe Befehl richtig, weil die Einheit auf ihr Ziel **zuläuft**.

**Was es bräuchte, bevor das hier wieder aufgemacht wird.** Einen Weg, ein Ziel
freizugeben. Der liegt in `Simulation/State/` und ist Inhaberentscheidung —
Befund [`findings/F001-stop-loescht-attacktarget-nicht.md`](findings/F001-stop-loescht-attacktarget-nicht.md).
Ohne den ist jede weitere Fassung dieselbe Sackgasse mit anderen Zahlen.

**Scope:** `AI/` — uns. Der Blocker liegt ausserhalb.

---

## 5 · Nachschub sammelt sich nicht

**Was der Spieler sieht.** Einheiten verlassen die Kaserne einzeln in
Richtung Front — dasselbe Bild wie Punkt 1, nur an der Quelle.

**Warum.** Die KI benutzt `SetRallyPoint` nicht. Der Grund dafür stand bis
`768da5c` als Kommentar im Code und war **falsch**: `ValidateSetRallyPoint`
prüft über `IsProducerRole` aus der Definitionstabelle, und beide Fraktionen
tragen am Harvester `producerRole: Refinery` seit D-077. Der Befehl ist sofort
nutzbar.

**Was zu bauen ist.** Rally-Punkt der Kaserne auf den Sammelpunkt aus Punkt 1
setzen. Das ist derselbe Punkt — die beiden Änderungen gehören zusammen und
sollten in dieser Reihenfolge kommen: erst der Sammelpunkt, dann der Rally-Punkt
darauf. Nebeneffekt: Das Harvester-Micromanagement kann entfallen.

**Scope:** `AI/` — uns.

---

## 6 · Abstand — die KI kennt weder ihre eigene Reichweite noch deine

> **Gespielt beobachtet** ([Journal B001](reports/behavior-log.md)): *„Halten
> keinen Abstand zu meinen Fernkampfangreifern."*

Das sind **zwei** Fehler, die gern für einen gehalten werden:

| | eigene Reichweite | Reichweite des Gegners |
|---|---|---|
| Was passiert | Artillerie läuft bis auf Abstand 0 heran | Die Armee läuft ohne zu zögern in die Reichweite gegnerischer Fernkämpfer |
| Gemessen | ja — nutzbarer Überlauf **7** von 7 | **nein**, es gibt kein Szenario dafür |
| Gehört zu | Issue 03, `Movement/` | Punkt 2, die Annäherung nach Kosten |

Das Labor kann die zweite Spalte heute nicht messen. **Das fehlende Szenario:**
eine Gruppe läuft auf einen *stehenden Fernkämpfer* zu, gemessen wird der
Schaden, den sie auf dem Weg frisst, gegen den Schaden bei einem Umweg. Das ist
ein `movement`-Szenario, kein Duell — und es ist die Vorarbeit, ohne die Punkt 2
nur behauptet statt belegt werden kann.

**Was der Spieler bei der eigenen Reichweite sieht.** Artillerie mit 20 Zellen
Reichweite läuft auf Tuchfühlung heran und stirbt an Infanterie.

**Warum.** Gemessen: nomineller Überlauf 20 (Allianz) und 18 (Legion), nutzbarer
Überlauf **7** — die Einheiten rücken über die Entfernung hinaus vor, auf der sie
zum ersten Mal getroffen haben. Dazu: In 100 von 576 Duellen fällt kein einziger
Schuss, weil die Waffenreichweite über der Sichtweite liegt (20 gegen 10).

**Was zu bauen ist.** Zwei getrennte Dinge, die gern verwechselt werden:

- **Abstandhalten** (Issue 03) in `Simulation/Movement/` — der rote Balken im
  Standoff-Diagramm, und nur der.
- **Aufklärung**, damit die Reichweite überhaupt nutzbar wird. Ein Kontrolllauf
  hat gezeigt: eine Gruppe, die auf voller Reichweite stehenbleibt, richtet über
  2.000 Ticks **null** Schaden an. „Auf Reichweite stehenbleiben" allein ist also
  keine Verbesserung, sondern eine wirkungslose Waffe.

**Scope:** `Movement/`, `Combat/`, `AI/` — uns. Neue Legion-Waffenwerte wären
`Simulation/Definitions/` und damit **Absprache**, nicht Umsetzung.

---

## 7 · Spielgefühl — und warum es mit dem heutigen Messaufbau durchfällt

> **These:** Die KI ist nicht zu schwach, sie ist **träge**. Sie tut dasselbe,
> egal was der Spieler tut. Eine KI, die schlechter spielt, aber sichtbar
> reagiert, fühlt sich besser an als eine, die effizient überrollt.

Das ist keine Geschmacksfrage, sondern hat eine Messfolge — und die erklärt
womöglich zwei der drei bisherigen Rückweisungen.

### Der strukturelle Fehler im Messaufbau

**Jede Messung bisher lief in einer Partie, in der sich BEIDE Seiten geändert
haben.** V002 und V003 wurden an `ms1-canonical` gegen sich selbst beurteilt.
Eine Verhaltensregel steckt im Binary, also bekommen beide KIs sie
gleichzeitig. Zwei Armeen, die beide besser zielen oder beide heimlaufen,
liefern eine **längere und blutigere** Partie — und genau das wurde gemessen
und als „schlechter" gelesen.

`compare` behebt das nicht von allein: Kandidaten unterscheiden sich
ausschliesslich in **Zahlen**, und eine Coderegel erreicht dort ebenfalls beide
Seiten.

> **In symmetrischem Selbstspiel sieht eine beidseitige Verbesserung wie ein
> Rückschritt aus.** „Später entschieden, mehr Verluste" heisst dort nicht
> „schlechtere KI", sondern kann schlicht „zwei stärkere Armeen" heissen.
> Welches von beidem zutrifft, kann der heutige Aufbau **nicht unterscheiden**.

### Die Abhilfe, und sie ist billig

**Jedes neue Verhalten bekommt einen Profilwert mit einer Aus-Stellung.** Dann
spielt dasselbe Binary „mit" gegen „ohne", einseitig, in einem `compare`-Lauf:

| Verhalten | Aus-Stellung |
|---|---|
| Wellen | `waveSize: 1` — jede Einheit ist ihre eigene Welle, also heutiges Verhalten |
| Rückzug | `retreatEnterHealthPercent: 0` — greift nie |
| Verteidigung | `defenseRadiusCells: 0` |
| HQ-Gewicht | ein Wert so hoch, dass er jeden Score überstimmt = heutiger Kurzschluss |

Kosten: ein `int` und ein `if`. Gewinn: Die Messung beantwortet die Frage, die
gestellt wurde.

> **Das ist ein Eigenbefund, kein Ratschlag an andere.** V002 hatte
> `defenseRadiusCells` bereits als Feld — und verglich trotzdem zwei komplette
> Messmengen vor/nach dem Codeumbau, statt in einem Lauf `16` gegen `0` spielen
> zu lassen. Die einseitige Messung war einen Profilwert entfernt und wurde
> nicht gemacht.

### Kennzahlen, die Spielgefühl abbilden

Die heutigen Spalten — Entscheidungstick, Verluste, Siegquote — messen Stärke
und Tempo. Keine davon misst, ob eine Partie sich gut anfühlt. Vier, die es
tun, alle ganzzahlig und alle aus vorhandenen Artefakten ableitbar:

| Kennzahl | Was sie einfängt | Woraus |
|---|---|---|
| **Austauschverhältnis** | eigene gegen gegnerische Verluste — erst bei **einseitiger** Messung aussagekräftig | `unitsLost` je Slot |
| **Gefechtsdichte** | Rhythmus: wenige grosse Zusammenstösse gegen Dauertröpfeln. Zahl der Metrikintervalle mit Verlusten, und der grösste Sprung | `unitsLost`-Differenz je Intervall |
| **Reaktionslatenz** | Lebendigkeit: Ticks zwischen „eigene Einheit nimmt Schaden" und „ein Befehl ändert sich" | `healthLost` gegen `intentsSubmitted` |
| **Wiederspielwert** | verschiedene Ausgänge über *n* Seeds — **heute exakt 1** | `sweep` |

Die Gefechtsdichte ist die interessanteste: Punkt 1 (Wellen) wird den
Entscheidungstick **erhöhen** — die Armee wartet ja — und trotzdem eine
Verbesserung sein. Ohne diese Spalte fällt der PR durch, aus demselben Grund
wie V002.

### Fünf Hebel, bewertet

| Hebel | Warum es sich anfühlt | Bewertung |
|---|---|---|
| **Rhythmus durch Wellen** | Eine Welle, die sich sammelt, **kündigt sich an**. Aufbau, Angriff, Ruhe. Telegrafieren ist ein Feature, kein Fehler | Grösster Effekt je Aufwand, und die Bauform aus §0 steht bereits |
| **Sichtbare Ursache und Wirkung** | „Ich schiesse Harvester, vier Einheiten drehen ab" — auch wenn es taktisch falsch ist | Rückzug ist die erste sichtbare Reaktion überhaupt. **Achtung F001:** `Stop()` löscht `AttackTarget` nicht, eine abdrehende Einheit trägt ihr Ziel weiter |
| **Die richtigen Fehler** | Rückzug etwas zu spät wirkt menschlich; ewig in Artillerie laufen wirkt kaputt | Nicht auf „optimal" tunen, sondern die Spalte „wirkt kaputt" leeren |
| **Wiederspielwert über den Sim-RNG** | Die zweite Partie ist heute **exakt dieselbe**. Der Seed tut nichts, weil kein System zieht | §4 verbietet `System.Random`, **nicht** den Sim-RNG: er liegt im Kernel, geht in Zustands-Hash und Snapshot, ist lockstep-sicher. Zöge die KI daraus (Angriffszeitpunkt, Gleichstand, Anmarschseite), wäre jede Partie anders und trotzdem bitgenau reproduzierbar — und die Seed-Achse des Labors würde echt. **Vorschlag, keine Umsetzung:** es koppelt KI-Verhalten an jedes künftige System, das ebenfalls zieht |
| **Sichtbar nicht schummeln** | Die KI liest nur die committed Team-Sicht — der Spieler sieht das nie | Ein einzelner Späher kommuniziert Fairness besser als jede Doku. Billig, und `Scout` steht ohnehin im Plan |

### Gegenprobe: was ausserhalb dieses Projekts als gute Praxis gilt

Die fünf Hebel oben sind aus unseren eigenen Beobachtungen entstanden. Ein
Abgleich mit der Literatur bestätigt vier davon, benennt einen Fehlermodus, den
wir schon getroffen haben, und liefert eine Kennzahl, die wir übersehen hatten.

| These aus der Literatur | Was sie für uns heisst |
|---|---|
| **Lesbarkeit schlägt Komplexität.** Halos KI gilt nicht als die klügste, sondern als die **lesbarste**: Man lernt ihre Regeln in einer Stunde und findet zehn Jahre lang Randfälle ([GDKeys](https://gdkeys.com/ai-keys-to-believable-enemies/)) | Deckt sich mit der These oben. Es rechtfertigt ausserdem, Wellen bewusst **sichtbar** zu sammeln, statt sie zu verstecken |
| **Reaktionen müssen kontextangemessen sein** — nicht auf Belangloses überreagieren, nicht auf echte Bedrohung unterreagieren ([Game AI Pro, „You had me at AAAAHHH"](https://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter11_You_had_me_at_AAAAHHH_On_the_importance_of_reactions_in_game_AI.pdf)) | **Das ist V002, benannt.** `DefendBase` holte die Armee heim, sobald *irgendein* bewaffneter Feind den Radius berührte — Überreaktion auf Belangloses. Nicht die Idee war falsch, sondern die Schwelle. Ein zweiter Anlauf braucht ein Mass für „echte Bedrohung", nicht nur Hysterese |
| **Überzeugende Fehler sind schwerer zu bauen als perfektes Spiel** ([Game Developer](https://www.gamedeveloper.com/game-platforms/bonus-feature-intelligent-mistakes-key-to-believable-ai)) | Stützt „die richtigen Fehler". Beim Tunen nicht auf optimal zielen |
| **Der Spieler füllt die Lücken selbst** und schreibt der KI Absicht zu, solange die Illusion trägt ([GDKeys](https://gdkeys.com/ai-keys-to-believable-enemies/)) | Deshalb ist der Späher (Hebel 5) mehr wert als seine Kampfkraft: Er *zeigt*, dass die KI sucht |
| **Übermenschliche APM lesen sich als unfair**; Entwickler deckeln sie deshalb ([arXiv 2503.15514](https://arxiv.org/pdf/2503.15514)) | Gegengerechnet am aktuellen Stand `r2`: 343 und 363 Intents über 8.700 Ticks sind bei 10 Hz **23,7 und 25,0 Aktionen pro Minute** — weit unter menschlichem RTS-Niveau. Unser Problem ist also nicht zu viel Aktion. Die Churn-Zahl aus V002 (dort 26,6 / 30,8 APM) misst **Zappeln**, nicht Überlegenheit, und darf nicht als „die KI handelt zu viel" gelesen werden |
| **Ressourcenboni sind die übliche Abkürzung** — und Spieler erwarten auf Normal eine KI ohne Boni ([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/Main/NotPlayingFairWithResources)) | Unsere KI nimmt diese Abkürzung **nicht**: gleicher Befehlspfad, gleiche Startmittel, nur die committed Team-Sicht. Das ist ein Aktivposten — er muss nur sichtbar werden |
| **Staging und gruppenweises Vorgehen** sind in der RTS-Bot-Literatur eine anerkannte Technik ([CEUR Vol-1196](https://ceur-ws.org/Vol-1196/cosecivi14_submission_24.pdf)) | Bestätigt Punkt 1, ohne dass wir eine Eigenentwicklung rechtfertigen müssten |

**Eine Kennzahl kommt dazu:** *Aktionen pro Minute* als Lesart der
Intent-Zahl. `intentsSubmitted / Ticks × 600` — dieselbe Spalte, andere
Frage: nicht „zappelt die KI", sondern „handelt sie überhaupt in
menschlichem Rahmen". Heute **23,7 / 25,0**. Ein Umbau, der diese Zahl in
dreistellige Bereiche treibt, ist unabhängig von jeder anderen Messung ein
Problem — und umgekehrt ist reichlich Luft nach oben: An zu wenig Handeln
scheitert die KI heute nicht, an zu wenig *Reaktion*.

Zwei Themen aus der Literatur übernehmen wir **nicht**: adaptives Lernen
gegen den Spielstil und Gegnermodellierung. Beides braucht Gedächtnis über
Partien hinweg — das ist jenseits von Stufe 1, jenseits der Zustandslosigkeit
und läge im Sidecar-Bereich (Inhaberentscheidung).

### Was auch dann unmessbar bleibt

Ob eine Welle sich *angekündigt* anfühlt, ob ein Rückzug *lebendig* wirkt, ob
der Gegner *fair* erscheint — dafür gibt es keine Kennzahl, und es soll auch
keine geben (Entscheidung 11). Das entscheidet die gespielte Partie, und die
drei Fragen dafür stehen unten.

---

## Reihenfolge der PRs — je einer, je eine Verhaltensänderung

Die Reihenfolge oben sagt, was ein Spieler zuerst merkt. Diese hier sagt, in
welcher Folge man es baut, ohne zweimal dasselbe anzufassen: Zielen vor
Marschieren, Sammelpunkt vor Rally-Punkt, Rückzug erst, wenn es einen Ort
gibt, an den man sich zurückzieht.

| # | Was | Punkt | Ort | Stand |
|---:|---|---|---|---|
| 0 | Form: Absicht je Einheit, **verhaltensneutral** | §0 | `AI/` | ✅ **gebaut**, byte-identisch nachgewiesen |
| ~~1~~ | ~~Zielen unabhängig von der Schwelle~~ | 4 | — | **zurückgenommen**, Journal V003; blockiert von Befund F001 |
| 2 | Sammelpunkt und Wellengrösse | 1 | `AI/`, `AI.Data/` | ✅ **gebaut** (`r3`, Journal V004). Einseitig: Verluste 41 statt 175, Intervalle mit Verlusten 11 statt 64. `waveSize: 12` = ganze Armee, `1` schaltet ab |
| ~~3~~ | ~~Rally-Punkt der Kaserne auf den Sammelpunkt~~ | 5 | — | **gestrichen**, Befund [F002](findings/F002-rallypoint-ist-die-spawnzelle.md): der Rally-Punkt ist die **Spawn-Zelle**, das wäre Teleportation. Die Absicht erfüllt seit `r3` die Wellenregel |
| 4 | `Retreat` als Einheitenfilter | 3 | `AI/`, `AI.Data/` | ✅ **gebaut** (`r4`, Journal V005) — **ohne** Lebens-Hysterese: MS-1-Einheiten heilen nie. Einseitig: Verluste 35 statt 62, Austausch 123 statt 93 |
| 5 | Zweites lohnendes Ziel (Harvester, Refinery) | 2 | `AI/` | offen — jetzt der nächste Punkt |
| 6 | Annäherung über eine Route statt der Luftlinie | 2 | `AI/`, `Pathfinding/` | offen |
| 7 | Abstandhalten **plus** Aufklärung | 6 | `Movement/`, `AI/` | offen |
| neu | `DefendBase`, zweiter Anlauf | 3 | `AI/` | **wichtiger geworden**: seit `r4` greift die KI erst mit voller Armee an, ein früher Konter trifft eine wartende Armee. Jetzt mit Aus-Stellung und einseitig messen |
| 8–13 | Kampfwert, Nachschub-Doktrin, Fahrzeuge, Stärkeziel, Schwierigkeitsgrade | 1, 7 | `AI/`, `AI.Data/`, Labor | ausgeplant in [`KAMPFSTAERKE.md`](KAMPFSTAERKE.md) — sechs gestapelte PRs, jeder mit Aus-Stellung. **Schritt 0 ist reine Laborarbeit** (Stärkekurve als Referenz) und muss vor dem ersten Verhaltens-PR stehen |

Was Schritt 0 und der ausgefallene Schritt 1 zusammen gezeigt haben: **die
Form ist billig, die Regel ist teuer.** Der Umbau war byte-identisch und in
einem Zug erledigt; die eine Verhaltensregel darauf brauchte vier Messungen und
endete als Befund. Wer die Liste unten abarbeitet, sollte damit rechnen, dass
jeder Punkt so ausgeht — und die Form trotzdem bauen, weil ohne sie keiner der
Punkte formulierbar ist.

Zwei Reihenfolgeregeln, die nicht verhandelbar sind: **3 nach 2**, weil der
Rally-Punkt derselbe Punkt ist wie der Sammelpunkt, und **7 als Paar**, weil
„auf Reichweite stehenbleiben" ohne Aufklärung im Kontrolllauf über 2.000
Ticks null Schaden angerichtet hat.

Bei jedem PR ab 5: Referenz sichern, Determinismus zuerst (Exit 2 = Ende),
Hash-Kette gegen die Referenz diffen (der **erste abweichende Tick** ist die
wertvollste Zahl), beide Suiten fahren, `AiBehaviorId.Revision` bumpen,
Journaleintrag mit Abschnitt „Schlechter". Der Abschnitt „Im laufenden Spiel
gesehen" bleibt leer, solange der Linux-Build aussteht.

**Und ab jetzt eine Regel mehr, aus §7:** Jeder dieser PRs bringt seinen
Profilwert **mit Aus-Stellung** mit und wird **einseitig** gemessen — ein
`compare`-Lauf, in dem ein Kandidat mit dem neuen Verhalten gegen die Referenz
ohne es spielt. Selbstspiel misst bei einer Coderegel beide Seiten zugleich und
kann „zwei stärkere Armeen" nicht von „schlechtere KI" unterscheiden. V002 und
V003 wurden so beurteilt; ob ihre Rückweisung trägt, ist damit offen.

---

## Nicht anfangen — begründet

| Vorhaben | Warum nicht |
|---|---|
| **Verteidigungsmodule bauen** (`InstallDefenseModule`) | `ValidateDomain` lehnt diesen Befehl **unbedingt** ab: G2/G4-Inhalt laut `mvp-v1.json`. Eine KI, die ihn benutzt, produziert nur `intentsRejected`. Der Plan führt ihn als Position 1 der fehlenden Befehle — der Code widerspricht. |
| **Zielen unter der Angriffsschwelle, fünfte Fassung** | Vier gemessen, alle teurer als gar nicht zielen (Journal V003). Nicht die Zielformel ist schuld, sondern die unlösbare Zielsperre — solange Befund F001 offen ist, ist jede weitere Fassung dieselbe Sackgasse. |
| **`DefendBase` erneut, mit anderem Radius** | Gemessen: 8 → 9.564, 16 → 9.470, 24 → byte-identisch zu 16. Am Radius liegt es nicht (Journal V002). |
| **Legion-Waffenwerte ändern** (Issue 01) | `Simulation/Definitions/` ist geteilte Vertragsfläche. Das Labor *misst* Issue 01, die Umsetzung braucht Absprache. |
| **Kartenvarianz** | Erst nach dem Goal-System. Vorher tunt man gegen die gebrochene `GetEnemyStartAreaCell`-Annahme statt gegen Verhalten. |
| **Ein automatischer Optimierer** | Nicht vertagt, sondern nicht vorgesehen (Entscheidung 11). Es gibt keine skalare Gütefunktion, und für „sieht im Spiel richtig aus" gibt es keine Kennzahl. |

---

## Wie man merkt, dass es wirkt

Die Laborzahlen sind **Diagnose**. Ob eine Änderung im Spiel etwas taugt,
entscheidet eine gespielte Partie — und die gibt es inzwischen: die vier
Beobachtungen in [Journal B001](reports/behavior-log.md) stammen aus einer
Partie am Rechner, nicht aus einem Lauf. Sie sind der Grund, warum die Punkte 1,
2 und 6 oben so stehen, wie sie stehen.

**Das ändert nichts an der PR-Regel.** Beobachtet wurde der *Ist-Zustand*
`r2.A037B84D`. Ob eine der Änderungen von dieser Liste im Spiel etwas taugt,
ist damit weiterhin ungesehen, und genau so gehört es in einen PR-Text. Was
fehlt, ist eine gespielte Partie **nach** der Änderung — dafür braucht es den
Linux-Build, der Bringschuld des Netzstrangs ist.

Drei Fragen, an denen sich die nächste gespielte Partie messen lässt:

1. **Kommt die Armee als Welle oder als Kette?** (Punkt 1)
2. **Läuft der Angriff zweimal hintereinander denselben Weg aufs HQ?** (Punkt 2)
3. **Drehen angeschlagene Einheiten ab?** (Punkt 3)

Alle drei sieht man, ohne eine Zahl zu lesen. Das ist der Massstab, den diese
Liste meint — und die Sorte Satz, die als einzige in den PR-Text gehört.
