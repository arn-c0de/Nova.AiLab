# Verhaltensjournal der KI

Jede Änderung am KI-Verhalten bekommt hier **einen Eintrag, bevor die nächste
beginnt.** Der Zweck ist nicht Buchhaltung, sondern zweierlei:

1. **Nachvollziehbarkeit.** Zu jeder Änderung stehen die genauen Werte, die
   gemessenen Folgen — *beide* Richtungen — und der Laufbericht, aus dem die
   Zahlen stammen.
2. **Keine doppelte Arbeit.** Was ausprobiert und verworfen wurde, steht unter
   „Widerlegt". Wer eine Idee hat, sucht sie zuerst hier. Eine Sackgasse, die
   niemand aufgeschrieben hat, wird zuverlässig ein zweites Mal gelaufen.

> [!IMPORTANT]
> **„Besser" und „schlechter" sind menschliche Urteile, keine Rechnung.** Das
> Labor vergibt keine Note (Entscheidung 11) — es legt Zahlen nebeneinander.
> Die Einordnung in diesem Journal trifft ein Mensch und trägt deshalb immer
> die Rohwerte mit, damit sie überprüfbar bleibt statt geglaubt zu werden.
>
> **Und nichts hier ist im Spiel gesehen worden.** Jeder Eintrag sagt in der
> Kopfzeile, ob er nur gemessen oder tatsächlich gespielt wurde.

↩ [Labor](../README.md) · [Start hier](../START-HIER.md) ·
**[Roadmap](../ROADMAP.md)** — dieses Journal sagt, was *war*; die Roadmap sagt,
was als nächstes drankommt und in welcher Reihenfolge.

## Vorlage

```markdown
## V00N · JJJJ-MM-TT · Kurzname
**Lauf:** `runs/<id>.md` · **Commit:** `xxxxxxx` · **Status:** im Labor gemessen / im Spiel gesehen

### Was genau geändert wurde
### Besser        (Kennzahl, vorher → nachher, Quelle)
### Schlechter    (dito — leer lassen ist verdächtig, nicht sauber)
### Unverändert   (was ausdrücklich NICHT verschoben wurde)
### Widerlegt     (Annahmen, die der Lauf gekippt hat — nicht nochmal prüfen)
### Offen
```

---

## V012 · 2026-08-13 · Kämpfende Einheiten halten Abstand voneinander — **angenommen**, und sie ist der Anlass für die zweite Schwelle in V011

**Lauf:** einseitig über zwei Checkouts, Seed `0xA17E57DE57`, `20260813-1925-795044c7` (ohne) gegen `20260813-1927-6232ce1c` (mit) ·
**Status:** im Labor gemessen **und im laufenden Spiel gesehen** ·
**KI-Verhalten:** `r12.CA58924C`, **unverändert** — das hier ist Simulation, nicht KI ·
**Stand:** `feat/movement-engaged-spacing`

### Was genau geändert wurde

In der Trennsteuerung von `MovementSystem` ist der Mindestabstand eines Paars
nicht mehr immer `Radius + Radius`. Tragen **beide** Einheiten ein Angriffsziel
**und gehören sie demselben Spieler**, kommen 0,5 m dazu — 1,5 m statt 1,0 m.
`SimFixed.Zero` ist die Aus-Stellung.

**Warum nicht immer.** Tuchfühlung ist für eine Kolonne richtig: wer 50 % breiter
läuft, braucht durch jede Lücke 50 % länger, und das Flow-Field schickt Gruppen
absichtlich durch Lücken. Falsch wird es in dem Moment, in dem die Gruppe stehen
bleibt und schiesst — jede bewaffnete Einheit in MS-1 ist eine Fernkämpferin,
und eine auf Tuchfühlung gepackte Linie ist ein Klumpen, dessen hintere Reihe
nichts beiträgt ausser einem Körper zum Draufschiessen.

**Symmetrisch, und das ist ein Determinismus-Argument.** Jede Einheit rechnet den
Mindestabstand des Paars für sich, in einer anderen Iteration desselben
Durchlaufs. Eine einseitige Regel hätte eine Einheit, die sich von einer
Nachbarin wegdrückt, die weder ausweicht noch folgt — ein Driften, das auf zwei
Rechnern **identisch** wäre und deshalb an keiner Determinismusprüfung
auflaufen würde. Einen Feind weiter wegzudrücken wäre ausserdem keine Formation,
sondern ein Kraftfeld.

### Besser

Kanonische Partie, Spiegelpartie gegen Spiegelpartie, also direkt vergleichbar:

| Kennzahl | ohne die Regel | mit der Regel |
|---|---:|---:|
| Entscheidungstick | 4.905 | **4.337** |
| Eigene Verluste | 29 | **23** |
| Intents | 407 | **361** |

Die Regel kostet nichts, sie spart. Das war **nicht** der erste Befund, siehe
unten.

### Schlechter

- **Der erste Bau war ein Rückschritt und ist der eigentliche Ertrag dieses
  Eintrags.** Mit der Standoff-Regel in ihrer ersten Fassung — *eine* Schwelle
  für „hinlaufen" und „bleiben" — kostete dieselbe Partie **956 Intents statt
  222** und dreimal so viele Verluste. Ursache: eine Trennkraft, die jeden Tick
  nachdrückt, schob angekommene Einheiten aus ihrem Standoff-Ring, worauf die KI
  sie jede Kadenz zurückbeorderte. Das ist die V002-Signatur, erzeugt durch das
  Zusammenspiel zweier Regeln, die einzeln beide sauber messen.
- Erst die zweite Schwelle (V011) macht die beiden verträglich. **Wer diese Regel
  wieder ausbaut, nimmt der Hysterese ihren Anlass, nicht ihre Berechtigung.**

### Unverändert

- **Die vier Determinismus-Baselines bleiben grün, und das ist kein Freispruch.**
  Ihre Szenarien bringen nie zwei Einheiten *eines* Spielers gleichzeitig ins
  Gefecht — dort kämpft Spieler gegen Spieler, und für ein gegnerisches Paar
  greift die Regel bewusst nicht. Der Nachweis, dass sie überhaupt feuert, sind
  die vier Fälle in `EngagedSpacingTests`.

### Offen

- **Die Linienformation bleibt Inhaberentscheidung** (D-088). Was hier steht, ist
  Abstand, keine Formation: die Verteilung der Zielzellen macht weiterhin
  `ApplyMove`.

---

## V011 · 2026-08-13 · Der Standoff-Ring — **angenommen**, und im laufenden Spiel bestätigt

**Lauf:** einseitig, Seed `0xA17E57DE57`, `20260813-1927-6232ce1c` ·
**Status:** im Labor gemessen **und im laufenden Spiel gesehen** — Einheiten halten Abstand und kämpfen sauber miteinander ·
**KI-Verhalten:** `r11.E750CBB3` → **`r12.CA58924C`** ·
**Stand:** `feat/ai-standoff-engagement`

### Was genau geändert wurde

Das Marschziel einer angreifenden Einheit ist nicht mehr die Zelle des Feindes,
sondern ein Punkt auf **ihrem eigenen** Waffenreichweiten-Ring darum.
`engagementStandoffPercent` geht von 0 (r11 legte nur das Feld an) auf **80**.

**Es ist kein Kampf-, sondern ein Bewegungsbefund.** Jede bewaffnete Einheit in
MS-1 ist eine Fernkämpferin — Legion-Rekrut 6 Kacheln, Artillerie 18 — und
`CombatSystem` hat die ganze Zeit auf Distanz geschossen. Was fehlte, war ein
Grund **anzuhalten**. Hitscan-Leuchtspuren werden zwischen Schütze und Ziel
gezeichnet; auf Tuchfühlung bleibt davon nichts, und das Spiel sah aus, als
hätte es Nahkampf, den ihm keine Definition gibt. B002 hat genau das gespielt
gesehen.

**Ein Prozentsatz, keine Kachelzahl**, weil die Einheiten keine gemeinsame
Reichweite haben: eine feste Distanz stellte die Artillerie in die erste Reihe
oder hielte die Rekruten ausser Schussweite. Aus **einer** Zahl sortiert sich
eine gemischte Armee nach Waffe.

**Zwei Schwellen, nicht eine, und das ist gemessen statt ordentlich.** Hinlaufen
und Bleiben sind verschiedene Fragen: eine angekommene Einheit sitzt **auf** dem
Ring, also nimmt sie jeder Schubs um Haaresbreite heraus und kostet einen Befehl
pro Kadenz. Eine **stehende** Einheit gilt deshalb bis eine Kachel weiter aussen
als in Position — dieselbe Unterscheidung, die `IsAtTheStagingCell` über
`IsMoving` trifft, ohne Gedächtnis. Die Schleppe ist auf `Reichweite − Standoff`
gedeckelt, sonst hielte man eine Einheit auf einer Distanz fest, die ihre eigene
Waffe nicht erreicht — genau der Fehler, gegen den die Regel gebaut ist.

**Der Ring zieht auch zurück, nicht nur heran.** `MoveCell -1` heisst „kein neuer
Befehl", nicht „anhalten": eine Einheit, der man nichts sagt, führt ihren alten
Marschbefehl weiter aus. Und was sie üblicherweise ausführt, ist der Marsch auf
das feindliche **Startgebiet** — das Ziel, solange nichts sichtbar ist. Eine
Kadenz sind 20 Ticks, Infanterie legt darin acht Kacheln zurück, der Gegner
taucht bei zehn auf und steht bei zwei, wenn das nächste Mal jemand fragt.
**Innerhalb des Rings anzukommen ist der Normalfall**, nicht der Sonderfall.
Ohne das Zurückziehen sah die Regel in den Befehlen richtig aus, während das
Spiel weiter Tuchfühlung zeigte — der teuerste Befund dieses Eintrags, und
gefunden hat ihn eine gespielte Runde, kein Test.

### Besser

Allianz-Sitz, einseitig gegen dieselbe Partie mit der Aus-Stellung:

| Kennzahl | `standoff-off` | **80** |
|---|---:|---:|
| Entscheidungstick | 7.212 | **4.337** (−40 %) |
| Eigene Verluste | 49 | **23** (−53 %) |
| Intents | 531 | **361** |
| Geringste Annäherung an etwas Feindliches | **0,0 Kacheln** | **2,0 Kacheln** |

Die 0,0 ist wörtlich: die Einheiten standen im selben Feld wie der Gegner.

**Gegengeprüft auf einer zweiten echten Achse.** Je Armeeobergrenze gegen
**dieselbe** Obergrenze mit Aus-Stellung:

| Obergrenze | Eigene Verluste | Entscheidung |
|---|---|---:|
| 16 | 82 → **61** | 9.626 → **7.844** |
| 20 | 74 → **61** | 9.645 → **8.488** |
| 30 | 37 → **36** | 5.979 → 5.983 |

Zwei von drei deutlich besser, die dritte ein Gleichstand. Kein Ausreisser nach
unten — anders als bei V010, wo eine Kappe schlechter wurde.

### Schlechter

- **Die Reihe stützt die 80 nicht.** 40 (32 Verluste), 70 (37) und 100 (44)
  liegen ähnlich, 55 (51), 65 (51) und 90 (73) deutlich schlechter, ohne dass
  die Kurve monoton wäre. Über **einen** Seed ist das eine Partie pro Wert. Die
  80 ruht auf dem Margenargument — bei 100 hält die Einheit exakt auf der
  Grenze, die `IsInRange` prüft, und der erste Schubs nimmt sie heraus, wozu
  noch bis zu eine Kachel Quantisierung kommt — **nicht auf einer Messung.**
- **120 % ist absichtlich ausser Schussweite und kostet 2.100 Intents** gegen
  361. Das ist die Kontrolle, die zeigt, dass die Achse etwas Echtes misst.
- **Das Zurückziehen kostet.** Ohne es lag dieselbe Partie bei 3.364 Ticks, 13
  Verlusten, 222 Intents; mit ihm bei 4.905/29/407 (Stand ohne Abstandsregel).
  Die KI spielt also messbar schlechter und sieht dafür richtig aus. Das ist die
  Entscheidung, und sie fiel für das Spiel, nicht für die Tabelle.

### Unverändert

- **`engagementStandoffPercent: 0` ist der Zweig von vor der Regel**, tickgenau
  und hashgenau, was `EngagementStandoffTests` festhält.
- **Angriffsziel, Goal-Tabelle, Wellentor, Rückzugsregel und Sammelpunkt** sind
  unangetastet. Der Standoff-Punkt ist eine dritte vom Aufrufer errechnete
  Tatsache neben Verfolger und Zuhause-Flagge, damit `ApplyGoal` die reine
  Funktion bleibt, auf der ein erzwungenes Goal beruht.
- **Die vier Determinismus-Baselines** — sie fahren die Skirmish-KI nicht.

### Offen

- **Spielerbefohlene Fernkämpfer laufen weiterhin auf Tuchfühlung.** Das
  Bewegungsszenario `standoff` misst es: Überlauf 7 von 7 Kacheln, nächster
  Abstand 0. Die Regel sitzt in der KI-Entscheidung, nicht in der Bewegung, also
  gilt sie für einen direkten Move-Befehl nicht. Ein Reichweiten-Stopp im
  `MovementSystem` wäre der offensichtliche Ort — er würde aber gegen diese
  Regel arbeiten (zwei Wohnorte für eine Distanz) und einen expliziten
  Spielerbefehl übergehen. Offen, bewusst.
- **Die 80 wartet auf eine Seed-Achse.** Solange es die nicht gibt, ist jeder
  Wert dieser Reihe eine Partie.

---

## V010 · 2026-08-13 · Das HQ wird ein Gewicht — **angenommen**, und es ist die erste Regel, die die Zielvielfalt bewegt

**Lauf:** einseitig, Seed `0xA17E57DE57`, kanonische Spec, `out/r10-messreihe/` ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r9.800C26B0` → **`r10.E75CB19D`** ·
**Stand:** `integration/ai-goals`

### Was genau geändert wurde

In `FindBestVisibleEnemyByScore` fällt der `return` für das feindliche HQ weg.
Statt abzubrechen bekommt das HQ `targetHqWeight` (ausgeliefert **100**,
**0 = der alte Kurzschluss**) auf seinen gewöhnlichen Score addiert und
konkurriert damit mit allem anderen Sichtbaren.

**Warum das nötig war, obwohl V001 richtig argumentiert hat.** „Eine
Siegbedingung ist keine Vorliebe" stimmt als Regel. Falsch wird es durch eine
Eigenschaft der Karte: die Ausweich-Marschzelle ist das feindliche Startgebiet,
und dort steht das HQ. Der Kurzschluss und die Ausweichzelle zeigten also auf
dasselbe Gebäude — B001 hat genau das gespielt gesehen („rennen immer auf
Headquarter … laufen einfach straight line").

**Die Aus-Stellung ist 0 und nicht „eine sehr grosse Zahl".** Der Plan wollte ein
Gewicht, das alles überstimmt. Das ist ein schwächeres Versprechen, als es
klingt: „überstimmt alles" hängt an der Kartengrösse, der Schadenstabelle und
der Entfernungsstrafe und müsste bei jeder ihrer Änderungen neu geprüft werden.
0 als Sentinel lässt den alten Zweig **denselben Code** sein. Ein Gewicht von
100.000 ist trotzdem messbar identisch zum Kurzschluss, und ein Test hält das
fest — es ist nur nicht das, worauf der Schalter ruht.

### Besser

Allianz-Sitz, einseitig gegen dieselbe Partie mit dem Kurzschluss:

| Kennzahl | Kurzschluss | Gewicht 100 |
|---|---:|---:|
| **Verschiedene Zielarten** | **3** | **5** — Refinery und Harvester erstmals dabei |
| Entscheidungstick | 7.381 | **4.767** (−35 %) |
| Eigene Verluste | 33 | **14** |
| Intents je 1.000 Ticks | 43,2 | **42,4** |
| Ausgang | gewonnen | gewonnen |

**Gegengeprüft auf einer zweiten echten Achse.** Die Seed-Achse des Labors ist
leer, also ist ein guter Wert auf einer Achse eine Einzelpartie. Je
Armeeobergrenze gegen **dieselbe** Obergrenze ohne Gewicht:

| Obergrenze | Zielarten | Eigene Verluste | Entscheidung |
|---|---|---:|---:|
| 12 | 3 → **5** | 33 → **14** | 7.381 → **4.767** |
| 16 | 3 → **4** | 24 → 27 | 6.590 → 6.645 |
| 20 | 3 → **4** | 147 → **50** | 21.512 → **10.570** |
| 30 | 3 → **4** | 37 → **30** | 7.122 → **6.439** |

Die Zielarten steigen auf **jeder** Obergrenze, und die Refinery ist jedes Mal
dabei. Das ist der Unterschied zu V009: dort hing alles an einem Punkt einer
Achse, hier hält der Effekt über vier.

### Schlechter

- **Austauschverhältnis leicht runter** bei den kleinen Obergrenzen: 215 → 207
  (Kappe 12) und 254 → 248 (Kappe 16). Bei 20 und 30 geht es hoch (169 → 222,
  213 → 230). Die KI tauscht bei kurzen Partien etwas schlechter und gewinnt sie
  trotzdem früher.
- **Kappe 16 ist die eine Zeile, die nicht besser ist:** 27 statt 24 eigene
  Verluste bei praktisch gleichem Entscheidungstick.
- **Die Zwischenwerte sind teuer und stehen deshalb hier.** 1 bis 75 heben die
  Zielarten auf 5–7 und bezahlen es: Partie 1,4- bis 1,9-fach so lang, Intents je
  1.000 Ticks 53–58 gegen 43,2 (+24 bis +34 %). Das ist die V002-Signatur, und
  sie ist der Grund, warum keiner dieser Werte ausgeliefert wird — nicht ein
  Gefühl über „zu viel Ablenkung".
- **Bei Kappe 20 kostet auch der ausgelieferte Wert Befehlsstrom** (57,4 → 63,5,
  +11 %). Auf den anderen drei Kappen nicht.

### Unverändert

- **`targetHqWeight: 0` ist der alte Zweig, nicht seine Nachbildung** — der
  `return` steht noch da und wird noch genommen. Ein Test spielt eine Partie
  damit und prüft, dass die KI keine Refinery erreicht.
- **Ein Gewicht von 100.000 ist messbar der Kurzschluss:** dieselben Zielarten,
  dieselbe Partie.
- **Der Legion-Sitz ist auf fast jedem Wert unverändert.** Er bekommt das
  gegnerische HQ in dieser Partie nie zu sehen, also feuert die Regel dort nicht.
  Einzige Ausnahme ist Kappe 20, wo er eine Zielart dazugewinnt und marginal
  schlechter abschneidet (249 → 257 Verluste).
- `match --repeat 2` läuft mit Exit 0 durch; die vier Determinismus-Baselines
  bleiben grün.

### Widerlegt

- **„Der HQ-Kurzschluss ist als Siegbedingung richtig und nicht verhandelbar"**
  (V001). Er ist als *Regel* richtig und als *Verhalten* falsch, und der Grund
  ist keine Meinungsfrage: die Ausweichzelle zeigt auf dasselbe Gebäude, also
  gab es faktisch nur ein Ziel. Als Gewicht bleibt die Vorliebe erhalten — das
  HQ wird weiter angegriffen, auf jeder gemessenen Stellung.
- **„Mehr Zielvielfalt heisst längere Partien."** Das gilt für die kleinen
  Gewichte und kippt bei 100: fünf Zielarten und die kürzeste gemessene Partie
  der ganzen Reihe. Die Refinery abzuräumen ist nicht Ablenkung, sondern
  schneidet die gegnerische Produktion ab.

### Offen

- **Die Messung ruht auf einem Sitz.** Solange die Legion das gegnerische HQ
  nicht zu sehen bekommt, sagt diese Reihe nichts über sie. Ein Szenario, in dem
  beide Sitze weit genug kommen, fehlt.
- **Die Kurve ist nicht interpolierbar.** 75 und 150 sind beide schlechter als
  100. Wer den Wert verschiebt, misst neu.
- **Punkt 8a (Anmarsch über eine Route)** ist damit nicht erledigt: die Armee
  läuft weiterhin die Luftlinie, sie läuft sie jetzt nur auf mehr verschiedene
  Ziele. Die zweite Ursache aus NEXT-STEPS §2 steht.
- **Ungesehen im laufenden Spiel.** Das ist die sichtbarste Änderung dieser
  Reihe — „läuft der Angriff zweimal denselben Weg?" ist eine der fünf Fragen
  der Playtest-Checkliste — und genau deshalb ist es keine erledigte.

---

## V009 · 2026-08-13 · Nachschub-Doktrin — **gebaut, elf Stellungen gemessen, ausgeschaltet ausgeliefert**

**Lauf:** einseitig, Seed `0xA17E57DE57`, kanonische Spec, `out/r9-messreihe/` ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r8.1E6E7AE3` → **`r9.800C26B0`** ·
**Stand:** `integration/ai-goals` (Goal-System `r8` und der Feld-Fix aus #85
lokal zusammengeführt, beides noch nicht in `upstream/main`)

### Was genau geändert wurde

Neues Goal `Reinforce`, Priorität zwischen `DefendHome` und `Attack`, und ein
Profilfeld `reinforceMinStrengthPercent` (**0 = aus**). Die Regel vergleicht die
Kampfpunktsumme der Einheiten **draussen** gegen den eingestellten Anteil der
vollen Wellenschwelle:

| Lage | draussen | Wirkung |
|---|---|---|
| Erstschlag | 0 Punkte | nichts — das Wellentor behält seine Antwort |
| Welle intakt | ≥ Anteil | jede sammelnde Einheit marschiert sofort, als `Reinforce` |
| Welle gebrochen | > 0, < Anteil | der Ring wird auf die **volle** Schwelle festgehalten, niemand tröpfelt nach |

Die Arithmetik liegt als `ReinforcementDoctrine` in einem eigenen Typ, aus
demselben Grund wie `WaveStrengthGate`: die Zustände, auf die es ankommt — genau
die Schwelle, ein abschneidender Prozentsatz, ein Rest von einem Punkt — kann
eine Partie nicht herstellen.

**Was sie ersetzt, ist ein Nebeneffekt und keine Regel.** Seit `r5` wird der
Schwellwert auf das gekappt, was die Produktion noch liefern kann. Steht die
Armee an ihrer Obergrenze, fällt diese Decke auf das, was ohnehin im Ring steht,
der Vergleich wird `x >= x`, und jede Ersatzeinheit marschiert los — gleich ob
sie einem laufenden Gefecht oder einem Rest hinterherläuft.

### Besser

**Nichts, das trägt.** Es gibt gute Einzelwerte, und genau deshalb steht hier
die ganze Reihe statt der schmeichelhaftesten Zeile.

Eigene Verluste, einseitig, Sitz mit der Regel gegen denselben Sitz ohne
(Allianz / Legion, aus ist **33 / 71**):

| Prozent | 25 | 30 | 35 | **40** | 45 | 50 | 60 | 70 | 80 | 90 | 100 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Allianz | 43 | 55 | 35 | **21** | 21 | 51 | 33 | 28 | 28 | 58 | 58 |
| Legion | 93 | 33 | 33 | **34** | 83 | 83 | 83 | 83 | 83 | 83 | 83 |

**Die Allianz verbessert sich bei 40–45 und 70–80, die Legion nur bei 30–40.**
Die Schnittmenge ist der einzelne Punkt **40**, und ein Schritt zur Seite
verliert einen Sitz: bei 45 springt die Legion von 34 auf 83. Bei 40 sieht jede
Spalte gut aus — Allianz Austausch 247 statt 215, Gefechtsintervalle 9 statt 15,
Entscheidung 6.136 statt 7.381, Intents je 1.000 Ticks 41,7 statt 43,2 — und das
ist genau die Sorte Zeile, die man nicht nehmen darf. Die Seed-Achse des Labors
ist leer, also kann kein weiteres Abtasten aus diesem Punkt ein Plateau machen.
Es ist eine Einzelpartie treffen, derselbe Fehler wie Kappe 20 in V007.

### Schlechter

**Dort, wo die Regel wirken soll, ist sie auf jeder Stellung schlechter.** Bei
Obergrenze 30 bindet die `r5`-Decke nicht mehr, also läuft erstmals die ganze
Regel statt nur ihrer zurückhaltenden Hälfte. Gemessen gegen dieselbe Obergrenze
**ohne** Doktrin (Allianz 37 eigene Verluste, Austausch 213, gewonnen):

| Prozent | 30 | 40 | 50 | 60 | 70 | 80 |
|---|---:|---:|---:|---:|---:|---:|
| Eigene Verluste | 102 | 63 | 101 | 77 | 102 | 73 |
| Austausch | 100 | 153 | 145 | 155 | 98 | 91 |
| Ausgang | **verloren** | gewonnen | gewonnen | gewonnen | **verloren** | **verloren** |

Drei von sechs Stellungen drehen eine gewonnene Partie in eine verlorene. Keine
liegt bei den 37 Verlusten der Aus-Stellung.

Dazu, bei der ausgelieferten Obergrenze: **niedrige Werte kosten Befehlsstrom.**
Bei 20 steigen die Intents je 1.000 Ticks auf 49,0 und bei 25 auf 53,6, gegen
43,2 ohne die Regel — das ist der V002-Fehlermodus, den KAMPFSTAERKE §6.4
vorhergesagt hat, und er tritt genau dort auf, wo er vorhergesagt wurde. Ab 35
bleibt die Zahl unter der Aus-Stellung.

### Unverändert

- **`reinforceMinStrengthPercent: 0` ergibt bitgenau `r8`:** Entscheidung
  **7.381**, Endzustand **`0x68A90A2C0FAB6EE2`**, `hashchain.json` und
  `trace.ndjson` byteidentisch gegen den vorher gesicherten Referenzlauf. Die
  Aus-Stellung ist gemessen, nicht behauptet.
- `match --repeat 2` läuft mit Exit 0 durch.
- Die vier Determinismus-Baselines bleiben grün; keine fasst ein KI-System an.
- Verworfene Intents bleiben 0 auf allen Stellungen.
- **Der befürchtete Blockade-Rückfall trat nicht ein.** Fall (c) hält den Ring an
  einer Schwelle fest, die eine überlebende Restwelle blockieren kann — das ist
  V006 unter neuem Namen. Kein Lauf endete im Zeitlimit; alle 24 gingen als
  `VictoryElimination` aus. Das ist eine Beobachtung über diese Partie, kein
  Beweis, dass die Blockade unmöglich ist.

### Widerlegt

- **„Die Nachschub-Doktrin ist die Hälfte, die `r5` schon tut, plus eine
  Bedingung."** So stand es in KAMPFSTAERKE §6, und bei der ausgelieferten
  Obergrenze stimmt es nicht: dort feuert `Reinforce` **überhaupt nicht**. Die
  `r5`-Decke hat das Tor bereits offen, also hat die freigebende Hälfte nichts
  freizugeben, und was bei Kappe 12 gemessen wird, ist ausschliesslich die
  zurückhaltende Hälfte. Wer die Zahlen dieser Obergrenze als Urteil über die
  Doktrin liest, urteilt über eine halbe Regel.
  `ReinforcementDoctrineTests.AtTheShippedArmyCap_TheGateIsAlreadyOpenAndTheGoalNeverFires`
  hält das fest, statt es wiederentdecken zu lassen.
- **„Ein mittlerer Wert ist nicht automatisch ein Kompromiss"** (V006) bestätigt
  sich zum zweiten Mal, diesmal schärfer: der Startwert 50 aus dem Plan ist auf
  **beiden** Sitzen schlechter als die Aus-Stellung, während 40 auf beiden
  besser ist. Zehn Prozentpunkte, gegenläufiges Vorzeichen.
- **„Die Obergrenze anheben macht die Regel wirksam."** Das war die Erwartung
  aus KAMPFSTAERKE §6 („verschwindet der Nebeneffekt von selbst"). Sie ist
  widerlegt: bei Kappe 30 verschwindet der Nebeneffekt tatsächlich, und die
  Regel, die ihn ersetzt, ist schlechter als er.

### Offen

- **Welche Hälfte trägt die Verschlechterung bei Kappe 30?** Beide laufen dort
  gleichzeitig. Sie zu trennen bräuchte einen zweiten Schalter, und das wären
  zwei Verhaltensänderungen in einem PR. Wer das aufmacht, macht es als eigenen
  Schritt.
- **Die Asymmetrie zwischen den Sitzen ist geerbt, nicht erfunden.** Ein
  Prozentsatz **einer** Punktschwelle bedeutet für die zwei Fraktionen sehr
  verschiedene Bruchteile ihrer Armee: 480 Punkte sind knapp fünf
  Allianz-Schützen, aber elf Legions-Rekruten. Das ist das r6-Argument eine
  Ebene höher — eine Zahl für beide Fraktionen ist Absicht, und hier ist der
  Preis dafür sichtbar.
- Ob eine gespielte Partie das anders sieht, ist **ungeprüft**. Der Wert steht
  auf 0, es gibt im Spiel nichts zu sehen.

---

## B002 · 2026-08-09 · **Gespielte Beobachtung** — Wellen und Rückzug bestätigt, Zielwahl nicht

**Quelle:** Partie am Rechner, kein Laborlauf · **KI-Verhalten:** `r4.779A1B5B` ·
**Stand:** `integration/ai-strang`, alle fünf Stufen zusammen

> [!IMPORTANT]
> Die zweite Eintragsart, die im Sinne des Repos etwas *beweist*. Alles mit `V`
> davor ist Diagnose. Hier steht, was ein Mensch gesehen hat — und was er
> **nicht** gesehen hat, was genauso zählt.

Vier Beobachtungen, wörtlich und dann eingeordnet:

**1. „Kam in Welle."**
Bestätigt die Regel aus [V004](#v004--2026-08-09-wellen--angenommen-und-zwar-erst-in-der-fünften-fassung) an der Stelle, an der sie
gebaut wurde: nicht als Laborkurve, sondern als das, was man sieht. Das
Förderband aus NEXT-STEPS §1 ist im Spiel weg.

**2. „Angeschlagene drehen um und gehen in nächster Gruppe wieder mit los auf
Angriff."**
Das ist der Rückzug aus [V005](#v005--2026-08-09-rückzug--angenommen-ohne-die-hysterese-die-der-plan-wollte) **vollständig** bestätigt — und
zwar in beiden Hälften. Die zweite ist die, an der die Konstruktion hing: Ohne
Heilung war die Sorge, dass Verwundete zu Hause versauern und die Armeeobergrenze
belegen. Sie tun es nicht, sie ziehen mit der nächsten Welle wieder los. Genau
dafür wurde die Hysterese über Gefahr statt über Leben gebaut, und genau das
lässt sich im Labor nicht sehen.

**3. „Zielwahl nicht eindeutig erkennbar bis dato."**
**Die Zielwahl nach Score ist im Spiel NICHT bestätigt.** Sie ist im Labor
deutlich messbar (V001: Entscheidung 33 % früher, beide Seiten verlieren
weniger) und mit einem Test festgenagelt, der die alte Regel reisst — aber ein
Zuschauer sieht sie nicht. Das ist kein Widerspruch und kein Defekt: Wer welches
Ziel beschiesst, ist in einer Schlacht mit zwölf Einheiten mit blossem Auge kaum
auseinanderzuhalten. Es heisst nur, dass diese Regel **weiterhin nur gemessen
und nicht gesehen** ist, und so gehört sie in den PR-Text.

**4. „Momentan sieht alles sehr gut aus für das aktuell gewünschte Verhalten."**
Kein Fall, in dem etwas kaputt aussah — kein Hängenbleiben, kein sinnloses
Zurücklaufen, kein zu langes Warten. Das ist die Antwort auf die Frage nach dem
Gegenbeispiel, und sie ist diesmal leer. Aufgeschrieben, damit sie nicht später
als „wurde nicht gefragt" gelesen wird.

### Was daraus folgt

- **Die Wellenregel bekommt ihre Fortsetzung, und sie steht schon fest:**
  *„Automatischer Wechsel zu Tröpfeln, wenn die Armee bereits auf dem Angriffsweg
  ist, als Unterstützung."* Damit ist die Absicht aus V004 („kein Endzustand,
  die KI soll situationsabhängig entscheiden") vom Spieler selbst konkretisiert
  worden — und zwar auf genau die Lage, die im Labor am schwersten zu messen
  ist: Nachschub, während die Welle schon kämpft. Das ist der nächste Punkt für
  NEXT-STEPS.
- **Die Zielwahl braucht eine andere Art von Nachweis als das Auge.** Wenn sie
  im Spiel nicht sichtbar ist, ist die Frage, ob ein Spieler sie überhaupt
  merken *soll* — oder ob sie eine reine Effizienzregel bleibt, die man nur an
  den Zahlen sieht. Offen.

### Einschränkung, die zum Eintrag gehört

Gespielt wurde der **Integrationsstand mit allen fünf Stufen**, nicht jede Stufe
einzeln. Beobachtung 1 und 2 sind damit den jeweiligen Regeln zuzuordnen, weil
sie ohne sie gar nicht auftreten könnten; eine Aussage darüber, wie sich Stufe 3
**ohne** Stufe 4 anfühlt, ist damit nicht getroffen.

---

## B004 · 2026-08-10 · **Beobachtung am Player** — die Verteidigung greift, und man sieht es

**Quelle:** Mensch am Bildschirm, Laboraufnahme
`out/gui/20260810-203239-feat_ai-goal-system-dedd53b` ·
**KI-Verhalten:** `r8.1E6E7AE3` · **Stand:** `feat/ai-goal-system`

> **Die Aufnahme liegt als Video vor:**
> [die Basisverteidigung greift](https://youtu.be/pD9NXtTutmM) —
> `Nova.AiLab-goal-base-defense-r8-20260810`. Damit ist dieser Eintrag
> nachprüfbar statt zitiert.

**Das ist die Gegenaufnahme zu B003, dem Eintrag direkt darunter.** Dort,
wörtlich: *„Einheiten greifen Headquarter an, aber die roten NPC sammeln
sich an ihrem Armee-Sammelpunkt, anstatt erst die an ihrem Headquarter zu
bekämpfen."* Hier lösen sich dieselben Einheiten vom Sammelpunkt und marschieren
heim. Der Fall aus B003 ist in Bewegung zu sehen, nicht nur in einer Spalte.

Zur Aufnahme passt die Aufzeichnung: ab Tick **2820** stehen vier Legionäre
(#3098, #3099, #3103, #3105) unter `DefendHome`, und bei bedrohter Basis steht
**keine** Einheit im Sammelring mehr unter `Hold`.

### Was das belegt — und was nicht

- **Belegt:** die Regel feuert im gedachten Fall, sie feuert auf die richtigen
  Einheiten, und das Ergebnis sieht in Bewegung so aus, wie die Regel es
  beschreibt. Ein Anzeigefehler oder eine Regel, die nur in der Statistik
  existiert, wäre hier aufgefallen.
- **Nicht belegt:** dass die KI dadurch besser spielt. Sie verliert die Partie
  weiterhin, mit dreimal so vielen Verlusten — die Zahlen stehen in **V008**,
  der Messung zu derselben Regel.
- **Nicht gespielt.** Das ist eine Aufzeichnung des Labors, keine Partie in
  Unity. Der Abschnitt „Im laufenden Spiel gesehen" im PR-Text bleibt leer und
  als leer erkennbar. Was hier dazugekommen ist, ist ein Mensch, der die Sache
  angesehen hat — nicht ein Spiel, das sie bestanden hat.

---

## B003 · 2026-08-09 · **Beobachtung am Player** — die Welle sammelt, während das HQ fällt

**Quelle:** Mensch am Bildschirm, Laboraufnahmen `out/gui/…-1810/1811/1812` ·
**KI-Verhalten:** `r6.E34435F9` · **Stand:** `feat/ai-strength-wave-gate` (`308d8cf`)

> **Die Aufnahme von 18:12 liegt als Video vor:**
> [ganze Partie in normalem Tempo](https://youtu.be/bx0Pc4uCELI) —
> `Nova.AiLab-Behavior-a6-20260809-181200-feat_ai-strength-wave-gate-308d8cf`.
> Damit ist dieser Eintrag nachprüfbar statt zitiert, und die Aufnahme ist
> zugleich der Vorher-Stand, gegen den [ROADMAP](../ROADMAP.md) Punkt 1
> verglichen wird.

> Wörtlich: „Einheiten greifen Headquarter an, aber die roten NPC sammeln sich
> an ihrem Armee-Sammelpunkt, anstatt erst die an ihrem Headquarter zu
> bekämpfen."

Nachgemessen, und die Beobachtung hält:

| Lauf | HQ unter Beschuss | davon wehrlos | max. Wartende | Angriffe der Wartenden |
|---|---:|---:|---:|---:|
| **heute** (ausgeliefert) | 3.169 Ticks | **19 %** | **9** | 75 |
| Kappe 30 **ohne** Tor | 3.270 Ticks | **18 %** | **11** | 45 |
| Tor + Kappe 30 | *(HQ nie angegriffen)* | — | — | — |

„Wehrlos" heisst: mindestens drei eigene Kampfeinheiten stehen 8–16 Zellen vom
eigenen HQ entfernt — also am Sammelpunkt — während ein Feind innerhalb von
10 Zellen das HQ beschiesst. Spitzenwert in der ausgelieferten Fassung: **neun
Einheiten warten, während das HQ fällt.**

### Die Ursache, und sie ist nicht neu

Eine Einheit, die am Sammelpunkt **angekommen** ist, bekommt **überhaupt keinen
Befehl** — das ist Absicht und in `ResolveUnitAssignment` so kommentiert, weil
ein Befehl je Kadenz die Intent-Zahl aufbläht (V002). Sie hängt damit
ausschliesslich an der D-087-Auto-Acquisition, und die reicht nur so weit wie
die Waffe: Legions-Infanterie 6 Zellen. Der Sammelpunkt liegt **12 Zellen** vom
eigenen HQ entfernt. Ein Angreifer am HQ ist damit ausserhalb jeder Reichweite —
die 75 bzw. 45 gezählten Angriffe sind die Fälle, in denen der Feind nahe genug
vorbeikam, nicht Verteidigung.

**Das ist ein Defekt der ausgelieferten KI, nicht von r6.** Er existiert, seit
der Sammelpunkt existiert (r3). Eine Verteidigungsregel gibt es nicht: `DefendBase`
wurde in V002 gebaut und wieder ausgebaut (+23 % Intents, schlechteres Spiel).

### Aber r6 macht die Aussetzzeit erheblich grösser

| Lauf | Einheit-Ticks am Sammelpunkt je 1.000 Ticks | längste Wartezeit einer Einheit |
|---|---:|---:|
| **heute** | 3.502 | 1.048 Ticks |
| Kappe 30 **ohne** Tor | 4.186 | 886 Ticks |
| **Tor + Kappe 30** | **12.326** | **3.214 Ticks** |

> **Das Wellentor verdreifacht die Zeit, die Einheiten wartend herumstehen, und
> eine einzelne Einheit wartet bis zu 3.214 Ticks — über fünf Spielminuten.**
> Genau das ist die Fläche, auf der der beobachtete Fehler auftritt. Dass das
> Legions-HQ im Tor-Lauf nie angegriffen wurde, ist kein Gegenbeweis: die Legion
> hat diese Partie gewonnen, bevor es dazu kam. Auf einer Karte oder einem Seed
> mit früherem Druck ist die dreifache Aussetzzeit ein dreifaches Risiko.

### Folge für die PR-Reihenfolge

Die Anhebung der Armeeobergrenze auf 30 (Rückfrage im PR) **darf nicht vor einer
Abbruchregel kommen**. Bisher stand im Plan: Tor → Kappe → Nachschub-Doktrin.
Richtig ist: **Tor → Abbruch/Verteidigung → Kappe**.

Das ausgelieferte Tor selbst bleibt davon unberührt — es ist schlafend, die
Aussetzzeit von heute ändert sich durch diesen PR nicht.

### Offen

- Welcher Auslöser? Kandidaten: Feind innerhalb des Sammelrings, Feind in
  N Zellen um ein eigenes Gebäude, oder eingesteckter Schaden. Die V002-Falle
  ist, den Auslöser zu weit zu fassen — „reagiere auf eine echte Bedrohung,
  nicht auf alles was sich bewegt".
- Als **Übersteuerung von `WaveReady`** ist es billig: die Welle marschiert
  sofort, statt dass die ganze Armee jede Kadenz ein neues Ziel bekommt. Das
  ist die Form, an der `DefendBase` gescheitert ist — sie wäre hier vermieden.
- Ungemessen: ob die Aussetzzeit auch dann dreifach bleibt, wenn die Abbruch-
  regel greift.

---

## V008 · 2026-08-10 · `DefendHome` — Sammeln abbrechen, wenn die Basis brennt. **Gebaut, und es verschiebt die Niederlage statt sie abzuwenden**

**Lauf:** einseitig gemessen, Seed `0xA17E57DE57`, kanonische Spec ·
**Status:** im Labor gemessen, **am Player angesehen** (Eintrag **B004** oben,
[Video](https://youtu.be/pD9NXtTutmM)), **im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r7.E34435F9` → **`r8.1E6E7AE3`** ·
**Stand:** `feat/ai-goal-system` (auf dem Goal-System aufgebaut, nicht auf `main`)

### Was genau geändert wurde

Neues Goal `DefendHome`, Priorität zwischen `Retreat` und `Attack`. Auslöser:
ein **sichtbarer bewaffneter** Gegner innerhalb von `defendHomeCells`
(ausgeliefert **10**, **0 = aus**) Chebyshev um das eigene HQ. Wirkung für jede
Kampfeinheit, die **noch im Sammelring** steht und nicht zurückzieht: Marsch zur
**statischen HQ-Zelle**, Ziel = nächster sichtbarer bewaffneter Gegner. Wer
draussen ist, marschiert weiter. Die Bedrohungssammlung wurde vor die Haltung
gezogen, ihr Gatter von `retreatHealthPercent > 0` auf
`retreatHealthPercent > 0 || defendHomeCells > 0` erweitert — sonst hätte
`retreat-off` die Verteidigung stillschweigend mit abgeschaltet.

### Besser

| Kennzahl (Sitz 1, Legion) | vorher | nachher | Quelle |
|---|---:|---:|---|
| **Wehrlos im Beschussfenster** — Anteil der Beurteilungen unter `Hold`/`Advance`, während das eigene HQ Treffer nimmt | **96 %** | **60 %** | `goals.ndjson` × `events.ndjson` |
| Beurteilungen unter `Attack` in diesem Fenster | **0** | 371 | `goals.ndjson` |
| Ticks bis zur Entscheidung | 3.213 | **6.490** | `result.json` |

Und der Nachweis, dass es **nicht** `DefendBase` ist: 31 `DefendHome`-Beurteilungen
über 10 Einheiten kosten **25 Befehle**, auf **eine einzige** Zielzelle (121,121).
0,81 Befehle je Beurteilung — die Wiederholungsunterdrückung greift, weil das Ziel
statisch ist. `DefendBase` gab jeder Einheit jede Kadenz einen neuen.

### Schlechter

| Kennzahl (Sitz 1) | vorher | nachher |
|---|---:|---:|
| **Eigene Verluste** | 18 | **60** |
| **Intents je 1.000 Ticks** | 20,9 | **30,1** (+44 %) |
| Treffer auf das eigene HQ | 327 | 365 |
| Ausgang | verloren | **verloren** |

**Die +44 % sind zum grössten Teil nicht die Regel, und das ist zu belegen statt
zu behaupten.** Sitz 0 hat sein Profil in beiden Läufen **nicht** geändert und
steigt trotzdem von 34,7 auf 41,4 (+19 %) — die Partie dauert doppelt so lange,
beide Armeen werden grösser. Die Regel selbst setzt **25 von 194** Intents ab
(13 %). Was bleibt, ist die längere Partie, nicht der Befehlsstrom, an dem V002
gestorben ist.

**Der Rest ist echt schlechter.** Die Legion verliert dreimal so viele Einheiten
und die Partie am Ende trotzdem. Die Regel kauft 3.277 Ticks Überleben mit
42 zusätzlichen Toten.

### Unverändert

- **`defendHomeCells: 0` auf beiden Sitzen ergibt bitgenau `r7`:** Tick 3.213,
  Endzustand `0xE002DD893916967B`. Die Aus-Stellung ist gemessen, nicht behauptet.
- **Sitz 0 mit der Regel ändert nichts** — 3.213 und derselbe Hash. Sein HQ wird
  in dieser Partie nie angegriffen, die Regel feuert dort also nie. Kein Defekt,
  aber ein Hinweis darauf, wie schmal die Messbasis ist.
- Verworfene Intents bleiben **0**.
- Die vier Determinismus-Baselines bleiben **grün** — wie V001 gemessen hat,
  fasst keine von ihnen ein KI-System an.
- `match --repeat 2` läuft mit Exit 0 durch.

### Widerlegt

- **„Eine Abbruchregel scheitert am Befehlsstrom."** Das war die Lehre aus V002
  und sie gilt so nicht: sie scheitert an einem **beweglichen** Ziel. Mit der
  HQ-Zelle als Ziel kostet dieselbe Idee 25 Befehle in einer ganzen Partie.
- **„Wehrlosigkeit ist die Ursache der Niederlage."** Sie fällt von 96 % auf
  60 %, und die Legion verliert weiterhin. Wehrlosigkeit war ein *Symptom*; die
  Regel behebt es, ohne die Partie zu drehen.

### Offen

- **Eine Partie, ein Seed, eine Sitzverteilung.** Sitz 0 hat die Regel nie
  benutzt. Bevor daraus eine Aussage wird, braucht es die Gegensitzung und mehr
  als eine Karte.
- **Die 60 % sind nicht 0 % — und der Grund ist nicht die Regel.** Nachgezählt
  über die Aufzeichnung: in **jeder** Entscheidung mit bedrohter Basis ist
  **jede** Einheit im Sammelring entweder `DefendHome` (31) oder `Retreat`
  (17). **Null** Fälle von `Hold`/`Advance` im Ring bei bedrohter Basis. Die
  Regel greift also vollständig, wo sie greift. Das übrige `Hold` liegt
  **ausserhalb des Auslösers**: das HQ nimmt über 4.044 Ticks Treffer, als
  bedroht gemeldet ist es nur in 53 Entscheidungen. Der Beschuss kommt also von
  etwas, das die KI in zehn Zellen nicht sieht — grössere Reichweite oder gar
  nicht sichtbar. **Die nächste Frage ist damit nicht der Radius, sondern die
  Sicht.** `defend-16` liegt als Kandidat bereit, überschreitet aber den
  Sammelring und würde vermutlich das Falsche messen.
- **Die Verluste.** Ob 60 Tote für 3.277 Ticks ein guter Tausch sind, ist keine
  Rechnung, die dieses Labor anstellen darf (Entscheidung 11). Es ist die Frage
  an den Menschen.
- **Ungespielt.** Kein Mensch hat das im laufenden Spiel gesehen.

---

## V007 · 2026-08-09 · Kampfstärke statt Kopfzahl — **gebaut, und ausgeliefert wirkungslos**

**Lauf:** [`runs/20260809-1519-f13f4d5f.md`](runs/20260809-1519-f13f4d5f.md) ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r5.779A1B5B` → **`r6.E34435F9`** ·
**Stand:** `feat/ai-strength-wave-gate` auf `upstream/main` (`f13f4d5`) ·
**Definitionstabelle:** `0x6326FA3E56CFF5A3`

### Was genau geändert wurde

`CombatStrength.Of(faction, role, health)` = `Schaden × Leben / Feuerintervall`,
ganzzahlig, eine Division. Neues Profilfeld `waveStrengthPoints` (ausgeliefert
**1200**, **0 = aus**). Das Wellentor summiert diese Punkte über die Einheiten
**innerhalb** des Sammelrings statt sie zu zählen. Die r5-Regel wandert in
Punkten mit: der Schwellwert wird gegen das gekappt, was der Ring noch
**erreichen** kann — was dort steht plus eine frische Einheit je Kopf, den die
Armeeobergrenze noch freihat.

| Rolle, volle Gesundheit | Allianz | Legion |
|---|---:|---:|
| BasicInfantry | **100** | **44** |
| ScoutVehicle | 264 | 180 |
| LightTank | 962 | 672 |
| AntiArmorInfantry | 200 | 144 |
| BattleTank | 2.640 | 2.500 |
| Artillery | 550 | 274 |

Zwölf Rekruten sind **528** Punkte, zwölf Schützen **1.200**. Dieselbe „volle
Welle", 44 % der Angriffsstärke.

### Unverändert — und das ist hier die Hauptaussage

> **Mit der ausgelieferten Obergrenze 12 entscheidet das Tor identisch zum
> Kopfzählen. Byte für Byte.** `strength-off` (0) und die Referenz (1200)
> enden beide auf Tick **5.773** und `0x2B34B4E194257940`, dem r5-Wert.
> `SkirmishAiTests` hält denselben Endzustand `0x14472B2B943ED2BB` auf Tick
> 2.548; nur der Bezeichner bewegt sich.

Der Grund ist keine Nachlässigkeit, sondern Arithmetik: die Erreichbarkeitsdecke
bindet zuerst. Zwölf Allianz-Schützen **sind** 1.200 Punkte, und eine
dreizehnte Einheit lässt die Kappe nicht zu; bei der Legion liegt die Decke mit
528 sogar dauerhaft unter der Schwelle. Das Tor kann also erst etwas tun, wenn
die Obergrenze mehr Stärke zulässt, als es verlangt.

**Damit ist dieser Schritt eine Entkopplung, keine Verbesserung** — und der
Eintrag sagt das, statt eine Wirkung zu behaupten, die die Zahlen nicht hergeben.

### Besser — sichtbar wird es erst mit angehobener Kappe, dafür deutlich

Einseitig gegen die heutige Auslieferung (`strength-off` auf beiden Sitzen:
Tick 5.773, Verluste 23 / 51, Austausch 221 / 45, grösste Armee 12 / 12).
Je Zeile ist **ein** Sitz umgestellt, der andere bleibt heutig:

| Sitz | Kandidat | Tick | Sieger | eig. Verluste | eig. Austausch | grösste eig. Armee |
|---|---|---:|---|---:|---:|---:|
| Legion | *(heute)* | 5.773 | Allianz | 51 | 45 | 12 |
| Legion | `army-24-count` — **Kappe allein** | 5.921 | Allianz | **64** | **34** | 16 |
| Legion | `army-20` | 5.599 | **Legion** | 42 | 64 | 20 |
| Legion | `army-24` | 17.350 | **Legion** | 178 | 69 | 24 |
| Legion | `army-36` | 7.747 | **Legion** | 63 | **90** | **35** |
| Allianz | *(heute)* | 5.773 | Allianz | 23 | 221 | 12 |
| Allianz | `army-24-count` — **Kappe allein** | 4.154 | Allianz | 6 | 633 | 24 |
| Allianz | `army-24` | 3.914 | Allianz | 12 | 225 | 23 |
| Allianz | `army-36` | 3.914 | Allianz | 12 | 225 | 23 |

Zwei Dinge stehen da:

1. **Die Obergrenze allein macht die Legion schlechter** — Verluste 51 → 64,
   Austausch 45 → 34. Das ist genau die Asymmetrie, wegen der das Tor gebaut
   wurde: mehr Bauplatz nützt nichts, wenn die Welle weiter bei zwölf Köpfen
   losläuft, egal was zwölf Köpfe wiegen.
2. **Mit dem Tor kippt derselbe Sitz.** Bei 20, 24 und 36 gewinnt die Legion,
   und ihre grösste Armee wächst von 12 auf bis zu 35 — sie sammelt während des
   Gefechts weiter, statt bei zwölf stehenzubleiben. Das ist die Beobachtung,
   mit der diese Arbeit angefangen hat.

Im zweiseitigen `compare` dasselbe Bild: `army-20`, `army-24` und `army-36`
gewinnen **beide** Sitzungen (2/0/0), die drei `-count`-Gegenstücke keine
einzige zusätzlich (1/1/0, wie die Referenz).

### Schlechter

- **APM steigt, und zwar erheblich.** Legionssitz 13 → 43 bei Obergrenze 36,
  Allianzsitz 29 → 47. Das ist die Kennzahl, an der `DefendBase` gescheitert
  ist (V002, +23 % und schlechteres Spiel). Hier ist ein Teil davon erklärbar —
  eine dreimal so grosse Armee wird häufiger neu gruppiert — aber „erklärbar"
  ist nicht „geprüft". `intentsRejected` bleibt über alle 23 Kandidaten **0**.
- **Der Allianz-Sitz gewinnt durch das Tor nichts.** `army-24-count` ist dort
  besser als `army-24` (6 Verluste gegen 12, Austausch 633 gegen 225). Die
  Allianz bezahlt das Tor also, damit die Legion es bekommt. Bei Obergrenze 12
  kostet es sie nichts, weil dort gar nichts passiert.
- **Bei Obergrenze 24 dauert die Legionspartie 17.350 Ticks** und kostet 178
  eigene Einheiten. Gewonnen ist gewonnen, aber das ist eine Zermürbung, kein
  Angriff — dieselbe Form, die `retreat-75` in V006 disqualifiziert hat.

### Widerlegt

> **„Die Klippe zwischen 18 und 20 kommt vom an die Kappe gekoppelten
> Wellentor."** Das war die Erklärung im Plan, und sie ist **falsch**. Mit dem
> Tor bleibt die Klippe stehen: `army-18` braucht auf dem Legionssitz 17.908
> Ticks und 203 eigene Verluste, `army-16` bleibt bei 50 % im `compare`. Die
> Kopplung war also *eine* Ursache, nicht *die*. Was 18 von 20 unterscheidet,
> ist unerklärt und darf im PR-Text nicht als erklärt auftauchen.

> **„Kappe 20 ist die richtige Stellung."** Aus drei Stützpunkten geschlossen,
> mit vierzehn widerlegt: 19 und 21 verlieren beide. 20 war ein Treffer, keine
> Eigenschaft. Die tragfähige Grenze liegt bei 30 und folgt aus dem Schwellwert
> (28 Rekruten + 2 Köpfe Bauluft), nicht aus dem Kurvenmaximum.

> **„Ein Punktwert von 1200 lässt die Allianz unverändert."** Beim Schreiben
> angenommen, im Labor gekippt: sobald die Kappe Luft lässt, zählen
> **verwundete** Einheiten mit weniger Punkten, also braucht auch die Allianz
> mehr als zwölf Köpfe für 1.200 Punkte. Slot 0 mit Tor gegen Slot 0 ohne
> unterscheidet sich bei Kappe 24 um 5.297 Ticks. Die erste Messreihe war
> deshalb **nicht** einseitig und wurde verworfen und neu gefahren — hier steht
> nur die zweite.

### Gefunden und behoben, bevor es in den PR kam

> **Der „Boden von einer Einheit" war keine Absicherung, sondern eine zweite
> Regel.** Die erste Fassung hob den Schwellwert auf mindestens eine
> vollgesunde produzierte Einheit an. Damit wartete ein einzelner
> **verwundeter** Nachzügler — weniger wert als ein frischer Rekrut, während
> die Kappe von den Kämpfenden draussen belegt ist — auf eine Einheit, die nie
> gebaut werden konnte. Das ist die Blockade, die `f13f4d5` beseitigt hat, eine
> Nummer kleiner. Das Labor hat es gesehen, bevor ein Mensch es hätte sehen
> können: die kanonische Partie lief **1.650 Ticks länger** (7.423 statt
> 5.773). Ohne den Boden ist die Aus-Stellung wieder bitgenau. Die Lehre ist
> die alte: ein Gleichheitspfad, der nicht Byte für Byte gleich ist, ist kein
> Gleichheitspfad.

### Die Kappenkurve, dicht abgetastet — und warum 30 nicht geraten ist

Nachtrag desselben Tages. Drei Stützpunkte reichten nicht; hier sind vierzehn,
alle einseitig gegen die heutige Auslieferung, je ein Sitz umgestellt.

| Kappe | Legion: Tick | Sieger | eig. Verl. | Austausch | APM | Allianz: Tick | eig. Verl. | Austausch | APM |
|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| **12** *(heute)* | 5.773 | Allianz | 51 | 45 | 13 | 5.773 | 23 | 221 | 29 |
| 16 | 5.958 | Allianz | 50 | 54 | 31 | 3.437 | 7 | 342 | 27 |
| 18 | 17.908 | Allianz | 203 | 56 | 30 | 3.574 | 5 | 540 | 28 |
| 19 | 11.021 | Allianz | 116 | 56 | 31 | 3.620 | 7 | 371 | 30 |
| 20 | 5.599 | **Legion** | 42 | 64 | 34 | 3.677 | 7 | 357 | 32 |
| 21 | 6.749 | Allianz | 70 | 40 | 35 | 3.761 | 7 | 371 | 34 |
| 22 | 9.090 | **Legion** | 78 | 71 | 38 | 3.897 | 13 | 200 | 36 |
| 24 | 17.350 | **Legion** | 178 | 69 | 35 | 3.914 | 12 | 225 | 39 |
| 28 | 15.735 | **Legion** | 154 | 64 | 33 | 3.914 | 12 | 225 | 43 |
| **30** | **5.005** | **Legion** | **23** | **139** | **29** | 3.914 | 12 | 225 | 46 |
| 32 | 5.253 | **Legion** | 31 | 100 | 34 | 3.914 | 12 | 225 | 46 |
| 34 | 8.784 | **Legion** | 75 | 86 | 41 | 3.914 | 12 | 225 | 47 |
| 36 | 7.747 | **Legion** | 63 | 90 | 43 | 3.914 | 12 | 225 | 47 |
| 40 / 48 | 7.747 | **Legion** | 63 | 90 | 49 / 54 | 3.914 | 12 | 225 | 47 |

**20 ist eine Messerschneide, keine Stellung.** 19 verliert mit 116 eigenen
Verlusten, 21 verliert mit 70 und einem Austausch von 40 — schlechter als heute.
Wer aus dieser Nachbarschaft die 20 nimmt, hat eine Einzelpartie getroffen.

**30 und 32 sind es nicht, und es gibt einen Grund dafür.** 1.200 Punkte sind
**28 Legions-Rekruten** (27 sind 1.188, einer zu wenig). Die Punktklausel kann
aber nur entscheiden, solange **noch ein Kopf frei** ist — sonst hat der Deckel
„was die Produktion noch liefern kann" längst geöffnet. Sie greift deshalb
erstmals bei Obergrenze **29**, nicht 28.

Und darunter fällt die Welle **nicht auf Kopfzahl zurück**, wie hier zuerst
stand: sie degeneriert zu **„sammle die gesamte Armeeobergrenze"**. Genau das
sind die Zermürbungspartien bei 22, 24 und 28 (78, 178 und 154 eigene Verluste)
— die Legion sammelt erst alle 24 bzw. 28 und hat dann nichts mehr in
Produktion. **30** ist die erste Stellung, in der die Schwelle greift und
trotzdem zwei Köpfe zum Nachbauen frei bleiben, und dort entscheidet die Legion
**schneller als die heutige KI** (5.005 gegen 5.773) bei 23 statt 51 eigenen
Verlusten und Austausch 139 statt 45. Das ist eine Ableitung aus dem
Schwellwert, keine Auswahl aus der Kurve.

Nach oben hin steigt nur noch der Preis: die Allianz sättigt bei 23 Einheiten
(gleiche Zahlen ab Kappe 24), aber ihre APM klettert weiter bis 47, weil der
Produktionsschritt bei höherer Kappe unablässig nachlegt. Ab 40 ist die Kappe
reine Intent-Erzeugung.

### Der Fund, der die Obergrenze aus diesem PR heraushält

> **`MatchRunner` überschreibt vier Profilwerte mit eigenen Literalen**, und
> `targetArmySize: 12` ist eines davon (`MatchRunner.cs:254`). Die übrigen
> fünfzehn holt es sehr wohl aus `AiProfiles.Ms1Canonical` — über den
> historischen `AiFactionProfile`-Konstruktor, der genau dafür da ist; nur
> deshalb kommt `waveStrengthPoints` überhaupt im Spiel an.
>
> Folge für die Obergrenze: sie in `AiProfiles` zu ändern hätte das **Labor**
> verschoben und das **Spiel** genau so gelassen, wie es ist — und den
> Kommentar „was MatchRunner heute ausliefert, Wert für Wert" zur Lüge gemacht.
> Aufgefallen ist es, weil der gepinnte Endzustand nach der Änderung **nicht**
> wanderte: `SkirmishAiTests.BuildAiHost` spiegelt denselben Vierzahl-Aufruf.
>
> `Scripts/Gameplay/Match/` ist Netzstrang und laut Arbeitsvertrag §2
> ausdrücklich nicht anzufassen. Die Obergrenze ist damit eine **Rückfrage an
> den Maintainer**, keine Änderung, die wir einreichen — eine Zeile in seiner
> Datei. Die Zahlen dafür stehen oben.
>
> **Nebenbefund, inzwischen geschlossen:** diese vier Zahlen konnten still
> auseinanderlaufen — der Profil-Hash rechnet über `Ms1Canonical`, der
> Endzustands-Pin spiegelt MatchRunners Literale, beide Wächter sehen also von
> je einer Seite an der Lücke vorbei. `AiProfileTests.MatchRunnerPassesTheSame\
> FourNumbersTheShippedProfileCarries` liest jetzt MatchRunners Quelltext und
> wird rot, wenn sie abweichen. Lesen ist kein Anfassen.

### Gegengelesen — was die Prüfung noch gefunden hat

Vier unabhängige Durchgänge über den fertigen Stand (Arithmetik, Scope und
Determinismus, Testaussagekraft, Behauptungen gegen Code). Scope, Baselines,
Verträge, Assembly-Trennung und `.meta`-GUID waren sauber. Der Rest war es nicht:

> **Der Deckel zählte Köpfe, die niemand bauen kann.** „Was die Produktion noch
> liefern kann" rechnete freie Plätze der Armeeobergrenze mit, auch ohne
> Kaserne. Bei Obergrenze 12 folgenlos, bei der vorgeschlagenen 30 ein Hänger:
> plateauiert die Armee unter der Schwelle, wartet die Welle bis zum Zeitlimit.
> Der Zählpfad kannte das Problem nicht, weil er unabhängig davon bei `waveSize`
> marschierte — ein Punktschwellwert hat diese zweite Grenze nicht. Behoben, die
> Aus-Stellung bleibt bitgenau.

> **Die Arithmetik war nicht prüfbar, und das war messbar.** Mutationsprobe: den
> Negativ-Clamp löschen — Suite grün. `waveStrengthPoints` ganz ignorieren —
> Suite grün. Die ausgelieferte Obergrenze erreicht die unterscheidenden
> Zustände nie. Die Schwelle liegt jetzt als reine Funktion in
> `WaveStrengthGate` mit eigener Fixture; beide Mutationen fallen.

> **Der Dormanz-Wächter bewachte die falsche Zahl.** Er prüfte
> `Kappe × 44 <= 1200` aus drei Konstanten. Die tragende Ungleichung ist
> `(Kappe − 1) × stärkste produzierte Einheit < Schwelle`, also `11 × 100 <
> 1200` — **Reserve neun Punkte pro Allianz-Schütze**. Ein Schadenspunkt mehr
> beim Schützen hätte das Tor geweckt und der Test hätte weitergeschlafen. Er
> rechnet jetzt aus `CombatStrength`.

> **„`MatchRunner` liest `AiProfiles` nicht" war falsch** — siehe die Korrektur
> oben. Es liest fünfzehn der neunzehn Werte und überschreibt vier.

> **`NoFloatInSimulationTests` bewacht `Scripts/AI*` nicht**, obwohl ein
> Kommentar von uns das behauptete. Es scannt `Core` und `Simulation`. Der neue
> Code ist float-frei, aber ein `float` unter `Scripts/AI/` käme heute lautlos
> durch die CI. Kommentar korrigiert; die Lücke selbst nicht, weil der Test
> zweimal existiert und die EditMode-Kopie unter `Assets/Tests/` nicht
> zugeteilt ist — steht als Frage im PR.

### Offen

- **Die Obergrenze selbst.** Siehe oben — sie liegt in fremdem Terrain.
- **Warum 18 einbricht.** Siehe „Widerlegt". Unerklärt, und 19 und 21 zeigen,
  dass der ganze Bereich unter 22 unruhig ist.
- **Immer noch keine gespielte Partie.** Alles hier ist Diagnose.
- Der Punktwert kennt weder Panzerung noch Reichweite. Solange die KI reine
  Infanterie baut, ist das folgenlos; mit Fahrzeugen nicht mehr.

---

## V006 · 2026-08-09 · **Nachmessung auf der neuen Basis** — beide Regeln halten, eine Annahme nicht

**Lauf:** [`runs/20260809-0933-9c2817fe.md`](runs/20260809-0933-9c2817fe.md) ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r4.779A1B5B` · **Commit:** `9c2817fe` (`feat/ai-waves-and-retreat`
auf dem neuen `upstream/main`) · **Definitionstabelle:** `0x6326FA3E56CFF5A3`

Kein Eingriff, eine Nachmessung. Zwei Dinge haben sich seit V004/V005 geändert,
und beide zwingen dazu:

1. **Das Spiel hat sich bewegt.** Der Maintainer hat gemergt, unter anderem
   „die fertige Raffinerie stellt ihren ersten Sammler kostenlos hin" — eine
   Simulationsänderung. Damit sind **alle** Zahlen aus V004 und V005 an einem
   Commit gemessen, den es so nicht mehr gibt. Die Regel des Labors dazu ist
   eindeutig: nach einem Merge-Fenster wird **neu vermessen, nicht über die
   Grenze hinweg verglichen.**
2. **Das Labor liegt jetzt neben dem Spiel** statt darin. Dieser Lauf ist der
   erste, der einen Branch misst, in dem das Messwerkzeug **nicht** liegt —
   vorher war genau das unmöglich, und deshalb war der PR-Stand bis hierher
   unvermessen.

### Die Referenzpartie

Tick **9.164**, Endzustand **`0x8054A759F73E1F81`**, Determinismus Exit 0.
Slot 0: Austausch 230, 19 von 183 Intervallen mit Verlusten, 41 unbeantwortete
Schadensereignisse, 18 Aktionen pro Minute.

### Besser — beide Regeln bestätigen sich einseitig

Jeder Kandidat spielt gegen die ausgelieferte Referenz, beide Fraktionsrollen:

| Kandidat | Austausch | Verluste | Gefechtsintervalle | APM |
|---|---:|---:|---:|---:|
| **ausgeliefert** (`waveSize 12`, `retreat 60`) | **131** | **62** | **18** | **18** |
| `wave-off` (`waveSize 1`) | 84 | 143 | 59 | 26 |
| `retreat-off` (`retreatHealthPercent 0`) | 89 | 56 | 15 | 16 |

Wellen: Austausch +56 %, Verluste −57 %, und die Verlustkurve fällt von 59 auf
18 Intervalle. Rückzug: Austausch +47 %. Die Richtungen sind dieselben wie in
V004 und V005, die Beträge nicht — was genau der Grund ist, warum man nach
einem Merge-Fenster nachmisst statt umzurechnen.

### Widerlegt — und das ist der Ertrag dieses Laufs

> **„Die Wellengrösse wirkt monoton." Auf der neuen Basis nicht mehr.** V004 hat
> über fünf Grössen eine monoton steigende Kurve gemessen und daraus geschlossen,
> das Optimum liege am Rand. Jetzt liegt `wave-6` mit Austausch **74** und 186
> Verlusten **unter** `wave-off` (84 / 143) — eine halbvolle Welle ist schlechter
> als gar keine. Das ist plausibel und war vorher nicht sichtbar: Sechs Einheiten
> warten lange genug, um den Nachschub zu bremsen, und sind zu wenige, um die
> Schlacht zu entscheiden. Sie kommen zu spät und zu schwach.
>
> **Folge:** `waveSize` ist kein Regler, an dem man vorsichtig dreht. Entweder
> ganze Armee oder aus. Wer ihn „auf einen mittleren Wert" stellt, wählt die
> schlechteste Stellung von allen.

> **Der Rückzug kostet Tempo, nicht Einheiten — und zwar messbar.**
> `retreat-off` entscheidet bei Tick 7.125 statt 9.164 und verliert dabei
> *weniger* eigene Einheiten (56 gegen 62). Bezahlt wird das mit dem Austausch
> (89 gegen 131): Ohne Rückzug stirbt die Armee schneller und nimmt weniger mit.
> Die Regel macht die Partie also länger und teurer für beide — das ist ein
> Handel, kein reiner Gewinn, und in V005 stand er noch nicht so deutlich da.

> **`retreat-75` schlägt die ausgelieferte 60 im Austausch — und bleibt trotzdem
> falsch.** 166 gegen 131, aber bei 0 % Siegen und 17.770 Ticks. Dieselbe Form wie
> auf der alten Basis: Der höhere Wert wird mit einer Partie erkauft, die nicht
> mehr endet. Eine Kennzahl allein hätte hier die schlechtere KI gewählt.

### Unverändert

- `intentsRejected` bleibt **0** über alle 14 Kandidaten.
- Determinismus Exit 0; Duell-Arena und Bewegungsszenarien unauffällig.
- Die Definitionstabelle ist dieselbe (`0x6326FA3E56CFF5A3`) — der Merge hat
  Verhalten geändert, keine Einheitenwerte.

### Offen

- **Immer noch keine gespielte Partie.** Die Prüfliste steht in
  [`../PLAYTEST-CHECKLIST.md`](../PLAYTEST-CHECKLIST.md); gespielt ist zuletzt
  `r2`, und alles seither ist ungesehen.
- Ob die halbvolle Welle auch dann verliert, wenn die Armeeobergrenze steigt,
  ist ungemessen — `waveSize 6` bei `targetArmySize 24` wäre eine andere Frage
  als `6` bei `12`.

---

## V005 · 2026-08-09 · Rückzug — **angenommen**, ohne die Hysterese, die der Plan wollte

**Lauf:** `runs/` (letzter Eintrag) · **Status:** im Labor gemessen,
**im laufenden Spiel ungesehen** ·
**KI-Verhalten:** `r3.1D8DA20F` → **`r4.779A1B5B`** ·
**Referenzpartie:** Tick **5.931**, Endzustand **`0x8E054C63DE80BDD6`**

### Was genau geändert wurde

Zwei Profilfelder (`RetreatHealthPercent: 60`, `RetreatDangerCells: 8`) und ein
Zweig in `ResolveUnitAssignment`, der **vor** allem anderen steht:

> Eine Einheit unter 60 % Leben, in deren Nähe (8 Zellen) ein **bewaffneter**
> Feind sichtbar ist, läuft zum Sammelpunkt zurück — auch wenn sie längst
> draussen ist. Zu Hause ist sie eine ganz normale wartende Einheit und zieht
> mit der nächsten Welle wieder los.

Ein zurückweichender Soldat bekommt kein Angriffsziel; die
D-087-Auto-Acquisition schiesst weiter auf das, was ihn verfolgt.

### Widerlegt — noch bevor eine Zeile lief

> **Die Hysterese aus NEXT-STEPS §0 ist nicht baubar.** Sie verlangt Eintritt
> bei 25 % und Austritt bei 60 % — das setzt voraus, dass eine Einheit heilt.
> **In MS-1 heilt keine Einheit.** `ValidateRepair` verlangt als Ziel eine
> fertiggestellte **Platzierung**, also ein Gebäude; die einzige Stelle im
> ganzen Repo, die Einheitenleben erhöht, ist `EvolvedFactionSystem` — G2-Prototyp
> mit Float-Gerüst, in keiner MS-1-Partie registriert. Ein Austrittswert wäre
> nie erreicht worden: Verwundete hätten sich zu Hause gestapelt, die
> Armeeobergrenze belegt und die Welle nie wieder voll werden lassen. Die
> Dämpfung läuft deshalb über **Gefahr und Entfernung** statt über Leben.

> **`SetRallyPoint` ist kein Sammelbefehl** (Befund
> [F002](../findings/F002-rallypoint-ist-die-spawnzelle.md)). Er ist die
> **Spawn-Zelle**: `TryFindSpawnCell` sucht ab der Rally-Zelle nach aussen, und
> `ValidateSetRallyPoint` kennt **keine Entfernungsgrenze**. Der Rally-Punkt auf
> den Sammelpunkt hätte den Nachschub zwölf Zellen weit **teleportiert** — in
> einem Projekt, dessen KI ausdrücklich keine Abkürzung nimmt. **PR 3 aus
> NEXT-STEPS wurde deshalb nicht gebaut**, und die Absicht dahinter ist seit
> `r3` ohnehin erfüllt.

### Besser

Einseitig, `compare`, beide Fraktionsrollen. `retreat-off` ist dasselbe Binary
mit `retreatHealthPercent: 0`:

| Kennzahl | `retreat-off` | ausgeliefert (60 %) | |
|---|---:|---:|---|
| Austauschverhältnis | 93 | **123** | +32 % |
| Verluste (Mittel) | 62 | **35** | −44 % |
| Intervalle mit Verlusten | 20 | **10** | halbiert |
| Entscheidungstick | 7.929 | **5.931** | −25 % |
| Aktionen pro Minute | 18 | **15** | weniger Rauschen |

Die Schwelle ist gemessen, nicht gewählt. Einseitig als Allianz, Austausch
gegen die Referenz **ohne** Rückzug (Stand `r3`, vor der Korrektur unten):
25 → 138, 40 → 184, 60 → **252**, 75 → 290, 90 → 209. Die 75 kauft ihren
höheren Wert mit einer doppelt so langen Partie und doppelt so vielen eigenen
Verlusten; bei 90 kippt die Kurve auf beiden Seiten.

### Schlechter

- **Die Partie zieht sich, wenn die Schwelle zu hoch steht.** Bei 75 % 14.475
  statt 5.931 Ticks. Bei 60 % ist der Effekt klein, aber vorhanden.
- **Die Reaktionslatenz steigt** (33 → 116 Ticks im Mittel). Das ist kein
  Widerspruch, sondern eine Eigenschaft der Kennzahl: Rückzug erzeugt
  *zusätzliche* späte Antworten auf Schaden, der vorher **gar keine** bekommen
  hätte, und der Mittelwert läuft ihnen hinterher. Die ehrlichere Spalte ist
  `unbeantwortet`: 59 → 35.
- **Ein Späher, der einmal getroffen wurde, ist praktisch aus der Partie**,
  solange die nächste Welle nicht voll ist. Ohne Heilung wird er nie wieder
  gesund; er läuft nur nicht mehr sinnlos in den Tod.
- **Gegen einen Menschen ungemessen.** Wer die KI mit einer einzelnen billigen
  Einheit beschiesst, kann eine ganze Welle nach Hause schicken. Im Labor tritt
  das nicht auf, weil keine Seite absichtlich ködert.

### Korrektur am Wellencode, im selben Zug

Der neue Test hat einen Fehler in **V004** gefunden: „angekommen" war als
*innerhalb der Toleranz um den Sammelpunkt* geprüft, ohne zu fragen, ob die
Einheit noch **läuft**. Eine zurückgerufene Einheit, die zufällig innerhalb von
vier Zellen am Sammelpunkt vorbeimarschierte, bekam deshalb gar keinen Befehl
und lief weiter aus dem Ring hinaus. Jetzt gilt „angekommen" nur für eine
Einheit, die auch **steht**.

Das verschiebt die Zahlen aus V004: Referenzpartie **6.223 → 5.931**,
Endzustand `0x5243FDAD54967102` → `0x8E054C63DE80BDD6`. Die Aussage von V004
bleibt: `wave-off` liegt in derselben Messung bei Austausch **82** gegen 123 und
100 Verlusten gegen 35.

### Unverändert

- **Determinismus:** zweimal gefahren, Exit 0, beidseitig wie einseitig.
- **Die Aus-Stellung ist bitgenau das Verhalten davor.**
  `retreatHealthPercent: 0` liefert die `r3`-Hash-Kette.
- `intentsRejected` bleibt **0**.
- **561/561 SimRunner-Tests und 94/94 Labortests grün**; die vier
  Baseline-Dateien sind nicht angefasst.

### Offen

- **Ködern.** Die offensichtliche Gegenstrategie eines Menschen, und sie ist
  ungemessen. Ein Laborszenario dafür wäre: eine billige Einheit beschiesst die
  Welle und zieht sich zurück.
- **Die Reaktionslatenz braucht einen zweiten Blick.** Ein Mittelwert über
  Ereignisse, deren Zahl sich mit der Änderung selbst ändert, ist schwer zu
  lesen. Ein Median oder eine Verteilung wäre ehrlicher.
- **Kein Spielbericht.** Die drei Fragen dafür stehen in
  [`../PLAYTEST-CHECKLIST.md`](../PLAYTEST-CHECKLIST.md).

---

## V004 · 2026-08-09 · Wellen — **angenommen**, und zwar erst in der fünften Fassung

**Lauf:** `runs/20260809-0748-0b0c211c.md` · **Status:** im Labor gemessen,
**im laufenden Spiel ungesehen** (Linux-Build steht aus) ·
**KI-Verhalten:** `r2.A037B84D` → **`r3.1D8DA20F`** ·
**Basis:** V003-Stand (`0x5D8FB2D45FFD16B6`, Tick 8.715)

Der erste Eintrag, der **einseitig** gemessen wurde — die Abhilfe aus
[M001](#m001--2026-08-09-methodenbefund--v002-und-v003-wurden-im-selbstspiel-beurteilt),
zum ersten Mal angewandt. Und der erste, bei dem eine Verhaltensregel im
ausgelieferten Profil landet.

### Was genau geändert wurde

| Ort | Änderung |
|---|---|
| `AI.Data/AiProfile.cs` | drei Ganzzahlfelder: `WaveSize`, `StagingDistanceCells`, `StagingToleranceCells` |
| `AI.Data/AiProfiles.cs` | `ms1-canonical` bekommt `waveSize 12, staging 12, tolerance 4`; `legacy-defaults` bleibt bei `waveSize 1` (Wellen gab es dort nicht) |
| `AI/SkirmishAiSystem.cs` | `ArmyPosture` trägt Sammelzelle und `WaveReady`; `ResolveUnitAssignment` entscheidet je Einheit zwischen marschieren, hinlaufen und stillhalten |
| `AI.Data/AiBehaviorId.cs` | `Revision` 2 → 3, Historienzeile |

Die Regel in einem Satz: **Wer schon draussen ist, marschiert weiter; wer noch
drinnen ist, wartet, bis die Armee voll ist.** „Draussen" heisst ausserhalb
eines Rings von `stagingDistance + tolerance` = 16 Zellen um das eigene HQ —
gemessen am **HQ**, nicht am Ziel, damit eine Einheit nicht zwischen „draussen"
und „wartend" kippt, weil der Gegner ein paar Zellen gelaufen ist. Die
Sammelzelle liegt 12 Zellen vom HQ auf der Geraden zum Feindgebiet und ist für
die ganze Partie dieselbe: beides statisches Kartenwissen, also kein
Befehlsrauschen.

`waveSize 12` **ist** die Armeeobergrenze. Das ist die Regel als eine Zahl:
angreifen mit voller Armee, nie stückweise nachschieben. Da die Obergrenze alle
lebenden Einheiten zählt, heisst das praktisch: die nächste Welle startet, wenn
die vorige aufgerieben ist.

### Besser

Einseitig gemessen, `compare`-Lauf, beide Fraktionsrollen, Seed `0xA17E57DE57`.
Der Kandidat `wave-off` (`waveSize 1`) ist dasselbe Binary **ohne** die Regel:

| Kennzahl | `wave-off` | ausgeliefert (`waveSize 12`) | |
|---|---:|---:|---|
| Verluste (Mittel je Partie) | 175 | **41** | −77 % |
| Austauschverhältnis (Feindverluste je 100 eigene) | 78 | **105** | +35 % |
| Intervalle mit Verlusten | 64 | **11** | die Verlustkurve wird zu Sprüngen |
| Aktionen pro Minute | 21 | **14** | weniger Befehlsrauschen, nicht mehr |
| Siegquote gegen die jeweils andere Fassung | 0 % | — | 0/1/1 |

Die dritte Zeile ist die, um derentwillen die Kennzahl überhaupt gebaut wurde:
**aus 64 Intervallen mit Verlusten werden 11.** Das ist genau der Unterschied,
den NEXT-STEPS §1 als „Förderband statt Angriff" beschreibt, und keine der
bisherigen Spalten konnte ihn sehen.

Die Referenzpartie entscheidet ausserdem **früher**, nicht später: Tick
**6.223** statt 8.715, Endzustand `0x5243FDAD54967102`. Das widerspricht der
Erwartung in NEXT-STEPS §7 („Wellen werden den Entscheidungstick erhöhen — die
Armee wartet ja") und ist als Widerlegung unten festgehalten.

### Schlechter

- **Die KI ist in der Aufbauphase wehrlos gegen einen frühen Angriff.** Sie
  greift erst mit zwölf Einheiten an; wer mit drei Einheiten früh kommt, findet
  eine Armee, die im Ring um die eigene Basis wartet — sie verteidigt sich
  (Auto-Acquisition), aber sie kontert nicht. Im Labor tritt das nicht auf, weil
  beide Seiten dieselbe Öffnung fahren. **Gegen einen Menschen ist das die
  offensichtliche Gegenstrategie**, und sie ist ungemessen.
- **Wartende Einheiten zielen nicht.** Bewusst so (Befund F001), aber es heisst:
  in der Wartephase schiesst die Armee nach Entfernung statt nach Gefahr — die
  Lücke aus V001, jetzt an der Wellengrösse statt an der Angriffsschwelle.
- **`SkirmishAi_ShootsTheDangerousTarget…` musste umgestellt werden.** Der Test
  prüft die Zielformel und wurde vom Wellentor verdeckt; er fährt jetzt
  ausdrücklich mit `waveSize 1`. Das ist ehrlich, aber es heisst auch: die
  Zielformel ist im ausgelieferten Profil **während der Wartephase ungetestet**.
- **Ein Rand bleibt offen:** eine Einheit, deren Welle aufgerieben wurde und die
  noch innerhalb des Rings steht, wird zum Sammelpunkt zurückgerufen. Das Fenster
  ist schmal (die ersten 16 Zellen des Marsches) und wurde nicht gesondert
  gemessen.

### Unverändert

- **Determinismus:** jede Fassung zweimal gefahren, Exit 0 — beidseitig und
  einseitig.
- **Die Aus-Stellung ist bitgenau das alte Verhalten.** `--profile wave-off`
  liefert Tick 8.715 und `0x5D8FB2D45FFD16B6`, Hash-Kette byte-identisch zur
  Referenz von vorher. Das ist der Nachweis, dass der Code ohne die Regel
  denselben Pfad nimmt und nicht bloss dasselbe Ergebnis ausrechnet.
- `intentsRejected` bleibt **0**.
- Duell-Arena und Bewegungsszenarien byte-identisch (sie fahren kein KI-System).
- **560/560 SimRunner-Tests und 94/94 Labortests grün**, die vier
  Baseline-Dateien inbegriffen und nicht angefasst.

### Widerlegt

> **„Wellen erhöhen den Entscheidungstick." Nein.** NEXT-STEPS §7 hat das als
> sicher angenommen und daraus abgeleitet, dass der PR ohne die
> Gefechtsdichte-Spalte durchfallen müsste. Gemessen: 8.715 → **6.223**. Warten
> kostet Zeit am Anfang und spart mehr davon in der Mitte, weil eine volle Welle
> nicht aufgerieben wird. Die Spalte war trotzdem nötig — nur nicht aus dem
> Grund, der vorher aufgeschrieben war.

> **Ein Sammelpunkt weiter vorn ist nicht besser, sondern schlechter.** Bei
> `stagingDistance 70` (zwei Drittel des Weges) liegt das Austauschverhältnis bei
> 83 statt 105 (`wave-6-far`). Der Grund ist beim Zusehen offensichtlich: Wer
> weit vorn sammelt, hat den gefährlichen Teil des Weges **allein** gemacht.
> Sammeln gehört nach Hause.

> **Die Wellengrösse wirkt monoton, und das Optimum liegt am Rand.** Einseitig
> über fünf Grössen gemessen (Kandidat als Slot 0, Austauschverhältnis gegen die
> Referenz 138): 4 → 133, 6 → 147, 8 → 153, 10 → 200, 12 → **222**. Wer hier
> „einen mittleren Wert wählen" will, wählt gegen die Messung.

> **Eine Toleranz um die Sammelzelle taugt NICHT als Wellenzähler.** Die erste
> Fassung zählte nur Einheiten innerhalb von vier Zellen um den Sammelpunkt. Die
> Formationsverteilung streut zwölf Einheiten weiter als das, die Zahl erreichte
> die Wellengrösse nie, und die Armee pendelte **11.000 Ticks lang** zwischen
> Basis und Sammelpunkt, während eine einzelne feindliche Einheit ihr HQ
> abtrug — 0 eigene Verluste, 127 gegnerische, und trotzdem verloren. Gefunden
> hat das nicht ein Test, sondern der aufgezeichnete Lauf: fünf Zeilen
> Einheitenpositionen über die Zeit.

> **Ankommen erzeugt Rauschen, wenn man es nicht abfängt.** `UnitState.Stop()`
> löscht bei Ankunft `TargetGridPos`, womit die Doppelbefehl-Unterdrückung nicht
> mehr greift und dieselbe Marschzelle jede Kadenz neu befohlen wird: gemessen
> 40 statt 23 Aktionen pro Minute **für stillstehende Einheiten**. Eine
> angekommene Einheit bekommt jetzt gar keinen Befehl. Das ist derselbe
> Fehlermodus wie bei `DefendBase` (V002, +23 % Intents) — er entsteht hier
> nicht aus Zielwechseln, sondern aus einem gelöschten Feld.

### Offen

- **Die frühe Gegenstrategie.** Ein Angriff vor der zwölften Einheit trifft eine
  wartende Armee. Das gehört in die nächste gespielte Partie und ist der Grund,
  warum `DefendBase` (NEXT-STEPS §3) durch diese Änderung **wichtiger** wird,
  nicht unwichtiger.
- **Kein Spielbericht.** Wie bei V001: was nicht gespielt wurde, steht als
  ungesehen im PR.
- Die Kopplung `waveSize == TargetArmySize` ist im Test festgehalten, aber sie
  ist eine Setzung. Ob eine Welle unterhalb der Obergrenze mit einer **grösseren**
  Obergrenze besser wäre, ist ungemessen.

---

## M001 · 2026-08-09 · **Methodenbefund** — V002 und V003 wurden im Selbstspiel beurteilt

**Status:** kein Lauf, eine Feststellung über den Messaufbau ·
**Betrifft:** [V002](#v002--2026-08-08--defendbase--gebaut-gemessen-verworfen),
[V003](#v003--2026-08-08--zielen-unter-der-angriffsschwelle--vier-varianten-verworfen-die-form-bleibt)

Beide Rückweisungen stützen sich auf Messungen an `ms1-canonical` **gegen sich
selbst**. Eine Verhaltensregel steckt aber im Binary, nicht im Profil — also
bekommen in dieser Partie **beide** KIs sie gleichzeitig.

> Zwei Armeen, die beide besser zielen oder beide heimlaufen, liefern eine
> längere und blutigere Partie. Genau das wurde gemessen. **„Später
> entschieden, mehr Verluste" heisst im Selbstspiel nicht „schlechtere KI" —
> es kann „zwei stärkere Armeen" heissen, und der heutige Aufbau kann die
> beiden Fälle nicht unterscheiden.**

`compare` hilft nicht von allein: Kandidaten unterscheiden sich dort
ausschliesslich in Zahlen, eine Coderegel erreicht ebenfalls beide Seiten.

**Abhilfe, und sie war einen Profilwert entfernt.** Jedes neue Verhalten
bekommt eine **Aus-Stellung** (`waveSize: 1`, `retreatEnterHealthPercent: 0`,
`defenseRadiusCells: 0`). Dann spielt dasselbe Binary „mit" gegen „ohne",
einseitig, in einem `compare`-Lauf. V002 hatte das Feld bereits und hat es
nicht so benutzt — es verglich zwei komplette Messmengen vor/nach dem Umbau.

**Was das für die beiden Einträge heisst:** Ihre Zahlen stimmen, ihre
Schlussfolgerung ist **offen**. Weder „DefendBase kostet" noch „Zielen unter
der Schwelle kostet" ist damit widerlegt — aber auch nicht mehr belegt. Wer
einen der beiden wieder aufgreift, misst ihn einseitig, und zwar bevor er
irgendetwas umbaut.

Ausführlich in [`../NEXT-STEPS.md`](../NEXT-STEPS.md) §7, samt der vier
Kennzahlen, die Spielgefühl überhaupt abbilden können.

---

## B001 · 2026-08-08 · **Gespielte Beobachtung** — Mensch gegen KI

**Quelle:** Partie am Rechner, kein Laborlauf · **KI-Verhalten:** `r2.A037B84D`

> [!IMPORTANT]
> Dies ist die **einzige** Eintragsart in diesem Journal, die kein Laborlauf
> ist — und die einzige, die im Sinne des Repos etwas *beweist*. Alles andere
> hier ist Diagnose. Beobachtungen bekommen deshalb ein eigenes Kürzel `B`
> statt `V`: Sie messen nichts nach, sie stellen fest.

Vier Beobachtungen, wörtlich und dann eingeordnet:

**1. „Manchmal kommen sie in kleiner Gruppe, oft danach einzeln."**
Bestätigt NEXT-STEPS §1 aus der Spielersicht. Die erste Welle ist die Armee,
die bei Erreichen der Schwelle stand; alles danach ist Nachschub, der einzeln
losläuft. Im Labor sah man davon nur eine gleichmässig steigende Verlustkurve.

**2. „Rennen immer auf Headquarter."**
Das ist kein Zufall und kein Balancing-Problem, das ist eine Zeile: Ist das
feindliche HQ sichtbar, bricht `FindBestVisibleEnemyByScore` sofort ab und
liefert es zurück — und die Marschzelle wird die HQ-Zelle. Ist gar nichts
sichtbar, marschiert die Armee zum entferntesten Aetherium-Feld, und das liegt
neben der Basis. **Beide Wege zeigen auf denselben Punkt.**

> **Diese Beobachtung widerlegt eine Entscheidung aus V001.** Dort steht:
> „Das feindliche HQ bleibt ein Kurzschluss und ist bewusst kein Gewicht: sein
> Verlust entscheidet die Partie (D-077). Eine Siegbedingung ist keine
> Vorliebe." Das ist als Regel richtig und als *Verhalten* falsch. Es macht die
> KI vollständig vorhersagbar und schickt sie durch jede Verteidigung, die
> zwischen ihr und dem HQ steht. Der Kurzschluss gehört durch ein hohes
> **Gewicht** ersetzt — hoch genug, dass ein freiliegendes HQ gewinnt, nicht so
> hoch, dass ein verteidigtes alles andere überstimmt.

**3. „Halten keinen Abstand zu meinen Fernkampfangreifern."**
Die Laborzahl dazu ist der Standoff-Überlauf, aber sie misst die *eigene*
Reichweite. Der Spieler beschreibt die andere Hälfte: Die KI erkennt die
Reichweite des **Gegners** überhaupt nicht und läuft in sie hinein. Im Labor
bisher nicht gemessen — es gibt kein Szenario dafür.

**4. „Laufen einfach straight line, anstatt diese zu umlaufen, solange
Leben sparen sich mehr rentiert als der Umweg, den sie nehmen müssten."**
Das ist die präziseste Formulierung des Problems in diesem ganzen Journal, und
sie enthält bereits die Regel: **Umweg nehmen, solange der Umweg billiger ist
als die Verluste auf der geraden Linie.** Beides ist ganzzahlig rechenbar —
Zellen gegen erwarteten Schaden — und braucht keinen Zufall und kein Gedächtnis.

### Was daraus folgt

- NEXT-STEPS §2 wird um den HQ-Kurzschluss erweitert; er ist die Ursache, nicht
  nur `GetEnemyStartAreaCell`.
- Die Kostenregel aus Beobachtung 4 wird die Formulierung, gegen die gebaut wird.
- Ein Laborszenario für Beobachtung 3 fehlt: eine Gruppe läuft gegen einen
  **stehenden Fernkämpfer** und die Frage ist, wie viel Schaden sie auf dem Weg
  frisst. Das ist ein `movement`-Szenario, kein Duell.

---

## V003 · 2026-08-08 · Zielen unter der Angriffsschwelle — vier Varianten, **verworfen**; die Form bleibt

**Lauf:** [`runs/20260808-2153-206d8bc5.md`](runs/20260808-2153-206d8bc5.md) ·
**Variantenlauf (a):** [`runs/20260808-2146-206d8bc5.md`](runs/20260808-2146-206d8bc5.md) ·
**Status:** im Labor gemessen, **Verhaltensteil zurückgenommen**, Formänderung bleibt ·
**Basis:** V001/V002-Stand (`0x5D8FB2D45FFD16B6`, Tick 8.715)

Dieser Eintrag hat zwei Hälften, die man auseinanderhalten muss: eine
**Formänderung, die bleibt und nichts am Verhalten ändert**, und eine
**Verhaltensregel, die gebaut, viermal gemessen und wieder ausgebaut wurde.**

### Was bleibt: Schritt (6) in drei Stufen

`SkirmishAiSystem` erteilt keinen Armeebefehl mehr, sondern löst auf:

```
ArmyPosture posture = ResolveArmyPosture(...);     // was tut die Armee
foreach (Einheit)  UnitAssignment a = ResolveUnitAssignment(...);   // was tut diese Einheit
SubmitAssignments(...);                            // gleiche Befehle → ein Intent
```

Die Regeln sind dabei **unverändert** geblieben; der Nachweis ist keine
Testzusage, sondern eine Zahl: Entscheidungstick **8.715**, Endzustand
**`0x5D8FB2D45FFD16B6`**, Hash-Kette und `result.json` byte-identisch zur
Referenz bis auf `elapsedMilliseconds`. `AiBehaviorId.Revision` bleibt deshalb
bei **2** — die Regel dort sagt „bumpen, wenn sich Verhalten ändert", und hier
ändert sich keins.

Der Zweck: „diese eine Einheit dreht ab", „Nachschub wartet am Sammelpunkt"
und „zielen, bevor die Schwelle steht" sind in einem Armeebefehl **nicht
formulierbar**. Alle drei stehen in NEXT-STEPS.

### Was gebaut und wieder ausgebaut wurde

Die Angriffsschwelle sollte nur noch den *Vormarsch* regeln, nicht das
*Zielen* (NEXT-STEPS §4). Vier Fassungen, alle deterministisch (Exit 0), alle
auf der Referenzpartie `ms1-canonical` gegen sich selbst, Seed `0xA17E57DE57`:

| Fassung | Entscheidungstick | Verluste 0/1 | Intents 0/1 |
|---|---:|---:|---:|
| **r2 — kein Zielen unter der Schwelle** | **8.715** | **70 / 97** | **343 / 363** |
| (a) je Einheit ihr bestes Ziel in eigener Reichweite | 10.847 | 90 / 122 | 451 / 468 |
| (a′) wie (a), ohne HQ-Kurzschluss | 10.855 | 90 / 126 | 455 / 474 |
| (b) **ein gemeinsames** Ziel, nur wer es erreicht | 9.664 | 77 / 107 | 397 / 382 |
| (c) nur korrigieren: unbewaffnetes Ziel → bewaffnetes | 10.831 | 89 / 124 | 421 / 446 |

### Schlechter

Alle vier kosten auf allen drei gemessenen Achsen. Die beste Fassung (b) liegt
immer noch 11 % später und bei 10 % mehr Verlusten je Slot als gar kein Zielen.

### Warum — der Befund, der den Aufwand wert war

**Ein expliziter Angriffsbefehl ist eine Einbahnstrasse.** Im Code nachgesehen,
nicht vermutet: `UnitState.AttackTarget` wird an genau drei Stellen geschrieben
— vom Befehl `AttackTarget`, von der D-087-Auto-Acquisition **in ein leeres
Feld**, und beim Tod des Ziels. `UnitState.Stop()` lässt es unangetastet,
obwohl der Kommentar an der Anwendungsstelle „Stop cancels every standing
order" behauptet.

Daraus folgt: Wer einer **stehenden** Einheit ein Ziel gibt, nimmt sie
dauerhaft aus der Automatik. Läuft das Ziel aus der Reichweite, hält die
Einheit den Befehl (`CombatSystem` Phase 3 hält, statt zu verwerfen) und
**feuert nicht mehr** — die Automatik kann nicht einspringen, weil das Feld
belegt ist. Die Automatik ist reaktiv und wird nie schal; ein Befehl wird es
mit jedem Tick. Solange die Einheit auf ihr Ziel **zuläuft**, ist der Befehl
besser; sobald sie steht, ist er schlechter.

Genau deshalb bleibt das Zielen oberhalb der Schwelle unverändert richtig: dort
marschiert die Armee auf ihr Ziel zu.

### Widerlegt

> **Der HQ-Kurzschluss ist nicht die Ursache.** (a) 10.847 gegen (a′) 10.855 —
> Rauschen. Wer die Regel wieder aufgreift, braucht einen anderen Ansatz.

> **Feuerverteilung erklärt den grössten Teil.** Ein gemeinsames Ziel (b) holt
> gegenüber „jede Einheit ihr eigenes" (a) 1.183 Ticks und 13 / 15 Verluste
> zurück. Konzentration bleibt richtig, auch stehend.

> **„Möglichst wenig eingreifen" ist hier falsch.** Fassung (c) greift am
> seltensten ein und liegt trotzdem schlechter als (b) — weil jedes einzelne
> Eingreifen eine dauerhafte Sperre erzeugt, unabhängig davon, wie selten es
> passiert. Die Zahl der Sperren zählt, nicht die Zahl der Befehle.

### Unverändert

- **Determinismus:** jede Fassung zweimal gefahren, Exit 0.
- `intentsRejected` bleibt **0** in allen Fassungen.
- Duell-Arena und Bewegungsszenarien byte-identisch (sie fahren kein KI-System).
- **87/87 Labortests, 559/559 SimRunner-Tests grün** — im Endzustand wie in
  jeder Zwischenfassung.

### Einschränkung, die zum Befund gehört

Gemessen wurde **symmetrisch KI gegen KI**. Beide Slots zielen schlechter oder
besser gleichzeitig; was eine bessere Zielwahl gegen einen Menschen wert ist,
kann dieses Labor nicht zeigen — dieselbe Einschränkung wie bei V002.

### Offen — und es ist eine Anfrage, keine Umsetzung

Damit „zielen, ohne zu marschieren" überhaupt gewinnen kann, braucht es einen
Weg, ein Ziel wieder **freizugeben**. Der naheliegende Kandidat ist `Stop`,
das laut eigenem Kommentar bereits jeden stehenden Befehl löscht, `AttackTarget`
aber auslässt. Das liegt in `Simulation/State/` — **fremdes Terrain, gesperrt.**
Befund abgelegt unter [`findings/F001-stop-loescht-attacktarget-nicht.md`](../findings/F001-stop-loescht-attacktarget-nicht.md);
der Weg nach draussen ist Mail oder Issue, kein PR.

---

## V002 · 2026-08-08 · `DefendBase` — gebaut, gemessen, **verworfen**

**Status:** im Labor gemessen, **Code zurückgenommen** · **Basis:** V001 (`3f596d6`)

Kein Commit im Code. Dieser Eintrag existiert, damit die Regel nicht in drei
Monaten ein zweites Mal gebaut wird.

### Was gebaut wurde

Ein Feld `DefenseRadiusCells` (16) im Profil und ein Zweig in Schritt (6) von
`SkirmishAiSystem`: Ist ein **bewaffneter** Feind innerhalb des Radius um das
eigene HQ sichtbar, wählt die Armee ihr Ziel aus diesem Bereich und marschiert
dorthin — **ohne** Angriffsschwelle, weil Verteidigen nichts ist, wofür ein Slot
zu klein sein darf. Ein unbewaffneter Harvester am Zaun löste bewusst nichts
aus. Determinismus geprüft, 95 Kettenglieder identisch, Exit 0.

### Schlechter — auf jeder gemessenen Achse

| | V001 ohne | V002 mit |
|---|---:|---:|
| Entscheidungstick | 8.715 | 9.470 |
| Verluste | 70 / 97 | 75 / 107 |
| Intents je 1000 Ticks | 39,4 / 41,7 | 44,3 / **51,4** |
| `late-push` | 8.228 Ticks, 72 verloren | **13.953 Ticks, 146 verloren** |

Siegquoten aller sechs Kandidaten: **unverändert**. Die Verteidigung kostet
also und liefert nichts zurück, was diese Szenarien messen können.

### Widerlegt

> **Am Radius liegt es nicht.** Gemessen: 8 → Tick 9.564, 16 → 9.470,
> 24 → **byte-identisch zu 16** (die Kämpfe finden ohnehin innerhalb von 16
> Zellen statt). Alle drei liegen über den 8.715 ohne Verteidigung. Wer die
> Regel wieder aufgreift, braucht einen anderen Ansatz, keine andere Zahl.

> **Das Pendeln ist die wahrscheinlichste Ursache.** +23 % Intent-Rauschen bei
> Slot 1 heisst: es werden mehr Befehle neu erteilt, nicht mehr Ziele getroffen.
> Die Armee wird zwischen Front und Basis hin- und hergezogen. Der Plan sieht
> dagegen `switchHysteresis` vor (§4.6) — ein Zielwechsel verlangt Vorsprung,
> statt bei jedem Sichtkontakt umzuschalten. Ohne Hysterese ist ein zweiter
> Anlauf sinnlos.

### Einschränkung, die zum Befund gehört

**Gemessen wurde symmetrisches KI-gegen-KI.** Beide Slots verteidigen, also
bleiben beide zu Hause und die Partien ziehen sich. Gegen einen Menschen, der
die Basis wirklich überfällt, könnte dieselbe Regel wertvoll sein — das kann
dieses Labor nicht zeigen. „Kostet in KI-gegen-KI" ist nicht „nutzlos".

### Nachweis des Rückbaus

Nach dem Zurücknehmen liefert die Referenzpartie wieder Tick **8.715** und
Endzustand **`0x5D8FB2D45FFD16B6`**, und alle Artefakte sind byte-identisch zu
V001 bis auf `elapsedMilliseconds` — Wanduhr, keine Simulationsausgabe.

---

## V001 · 2026-08-08 · Score-Targeting statt Reihenfolge

**Lauf:** [`runs/20260808-2035-ab6cb9a1.md`](runs/20260808-2035-ab6cb9a1.md) ·
**Vorher-Lauf:** [`runs/20260808-1945-3b3f27d7.md`](runs/20260808-1945-3b3f27d7.md) ·
**Status:** im Labor gemessen, **im laufenden Spiel ungesehen** (Linux-Build steht aus)

### Was genau geändert wurde

| Ort | Änderung |
|---|---|
| `AI.Data/AiProfile.cs` | vier neue Ganzzahlfelder: `TargetDamageWeight`, `TargetThreatWeight`, `TargetFinishWeight`, `TargetDistanceWeight` — ohne Konstruktor-Vorgabewerte, damit kein Wert stillschweigend driften kann |
| `AI.Data/AiProfiles.cs` | `ms1-canonical` und `legacy-defaults` bekommen `dmg 10, threat 6, finish 3, dist 4` (Planskizze §4.6). **Die ersten Zahlen hier, die keine Kopie einer ausgelieferten Konstante sind** — Zielbewertung gab es vorher nicht |
| `AI/SkirmishAiSystem.cs` | `FindPreferredVisibleEnemy` → `FindBestVisibleEnemyByScore` plus `ScoreTarget` |
| `AI/AiFactionProfile.cs` | Alt-Konstruktor reicht die vier Gewichte aus dem ausgelieferten Profil durch |

Die Formel, ausschliesslich ganzzahlig:

```
score = W_dmg    · Ø DamageMatrix.Resolve(Waffe_i, Rüstungsklasse Ziel)
      + W_threat ·   Waffenschaden des Ziels
      + W_finish ·   fehlende Lebenspunkte in Prozent
      - W_dist   · Ø Chebyshev-Abstand Armee → Ziel
Gleichstand → niedrigere rohe Entity-Id
```

Drei Festlegungen, die nicht aus der Planskizze kommen:

- **Der Score gilt der Armee, nicht der Einzeleinheit**, weil dieser Aufruf
  *ein* gemeinsames Ziel liefert. Für die heute homogene Armee ist die
  Reihenfolge identisch zur Pro-Angreifer-Formel: das Mittel über n gleiche
  Angreifer *ist* der Einzelwert. Gemischte Armeen mitteln.
- **Das feindliche HQ bleibt ein Kurzschluss** und ist bewusst kein Gewicht:
  sein Verlust entscheidet die Partie (D-077). Eine Siegbedingung ist keine
  Vorliebe, die ein Gewicht überstimmen darf.
- **Eigene Einheiten werden hier gefiltert** — und nur hier. `ValidateDomain`
  hat für `AttackTarget` keinen Case, die Feuerphase prüft Reichweite und
  Sicht, aber nie den Besitzer. Ein expliziter Befehl auf eine eigene Einheit
  würde feuern.

### Besser

Referenzpartie `ms1-canonical` gegen sich selbst, Seed `0xA17E57DE57`:

| Kennzahl | vorher | nachher | |
|---|---:|---:|---|
| Entscheidungstick | 12.975 | **8.715** | −33 % |
| Verluste Slot 0 | 113 | **70** | −38 % |
| Verluste Slot 1 | 137 | **97** | −29 % |
| Eingereichte Intents | 443 / 578 | 343 / 363 | weniger Befehlsrauschen |

Beide Seiten verlieren weniger, obwohl beide dasselbe neue Zielverhalten
fahren — die Armee schiesst auf das, was sie tatsächlich beschädigen kann,
statt auf das erste sichtbare Gebäude. `late-push` entscheidet 4.370 Ticks
früher bei 43 % weniger Verlusten (126 → 72) und hält seine 100 %.

### Schlechter

**Die Änderung ist nicht für jedes Profil eine Verbesserung** — und genau
deshalb steht sie hier:

| Kandidat | Siegquote | Entscheidungstick | Verluste |
|---|---|---|---|
| `early-push` | 50 % → 0 % | 16.299 → 15.401 | 156 → 150 |
| `greedy-economy` | 50 % → 50 % | 8.635 → **11.470** | 86 → **114** |
| `fast-cadence` | 50 % → 50 % | 11.454 → **12.948** | 110 → **132** |

**Nachgesehen statt vermutet — und die erste Zeile trägt nicht.** „50 % → 0 %"
klingt nach Einbruch und ist eine einzige gekippte Partie. `early-push` spielt
zwei Partien: eine verliert es vorher wie nachher nach rund 7.200 Ticks. Die
andere ist ein Zermürbungskrieg über **23.500 von 27.000 Ticks** — 87 % des
Budgets, über 470 tote Einheiten zusammen — und *die* kippte von Slot 0 auf
Slot 1. In der aufgezeichneten Partie verliert `early-push` dabei sogar
**weniger** Einheiten als der Gegner (211 gegen 264).

Das ist keine Eigenschaft der Score-Formel, sondern die Auflösungsgrenze des
Messaufbaus: **bei zwei Partien je Kandidat ist ein Siegquotensprung von 50
Punkten genau eine Partie.** Die Seed-Achse ist leer, es gibt also keine
Streuung, aus der sich das herausmitteln liesse. `greedy-economy` und
`fast-cadence` sind aus demselben Grund Einzelmessungen — ihre Tick- und
Verlustzahlen sind echt, aber sie sind je *eine* Beobachtung, keine Tendenz.

Ausserdem sinkt in der Referenzpartie der Endkassenstand (33.460 / 38.810 →
21.790 / 25.020) und die Armee am Ende ist kleiner (7/7 → 7/4). Beides ist
Folge der kürzeren Partie und nicht für sich schlecht — aufgeschrieben, damit
niemand es später als eigenständigen Effekt liest.

### Unverändert

- `intentsRejected` bleibt **0 von 706**. Kein einziger Befehl läuft gegen eine
  Executor-Regel.
- **Determinismus**: zwei Läufe desselben Specs stimmen auf allen 88
  Kettengliedern überein, Exit-Code 0.
- Duell-Arena und Bewegungsszenarien: **byte-identisch**. Sie fahren kein
  KI-System, das ist die Gegenprobe, dass nichts ausserhalb der KI verrutscht ist.
- **557/557 SimRunner-Tests und 87/87 Labortests grün.**

### Widerlegt

> **Die vier Determinismus-Baselines werden von einer reinen KI-Änderung
> NICHT rot.** Das war die Erwartung — im Plan (§8, E7) und in `AGENTS.md` —
> und sie stimmt nicht. `SnapshotGoldenBytesTests`, `CommandGoldenBytesTests`,
> `SimRandomGoldenTests` und `Determinism10000Tests` erwähnen `SkirmishAi` mit
> keiner Zeile; ihre Szenarien fahren kein KI-System. Rot wird von einer
> KI-Änderung nur, was die KI auch ausführt.
>
> Die Trennungsregel gilt trotzdem unverändert: `check_baseline_guard.py` führt
> `Scripts/AI/` in seinen Simulationspfaden, ein PR mit Verhalten **und**
> Baseline wird also weiterhin maschinell abgelehnt. Nur die Begründung „er
> wäre ohnehin rot" trägt hier nicht.

> **Eine Siegquote aus `compare` hat drei mögliche Werte: 0 %, 50 %, 100 %.**
> Zwei Partien je Kandidat, keine Seed-Streuung — jeder Sprung ist eine
> gekippte Partie, und eine Partie, die 87 % des Tickbudgets läuft, kippt an
> allem. Wer aus dieser Spalte eine Tendenz liest, liest Rauschen. Brauchbar
> wird sie erst mit einer echten Varianzquelle; bis dahin sind
> Entscheidungstick und Verlustzahlen die belastbareren Spalten, und selbst
> die sind Einzelmessungen.

> **`SkirmishAiTests` fängt eine Zielverhaltensänderung nicht.** Der
> End-to-End-Test prüft Ausgang, Sieger und „mindestens 6 Infanteristen", nicht
> den Entscheidungstick. Er blieb grün, während sich die Partie um 4.260 Ticks
> verschob. **Geschlossen** durch
> `SkirmishAi_ShootsTheDangerousTarget_NotTheFirstOneInTheVisibleList`: ein
> Harvester (zuerst gespawnt, also niedrigerer Entity-Index) und ein
> BattleTank erscheinen gleichzeitig neben der Armee — die alte Regel hätte
> den Harvester gewählt. Der Test ist nachweislich tragend: mit
> `targetThreatWeight: 0` fällt er.

> **Unterhalb der Angriffsschwelle gibt es überhaupt kein KI-Zielverhalten.**
> Beim Schreiben des Tests gemessen: mit fünf Einheiten (Schwelle 6) reicht die
> KI **keinen einzigen** `AttackTarget`-Befehl ein. Was die Einheiten tragen,
> ist ausschliesslich D-087-Auto-Acquisition, und die nimmt das *nächste*
> sichtbare Ziel, nicht das gefährlichste: **vier von fünf schiessen auf den
> Harvester, während der Panzer eine Zelle daneben steht.** In dem Moment, in
> dem die sechste Einheit fertig wird, springen alle fünf auf den Panzer.
> Der Score regiert also erst ab der Schwelle — darunter regiert die
> Entfernung. Das ist kein Fehler dieser Änderung, sondern eine Lücke, die
> vorher niemand sehen konnte.

### Offen

- ~~Warum kippt `early-push`?~~ **Nachgesehen, siehe „Schlechter":** eine
  gekippte Partie an der Auflösungsgrenze, keine Eigenschaft der Formel. Was
  offen *bleibt*: warum diese Paarung überhaupt 23.500 Ticks braucht. Zwei
  Armeen, die sich gegenseitig endlos nachbauen und aufreiben, ohne dass eine
  Seite die Basis der anderen erreicht — das ist ein Verhaltensbefund für
  sich, unabhängig vom Zielverhalten.
- **Ziel je Einheit** statt ein Armeeziel. Heute ohne Unterschied (homogene
  Armee), sobald die KI Fahrzeuge baut nicht mehr.
- **Die Lücke unterhalb der Schwelle.** Zwischen dem ersten Soldaten und dem
  sechsten schiesst die Armee nach Entfernung statt nach Gefahr. Ob das
  überhaupt stört, ist offen — verteidigen muss sie in dieser Phase ohnehin,
  und `DefendBase` aus E7 wird genau dort ansetzen.
- **Kein Spielbericht.** E7 ist erst mit einer echten Partie fertig, inklusive
  eines Falls, in dem die Reaktion falsch war. Das hängt am Linux-Build, der
  Bringschuld des Netzstrangs ist.
- Die vier Gewichte sind ungetunt aus der Planskizze übernommen. `compare` kann
  sie jetzt variieren — `LabProfiles.Derive` kennt sie.

---

## V000 · 2026-08-08 · Ausgangslage

**Lauf:** [`runs/20260808-1945-3b3f27d7.md`](runs/20260808-1945-3b3f27d7.md) ·
**Status:** Referenz, kein Eingriff

Das Verhalten, gegen das V001 gemessen wurde: `ms1-canonical` mit den acht
Werten, die das Spiel ausliefert, Zielwahl nach „HQ, sonst erstes sichtbares
Gebäude, sonst erste sichtbare Einheit". Entscheidung bei Tick 12.975,
Endzustand `0x4947D4769384585C`, Verluste 113 / 137.

Die Etappen E0–E6 stehen im Plan
([`docs/feature-ideas/AiSimulationEnvironment.md`](https://github.com/arn-c0de/Project_Nova/blob/lab/ai-simulation/docs/feature-ideas/AiSimulationEnvironment.md)
§9) und werden hier nicht wiederholt: sie haben das Labor gebaut, nicht das
Verhalten geändert. E6 hat die Zahlen aus dem Code in Daten verschoben —
nachweislich verhaltensneutral, denn die Baselines blieben grün.
