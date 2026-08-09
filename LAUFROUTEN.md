# Laufrouten in der Auswertung — Idee, nicht gebaut

**Notiert am:** 2026-08-09 · **Status:** Idee, kein Code, kein PR

Frage, aus der das hier entstand: Kann man bei der Auswertung eines Laufs auch
nachverfolgen, **wo eine Einheit langgelaufen ist** — nicht nur, wo sie zu einem
Zeitpunkt stand?

Antwort: ja, aber nicht rückwirkend sauber. Es fehlt genau eine Zahl.

## 1 · Warum es heute nicht geht

`view.ndjson` schreibt pro Frame je Entity neun Integer
(`View/ViewFrame.cs:106-114`):

```
[slot, shape, x, y, healthPercent, flags, line, lineX, lineY]
```

Position ist da, **Identität nicht**. Ohne eine ID lässt sich ein Eintrag in
Frame *n* nicht mit einem Eintrag in Frame *n+1* verknüpfen. Was übrig bleibt,
ist eine Punktwolke pro Tick — kein Weg.

Was hingegen schon vorhanden wäre, sobald die Verknüpfung existiert:

- `IsMoving` steckt als `ViewFlags.Moving` in `flags` (`ViewRecorder.cs:135`)
- `GoalGridPos` wird als Endpunkt der blauen Move-Linie bereits mitgeschrieben
  (`ViewRecorder.cs:166-171`)

Man hätte pro Sample also **Ist-Position und Ziel nebeneinander**. Das ist genau
das Paar, aus dem Umwegfaktor und Zielwechsel fallen.

## 2 · Drei Wege, aufsteigend nach Aufwand

### A · Rückwirkend auf vorhandene Läufe, ohne Codeänderung

Routen offline aus `view.ndjson` rekonstruieren: Nearest-Neighbour zwischen
zwei aufeinanderfolgenden Frames, getrennt nach `slot` + `shape`.

Überschlag an `out/compare/runs/ms1-canonical/view.ndjson`: die Verschiebung
liegt bei rund 2–3 Zellen pro 50-Tick-Frame. Für Harvester und Builder — weit
verteilt, wenige pro Fläche — trägt das. In einem Armeeklumpen mit fünfzehn
Einheiten auf vier Zellen vertauscht das Verfahren Spuren, **ohne dass man es
der Zeichnung ansieht**. Eine falsch zusammengesetzte Route ist schlimmer als
gar keine: sie sieht aus wie eine Beobachtung.

Taugt als Skizze auf bereits gerechneten Läufen. Nicht als Befund.

### B · Die saubere Variante — `Id` ins Frame aufnehmen

Ein `uint` je Entity, die rohe Entity-ID. `UnitCommandStateView.ToRawEntityId(u.Id)`
wird in `BuildEntity` ohnehin schon geholt (`ViewRecorder.cs:103`) und dort nur
für die Baustellenabfrage benutzt. Sie kodiert Index und Version, ist über die
Lebenszeit einer Einheit stabil und macht einen wiederverwendeten Pool-Slot als
**neue** Einheit erkennbar — dieselbe Eigenschaft, auf der `TraceCollector`
seine Verlustzuordnung aufbaut.

Kosten: eine Zahl pro Entity pro Frame, grob +10 % Dateigrösse. Danach sind
Routen exakt statt geraten, und der HTML-Player kann eine Trail-Ebene neben Fog
bekommen: die letzten N Positionen einer Einheit als verblassende Linie,
abschaltbar wie die anderen Ebenen.

Zum Scope: Der Recorder bleibt reiner Beobachter — liest nach `StepTick()`,
schreibt nie zurück, steht nicht in der Tickreihenfolge, nicht im State-Hash,
nicht im Snapshot (`ViewRecorder.cs`, Klassenkommentar). Die Hashkette bleibt
identisch, **keine Baseline wird davon rot**. Auch `--view-every 5` oder `10`
ändert daran nichts — und für eine gezielte Bewegungsuntersuchung braucht es
das, die voreingestellten 50 Ticks sind für eine Route grob.

### C · Die Auswertung, die es erst nützlich macht

Aus den Spuren drei Integer-Spalten je Slot, in derselben Machart wie
`Metrics/FeelMetrics.cs` — vier weitere Spalten, die ein Mensch liest, kein
Score, keine Gewichtung:

| Spalte | Was sie beantwortet |
|---|---|
| **Umwegfaktor** — gelaufene Streckenlänge gegen Luftlinie Start→Ziel, in Prozent | „Bewegung, die am Ziel nicht dumm aussieht" |
| **Stillstand trotz `Moving`** — Ticks mit gesetztem Flag und unveränderter Position | gegenseitiges Blockieren, gemessen statt vermutet |
| **Richtungs- und Zielwechsel je Einheit** | Zappeln vor einer Gebäudeecke gegen einen sauberen Bogen |

Die zweite Zeile ist der eigentliche Grund, das zu bauen: „kein gegenseitiges
Blockieren" ist Auftrag (`CLAUDE.md` §1, `Simulation/Movement/`), und es gibt
heute keine Zahl dafür.

Nebenbei wird damit Schritt 6 aus `NEXT-STEPS.md` („Annäherung über eine Route
statt der Luftlinie", Messkriterium *zweimal dieselbe Partie, zwei verschiedene
Wege*) überhaupt erst bewertbar — ohne Spuren lässt sich das Kriterium nicht
prüfen.

## 3 · Empfehlung

B und C. A nur, wenn vor einer Neurechnung sofort etwas auf den vorhandenen
Läufen unter `out/compare/runs/` sichtbar sein soll — und dann mit dem Vermerk,
dass die Zuordnung geraten ist.

B ist eine kleine, in sich geschlossene Änderung an Frame, Recorder und Player.
C ist die eigentliche Arbeit.

## 4 · Was hier offen bleibt

- Trail-Länge im Player: feste Anzahl Frames oder feste Tickspanne? Bei
  wechselndem `--view-every` bedeutet dasselbe N zwei verschiedene Dinge.
- Umwegfaktor braucht einen Start: der Tick, an dem `Moving` gesetzt **und**
  `GoalGridPos` zuletzt gewechselt hat. Ein Ziel, das mitten im Lauf umspringt,
  ist ein neues Segment, kein Umweg — sonst misst die Spalte Zielwechsel statt
  Wegqualität.
- Nichts davon ist im laufenden Spiel gesehen. Es ist Papier.
