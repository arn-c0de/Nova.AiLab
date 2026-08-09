# Plan — Sammeln abbrechen, wenn die eigene Basis angegriffen wird

> **Anlass ist eine Beobachtung, kein Planpunkt.** Beim Ansehen einer
> Laboraufnahme fiel auf: die KI sammelt am Sammelpunkt weiter, während ihr
> eigenes Hauptquartier beschossen wird. Journal [B003](reports/behavior-log.md).
>
> **Stand:** Entwurf, nichts gebaut. Vorgesehen als PR 7,
> `feat/ai-abort-on-threat`, Bezeichner `r6` → **`r7`**.
> Der Vorgänger [`pr/6-staerketor.md`](pr/6-staerketor.md) ist eingereicht.

---

## 1 · Was gemessen ist

In der **ausgelieferten** KI, über das Fenster, in dem ihr HQ unter Feuer liegt:

| | ausgeliefert | Obergrenze 30 ohne Tor |
|---|---:|---:|
| HQ unter Beschuss | 3.169 Ticks | 3.270 Ticks |
| davon **wehrlos** | **19 %** | 18 % |
| Spitzenwert wartender Einheiten | **9** | 11 |

„Wehrlos" heisst: mindestens drei eigene Kampfeinheiten stehen 8–16 Zellen vom
eigenen HQ entfernt — also am Sammelpunkt — während ein Feind innerhalb von
10 Zellen das HQ beschiesst.

**Der Defekt ist alt.** Er existiert, seit es den Sammelpunkt gibt (r3), und
`r6` hat ihn nicht verursacht. Aber `r6` vergrössert seine Fläche, sobald die
Armeeobergrenze steigt: die Zeit, die Einheiten wartend herumstehen, geht von
3.502 auf 12.326 Einheit-Ticks je 1.000, eine einzelne Einheit wartet bis zu
3.214 Ticks. **Deshalb kommt diese Regel vor der Obergrenze**, nicht danach.

## 2 · Warum es passiert

Eine Einheit, die am Sammelpunkt **angekommen** ist, bekommt **überhaupt keinen
Befehl**. Das ist Absicht und in `ResolveUnitAssignment` so kommentiert: ein
Befehl je Kadenz für eine stehende Einheit bläht die Intent-Zahl auf, ohne dass
sich etwas ändert (Journal V002, `DefendBase`).

Sie hängt damit ausschliesslich an der D-087-Auto-Acquisition, und die reicht so
weit wie die Waffe:

| | Zellen |
|---|---:|
| Reichweite Legions-Infanterie | 6 |
| Reichweite Allianz-Schütze | 7 |
| **Sammelpunkt ↔ eigenes HQ** | **12** |

Ein Angreifer am HQ ist damit ausserhalb jeder Reichweite. Die Wartenden
ignorieren ihn nicht — **sie sehen ihn nicht**. Eine Verteidigungsregel gibt es
im Code nicht.

## 3 · Was ausdrücklich **nicht** gebaut wird

> `DefendBase` wurde in V002 gebaut und wieder ausgebaut: **+23 % Intents,
> schlechteres Spiel.** Der Fehler war die Form — die **ganze** Armee bekam
> jede Kadenz ein neues Ziel, und das Ziel war der Feind, also etwas
> Bewegliches. Jede Kadenz eine neue Zielzelle heisst jede Kadenz ein neuer
> Befehl für jede Einheit.

Diese Regel muss deshalb drei Dinge anders machen:

1. **Nur die Sammelnden** reagieren. Wer schon draussen ist, marschiert weiter —
   die r3-Regel „Einheiten draussen werden nie zurückgerufen" bleibt.
2. **Das Ziel ist eine statische Zelle**, nämlich das eigene HQ. Nicht der
   Feind, nicht der Schwerpunkt der Bedrohung. Eine statische Zelle erzeugt
   **einen** Befehl, keinen Strom.
3. **Kein zweiter Zielwahl-Pfad.** Was geschossen wird, entscheidet weiterhin
   die vorhandene Zielwahl bzw. die Auto-Acquisition.

## 4 · Die Regel

### 4.1 Auslöser

```
BasisBedroht  =  es gibt einen sichtbaren BEWAFFNETEN Gegner
                 innerhalb von DefendHomeCells Zellen (Chebyshev) um das eigene HQ
```

Drei Eigenschaften, jede mit Grund:

- **bewaffnet** — ein Harvester am Zaun ist kein Angriff. Genau daran ist
  `DefendBase` gescheitert („reagiere auf eine echte Bedrohung, nicht auf alles
  was sich bewegt"). `CollectVisibleThreats` filtert bereits so.
- **sichtbar** — nur die committed Team-Sicht. Alles andere wäre ein Blick durch
  den Nebel.
- **um das HQ** — nicht um irgendein Gebäude. Begründung in §7.

### 4.2 Wirkung

Für jede Kampfeinheit, die **nicht** committed ist (also im Sammelring wartet)
und **nicht** gerade zurückzieht:

```
Ziel      = eigene HQ-Zelle          (statisch, ändert sich die ganze Partie nicht)
Angriff   = nächster sichtbarer bewaffneter Gegner
```

Das ist exakt die Form, die der **Rückzug** schon benutzt: laufen zu einer
festen Zelle, dabei auf den Verfolger zielen (`NearestThreatRaw`). Sie ist
erprobt, und der Grund für den Zielbefehl steht in Befund F001 — wer läuft und
nichts anzielt, trägt einen veralteten Befehl mit sich herum.

**`WaveReady` bleibt unberührt.** Die Welle wird nicht „freigegeben", sie wird
**unterbrochen**: die Sammelnden gehen nach Hause, kämpfen, und sammeln danach
weiter. Das ist der Unterschied zwischen „Abbruch" und „vorzeitigem Angriff",
und nur der Abbruch löst das beobachtete Problem — ein vorzeitiger Angriff
würde die Wartenden **vom** HQ **weg** schicken.

### 4.3 Aus-Stellung

`DefendHomeCells: 0` ⇒ die Regel ist aus, der Pfad von `r6` unverändert, Bit für
Bit. Ohne diese Stellung ist die Regel im Selbstspiel nicht messbar
(Methodenbefund M001): eine Coderegel erreicht beide Seiten zugleich.

## 5 · Die Profilwerte

| Feld | Aus | Startwert | Warum dieser Startwert |
|---|---:|---:|---|
| `DefendHomeCells` | **0** | **10** | Der Radius, mit dem B003 gemessen wurde; liegt zwischen `RetreatDangerCells` (8) und dem Ring (16), also innerhalb der Basis und ausserhalb der Waffenreichweite |

**Ein Wert, kein zweiter.** Ein Hysterese-Radius („bleibt zuhause, bis der Feind
14 Zellen weg ist") wäre die naheliegende zweite Zahl und ist bewusst nicht im
Plan — siehe §7.

## 6 · Wo das im Code hängt — zwei Fallstricke

Beide sind vor dem ersten Testlauf zu klären, sonst plant man an der Struktur
vorbei.

> **1. Die Bedrohungsliste wird zu spät gebaut.**
> `CollectVisibleThreats` läuft heute **nach** `ResolveArmyPosture`, im Block
> von Schritt (6). Der Auslöser braucht sie aber **in** der Posture. Die
> Sammlung muss also vor den Posture-Aufruf gezogen werden. Das ist eine reine
> Umordnung innerhalb einer Entscheidung, keine Änderung der Tick-Reihenfolge —
> aber sie ist anzusehen, nicht anzunehmen.

> **2. Die Bedrohungsliste hängt am Rückzug.**
> Sie wird nur gefüllt, wenn `RetreatHealthPercent > 0`. Bliebe das so, würde
> `retreat-off` die Verteidigung stillschweigend mit abschalten — und der
> Laborkandidat `retreat-off` misst dann etwas anderes, als sein Name sagt. Das
> Gatter muss zu `RetreatHealthPercent > 0 || DefendHomeCells > 0` werden.

**Was schon da ist und reicht:** die HQ-Zelle (`hqCellX/Y`) fällt im
Entity-Scan ohnehin an, und `IsCommittedToTheWave` grenzt die Sammelnden bereits
ab. Es braucht **kein** neues System, **keine** neue Tick-Position, **keinen**
neuen Zustand.

## 7 · Drei Entscheidungen, die der Plan bewusst eng hält

**Nur das HQ, nicht jedes Gebäude.** Der Entity-Scan sammelt heute die Zelle des
HQ und der Raffinerie; die Kaserne trägt nur eine Id, keine Zelle. „Jedes
Gebäude" bräuchte also eine Erweiterung des Scans — und vor allem einen Grund.
Der Grund fehlt: beobachtet und gemessen ist der Fall am HQ. Wenn die Messung
zeigt, dass Angriffe auf die Raffinerie ebenso durchgehen, kommt das als eigene
Zahl nach.

**Keine Hysterese.** Die Sorge ist Flattern: Feind läuft in den Radius, Einheiten
laufen heim, Feind läuft raus, Einheiten laufen zurück. Zwei Dinge dämpfen das
schon — beide Ziele (HQ und Sammelpunkt) sind **statische** Zellen, und
`SubmitAssignments` unterdrückt die Wiederholung eines gleichen Befehls. Ob das
reicht, ist eine **Messfrage**, und die Antwort steht in der Intent-Spalte. Eine
Hysterese vorab einzubauen hiesse, eine zweite Zahl gegen ein Problem zu
stellen, das noch niemand gemessen hat.

**Die Angriffsschwelle bleibt, wie sie ist.** `posture.Engages` verlangt
`AttackSquadThreshold` (6) lebende Kampfeinheiten — darunter tut der Armee-Schritt
gar nichts. In B003 begann der erste Beschuss bei **einer** Einheit; die Regel
greift dort also nicht. Das ist eine echte Lücke und trotzdem bewusst offen:
`Engages` zu senken ist eine zweite Verhaltensänderung, sie gehört nicht in
denselben PR, und V003 hat schon einmal gezeigt, dass unterhalb der Schwelle
zu handeln teurer ist als es aussieht.

## 8 · Tests

1. **Auslöser über Positionen, nicht über Intents.** Szene: die KI sammelt am
   Sammelpunkt, ein bewaffneter Gegner wird neben ihrem HQ eingesetzt. Nach der
   nächsten Entscheidung muss mindestens eine sammelnde Einheit ein Ziel tragen,
   das **näher am eigenen HQ** liegt als ihr Standort. Auf `r6` fällt der Test —
   dort trägt sie gar kein Ziel.
2. **Die Welle wird nicht weggeschickt.** Dieselbe Szene: keine sammelnde
   Einheit bekommt ein Ziel **in Richtung des Gegner-Startgebiets**. Das trennt
   „Abbruch" von „vorzeitigem Angriff" — ohne diesen Test sind beide grün.
3. **Committed bleibt committed.** Einheiten ausserhalb des Rings behalten ihr
   Marschziel. Die r3-Regel darf nicht durch die Hintertür fallen.
4. **Aus-Stellung.** `DefendHomeCells: 0` ⇒ die kanonische Partie endet auf
   demselben Tick und demselben Endzustands-Hash wie `r6`.
5. **Kein Befehlsstrom.** Bei unveränderter Lage über mehrere Kadenzen wird
   **kein** zweiter identischer Befehl abgesetzt. Das ist der V002-Test, den es
   damals nicht gab.

Der gepinnte Endzustand in `SkirmishAiTests` **wird sich bewegen** — das ist
eine echte Verhaltensänderung, anders als bei `r6`. Bezeichner auf `r7`,
Journaleintrag mit „Schlechter", und **keine** der vier Determinismus-Baselines
im selben PR.

## 9 · Was gemessen wird

Einseitig, dasselbe Binary, ein Profilwert Unterschied, beide Fraktionssitze.

| Kennzahl | woher | warum |
|---|---|---|
| **Wehrlose Stichproben in %** | B003-Skript | die Zahl, wegen der die Regel existiert |
| **Aussetzzeit am Sammelpunkt** | Einheit-Ticks je 1.000 | dieselbe Messung, andere Seite |
| **Intents je 1.000 Ticks / APM** | Trace | **die erste Zahl, die angesehen wird** — V002 ist genau hier gestorben |
| verworfene Intents | Trace | muss 0 bleiben |
| eigene Verluste, Austausch, Tick | Trace / feel | ob die Regel das Spiel verbessert oder nur beruhigt |

**Reihenfolge der Beurteilung:** erst Intents, dann Wehrlosigkeit, dann Ausgang.
Eine Regel, die die Wehrlosigkeit senkt und dabei die Intents verdoppelt, ist
`DefendBase` unter neuem Namen.

## 10 · Woran der Plan scheitern darf

- **Flattern.** Wenn die Intent-Spalte steigt, ohne dass die Wehrlosigkeit
  fällt, ist der Auslöser zu nervös. Dann ist die Hysterese aus §7 fällig — als
  Antwort auf eine Messung, nicht als Vorsichtsmassnahme.
- **Zu spät.** Wenn die Wehrlosigkeit kaum fällt, liegt es vermutlich an der
  Angriffsschwelle (§7) — der Beschuss beginnt, bevor die KI überhaupt handelt.
  Dann ist die Schwelle die eigentliche Frage, nicht der Radius.
- **Zu teuer.** Wenn die Sammelnden ständig heimlaufen und die Welle nie
  zustande kommt, hat die Regel das Sammeln ersetzt statt es zu unterbrechen.
  Sichtbar an der grössten Welle je Partie.
- **Wirkungslos ohne die Obergrenze.** Möglich, dass 19 % erst bei der höheren
  Obergrenze wirklich weh tun. Dann ist die Regel trotzdem richtig, aber die
  Reihenfolge im PR-Text ist ehrlich zu benennen.

## 11 · Scope

Alles innerhalb unserer Schreibhoheit:

| Datei | Was |
|---|---|
| `Scripts/AI/SkirmishAiSystem.cs` | Auslöser in `ResolveArmyPosture`, Zweig in `ResolveUnitAssignment`, Umordnung der Bedrohungssammlung |
| `Scripts/AI.Data/AiProfile.cs` · `AiProfiles.cs` | Feld `DefendHomeCells` |
| `Scripts/AI.Data/AiBehaviorId.cs` | `Revision` 6 → 7 |
| `tools/Nova.SimRunner.Tests/` | die fünf Tests aus §8 |
| `CHANGELOG.md` | ein Eintrag |

Nicht angefasst: `MatchRunner`, Netzstrang, Definitionstabelle, Tick-Reihenfolge,
die vier Baselines. Ganzzahlig durchgehend, kein `System.Random`, keine Wanduhr,
keine Abhängigkeit von Iterationsreihenfolge.

## 12 · Reihenfolge

1. **Labor zuerst:** die B003-Auswertung als wiederholbare Kennzahl in den
   Bericht, nicht als Skript, das ich einmal geschrieben habe. Ohne sie ist der
   Effekt nicht messbar.
2. Referenzlauf sichern.
3. Code in der Reihenfolge: Bedrohungssammlung vorziehen → Gatter erweitern →
   Auslöser → Zweig in der Zuweisung → `Revision` 7.
4. **Determinismus zuerst** (`match --repeat 2`). Exit 2 heisst: hier ist Schluss.
5. Einseitig messen, beide Sitze. Dann beide Suiten.
6. Journaleintrag **mit** „Schlechter".
7. Fragen — Commit, Push und PR sind drei getrennte Freigaben.

---

**Im laufenden Spiel gesehen:** die *Beobachtung* ja, am Player. Die *Regel*
nicht — sie ist nicht gebaut. Was hier steht, ist ein Plan.
