# Laufrouten in der Auswertung — gebaut

**Notiert am:** 2026-08-09 · **Status:** gebaut, im Labor · kein PR nach Project_Nova

Frage, aus der das hier entstand: Kann man bei der Auswertung eines Laufs auch
nachverfolgen, **wo eine Einheit langgelaufen ist** — nicht nur, wo sie zu einem
Zeitpunkt stand?

Antwort: ja. Es fehlte genau eine Zahl, und die ist jetzt da.

## 1 · Warum es vorher nicht ging

`view.ndjson` schrieb pro Frame je Entity neun Integer:

```
[slot, shape, x, y, healthPercent, flags, line, lineX, lineY]
```

Position war da, **Identität nicht**. Ohne eine ID liess sich ein Eintrag in
Frame *n* nicht mit einem Eintrag in Frame *n+1* verknüpfen. Was übrig blieb,
war eine Punktwolke pro Tick — kein Weg.

## 2 · Was gebaut wurde

### Die ID, angehängt statt eingeschoben

`ViewEntity.Id` trägt die rohe Entity-ID (`Index` + `Version`) als **zehnte**
Spalte. Angehängt, damit eine `view.ndjson` von vorher in den ersten neun
Spalten korrekt bleibt; die Seite schaltet die Spuren ab, wenn die zehnte
fehlt, statt eine Route zu zeichnen, die sie nicht kennen kann.

Die Version macht einen wiederverwendeten Pool-Slot als **neue** Einheit
erkennbar — dieselbe Eigenschaft, auf der `TraceCollector` seine
Verlustzuordnung aufbaut.

### `tracks.ndjson` — jeder Tick, verlustfrei

`View/EntityTrackRecorder.cs`, aufgerufen in **jedem** Tick:

```
{"t":123,"a":[[id,x,y]],"d":[[id,dx,dy]],"x":[id]}
```

`a` absolut (neue IDs und Keyframes), `d` Delta gegen die letzte Position
derselben ID, `x` beendete Spuren. Wer sich nicht bewegt hat, steht in keiner
Liste und behält seine Position. Alle 500 Ticks eine Keyframe-Zeile, damit die
Seite springen kann, ohne von Tick 0 an nachzurechnen.

**Der Kompromiss aus der ursprünglichen Notiz war keiner.** Die Sorge galt der
Dateigrösse — gemessen trägt ein entschiedener Lauf 28 bis 37 Einheiten, nicht
Hunderte. Ein Lauf über 6000 Ticks kostet 840 KB Spur neben 855 KB Sichtframes.
Deshalb wird verbatim aufgezeichnet: nicht geglättet, nicht interpoliert, kein
Schwellwert. Die Nearest-Neighbour-Rekonstruktion aus der alten Fassung §2A
bleibt verworfen — eine falsch zusammengesetzte Route sieht aus wie eine
Beobachtung.

`--track-every n` gibt es als Ausnahme für einen Lauf, der je unhandlich wird.
Die **Ereignisse** ignorieren den Schalter: eine Flanke zwischen zwei Proben
ist nicht spät, sie ist weg.

### `events.ndjson` — was passiert ist, mit exaktem Tick

`Metrics/DebugEventLog.cs`, Flankenerkennung gegen den Zustand des Vortticks,
in derselben Machart wie `TraceCollector.TrackReactions`: Schattenarrays über
`Entities.Capacity`, aufsteigender Indexscan, keine Dictionary-Reihenfolge.

`spawn`, `death`, `damage`, `heal`, `order`, `goal`, `moveStart`/`moveStop`,
`attackStart`/`attackSwitch`/`attackStop`, `harvestStart`/`harvestStop`,
`cargoFull`/`cargoDelivered`, `siteOpen`/`siteDone`, `stuck`/`unstuck`,
`retreatBelow`/`retreatAbove`.

Benannte Schlüssel statt positionaler Arrays, gegen die Machart von
`ViewFrame`: Ereignisse sind dünn, Grösse spielt keine Rolle, und die Datei
soll sich mit `grep` lesen lassen.

`order` benutzt genau die Definition, die `TraceCollector` schon als „Reaktion"
zählt — die Ereignisspur spricht dieselbe Sprache wie die Spalte, die es
bereits gab.

**Der Verursacher ist hergeleitet, nicht beobachtet.** Die Simulation meldet
keinen. Wie hergeleitet wird, wo es versagt und wie ein sauberer Hook im Spiel
aussähe, steht in [`notes/schadensquelle.md`](notes/schadensquelle.md) — das
ist der Vorschlag, nicht die Umsetzung.

### `units.json` — die Zahlen je Einheit

`Metrics/RouteMetrics.cs`, gerechnet am Laufende aus Spur und Ereignissen. Eine
Zeile je Einheit, aufsteigend nach ID:

| Spalte | Was sie beantwortet |
|---|---|
| `detourPercent` | „Bewegung, die am Ziel nicht dumm aussieht" — gelaufene Strecke gegen Luftlinie, je Segment |
| `blockedTicks` von `movingTicks` | gegenseitiges Blockieren, **gemessen statt vermutet** |
| `goalChanges`, `orderChanges` | Zappeln vor einer Gebäudeecke gegen einen sauberen Bogen |
| `damageTaken`, `damageDealtDerived`, `killsDerived` | wer wie viel abbekommen und ausgeteilt hat |

Nur Ganzzahlen; die Streckenlänge geht durch eine ganzzahlige Wurzel, damit auf
dem Weg kein `double` existiert.

Die zweite Zeile war der eigentliche Grund, das zu bauen: „kein gegenseitiges
Blockieren" ist Auftrag (`CLAUDE.md` §1, `Simulation/Movement/`), und es gab
keine Zahl dafür. Jetzt gibt es sie — an einem Lauf über 6000 Ticks meldet sie
drei `stuck`-Vorfälle.

### `player.html` — die Wiedergabe

Vier Flächen, jede beantwortet etwas anderes: die Karte sagt **wo**, die
Einheitenliste **wer**, das Detailfeld **was gerade**, das Ereignisband unter
dem Scrubber **wann es sich geändert hat**.

- Auswahl per Klick in der Liste oder auf der Karte, tote Einheiten zuschaltbar
- Spur der Auswahl, verblassend, 200 / 600 / 2000 Ticks oder der ganze Lauf;
  zweite Ebene für alle Spuren eines Slots, mit hartem Zeichenlimit
- `n` / `p` springen zum nächsten/vorherigen Ereignis der Auswahl, Klick ins
  Band springt auf den Tick
- Wer stirbt, verschwindet nicht still: die Todesstelle bleibt markiert
- Die Auswahl steht im URL-Fragment (`#u=1043&t=1700`), also ist „schau dir
  1043 um Tick 1700 an" ein Link und keine Anleitung
- Weiter eine Seite, kein Build, kein Server, keine Abhängigkeit

**Der Gewinn:** Der Scrubber läuft auf Sichtframes (alle `--view-every` Ticks),
die Spur kommt aus `tracks.ndjson` mit voller Tickauflösung. Die Route ist
feiner als das Bild.

## 3 · Was das nicht kostet

Die Aufzeichner sind reine Beobachter wie `ViewRecorder` und `TraceCollector`:
sie lesen nach `StepTick()`, schreiben nie zurück, stehen nicht in der
Tickreihenfolge, nicht im Zustands-Hash, nicht im Snapshot.
`EntityTrackTests.RecordingTrackAndEvents_DoesNotChangeTheHashChain` hält das
fest — gegen einen Lauf, der die Sichtframes ohnehin schreibt, damit nur Spur
und Ereignisse übrig bleiben, falls sich etwas bewegt.

**Keine Baseline in `Project_Nova` ist davon betroffen. Am Spiel wurde nichts
geändert.**

## 4 · Was offen war und wie es entschieden wurde

- **Spurlänge im Player: Frames oder Tickspanne?** → Tickspanne. Bei
  wechselndem `--view-every` bedeutet dieselbe Framezahl zwei verschiedene
  Dinge, dieselbe Tickzahl nicht.
- **Wo beginnt ein Umwegfaktor?** → Ein `goal`-Ereignis öffnet ein neues
  Segment, `moveStop` und `death` schliessen es. Ein Ziel, das mitten im Lauf
  umspringt, ist ein neues Segment und kein Umweg — sonst misst die Spalte
  Zielwechsel statt Wegqualität.
- **Wogegen wird die Luftlinie gemessen?** → Gegen den **tatsächlichen
  Endpunkt** des Segments, nicht gegen die Zielzelle. Gegen die Zielzelle fällt
  der Wert bei jeder normalen Ankunft unter 100 %, weil eine Einheit innerhalb
  ihrer Ankunftstoleranz stehenbleibt — ein „Umweg" von 91 % ist Unsinn, der
  sich wie ein Befund liest. Zwischen zwei Punkten, die die Einheit wirklich
  besucht hat, kann das Verhältnis nie unter 100 fallen; ein Test hält das fest.

## 5 · Was hier offen bleibt

- **Das Ereignis `goal` heisst missverständlich.** Es ist die **Wegzelle**
  (`GoalGridPos`), nicht das Vorhaben der KI. Sobald das Goal-System steht,
  zeigt das Panel beides nebeneinander — deshalb wird das Ereignis laborseitig
  auf `pathGoal` umbenannt und das Neue heisst `aiGoal`. Zwei verschiedene Dinge
  unter einem Wort sind im Panel ein Anzeigefehler, den niemand als solchen
  erkennt. Siehe [`GOALS.md`](GOALS.md) §1.

- **Im laufenden Spiel gesehen wurde davon nichts.** Es ist ein
  Laborwerkzeug — die Spur zeigt, was die Simulation gerechnet hat, nicht, wie
  es sich im DMG anfühlt.
- Die Schadensquelle bleibt hergeleitet, solange kein Hook im Spiel existiert
  (`notes/schadensquelle.md`). Bei grösseren Gefechten wird sie unschärfer: bei
  3000 Ticks 92 % eindeutig, bei 6000 Ticks 84 %.
- `stuck` steht auf einer Schwelle von 20 Ticks. Die Zahl ist begründet, aber
  nicht gemessen — ob sie das echte Blockieren trifft, zeigt erst der Vergleich
  zweier Bewegungsstände.
