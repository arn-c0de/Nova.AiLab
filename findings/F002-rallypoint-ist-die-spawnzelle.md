# F002 · `SetRallyPoint` ist die **Spawn-Zelle**, kein Sammelbefehl

**Gefunden am:** 2026-08-09 · **Beim Bauen von:** Rally-Punkt der Kaserne auf den
Sammelpunkt (`NEXT-STEPS.md` §5) · **Status:** Befund, kein PR, **Vorhaben
abgebrochen** — in [`../ROADMAP.md`](../ROADMAP.md) §1 als gestrichen geführt

> Fremdes Terrain. `Simulation/Production/` ist uns nicht zugeteilt
> (`CLAUDE.md` §2, „nicht zugeteilt, also nicht unseres ohne Rückfrage").
> Dieser Befund beschreibt, er repariert nicht.

## Beobachtung

Der Rally-Punkt eines Produktionsgebäudes ist in diesem Build **nicht** das Ziel,
zu dem eine fertige Einheit *läuft*. Er ist der Ankerpunkt, an dem sie
**entsteht**.

`ProductionSystem.TryFindSpawnCell` (`ProductionSystem.cs:433-459`):

```
int rallyCellX = SimFixed.WorldToGrid(row.RallyXRaw);
int rallyCellY = SimFixed.WorldToGrid(row.RallyYRaw);
for (int ring = 0; ring <= SpawnSearchMaxRing; ring++)   // SpawnSearchMaxRing = 8
    ... erste freie Zelle um die Rally-Zelle -> dort spawnt die Einheit
```

Die Klassendoku sagt es ausdrücklich: *„On completion the unit spawns at the
building's rally point: the rally CELL is tried first, then expanding Chebyshev
rings 1..SpawnSearchMaxRing."*

`ValidateSetRallyPoint` (`ProductionSystem.cs:272-292`) prüft Besitz,
Produzentenrolle, fertiggestellte Platzierung und **nur**, dass das Ziel auf der
Karte liegt. **Es gibt keine Entfernungsgrenze.**

## Warum das ein Befund ist und kein Detail

NEXT-STEPS §5 und `AGENTS.md` §6.3 führen `SetRallyPoint` als den
nützlichsten fehlenden Befehl: „Nachschub sammelt sich, statt einzeln zu
sterben." Beide Stellen setzen voraus, dass ein Rally-Punkt ein *Laufbefehl*
ist — das ist er in den meisten RTS, und in diesem Build ist er es nicht.

Setzt die KI den Rally-Punkt ihrer Kaserne auf den Sammelpunkt zwölf Zellen
weiter, dann **erscheinen** ihre Einheiten dort, statt hinzulaufen. Das ist
kein Sammelverhalten, sondern eine Teleportation über zwölf Zellen — mit einem
Ziel am anderen Kartenrand wären es hundert. Sichtbar für jeden Spieler,
unfair, und in einem Projekt, dessen KI ausdrücklich **keine** Abkürzung nimmt
(gleicher Befehlspfad, gleiche Startmittel, nur die committed Team-Sicht), wäre
das der erste Fall, in dem sie eine nimmt.

Zweiter, kleinerer Punkt: Die Spawn-Suche bricht nach acht Ringen ab. Steht der
Sammelpunkt voll, findet sie keine freie Zelle, und die Produktion **pausiert**
(dokumentiert, keine Einheit geht verloren). Ein Rally-Punkt mitten in der
wartenden Welle wäre also auch mechanisch eine Bremse.

## Folge für den Einheitenstrang

**Das Vorhaben wird nicht gebaut.** Die Absicht dahinter — Nachschub
sammelt sich, statt einzeln loszulaufen — ist seit Verhaltensrevision `r3`
ohnehin erfüllt: Eine neu gebaute Einheit bekommt spätestens eine Kadenz später
(20 Ticks) einen Marschbefehl zum Sammelpunkt und wartet dort, bis die Welle
voll ist. Der Rally-Punkt würde daran nichts verbessern, das er nicht durch
Teleportieren erkauft.

## Frage an den Eigentümer

Ist „Rally-Punkt = Spawn-Zelle" so gewollt, oder ist es die provisorische
Fassung, die `Q-040 candidate` an mehreren Stellen der Datei nahelegt? Falls
gewollt: Dann sollte der Name das sagen, denn er verspricht heute etwas
anderes, und mindestens zwei Planstellen dieses Repos haben ihn so gelesen, wie
er heisst. Falls provisorisch: Ein Rally-Punkt als *Marschziel für frisch
produzierte Einheiten* wäre für den Einheitenstrang unmittelbar nützlich und
würde die Zahl der KI-Befehle senken statt sie zu erhöhen.

**Reproduktion:** keine Partie nötig — der Code ist eindeutig. Wer es sehen
will: Rally-Punkt einer Kaserne auf eine Zelle weit ausserhalb der Basis setzen
und eine Einheit produzieren.
