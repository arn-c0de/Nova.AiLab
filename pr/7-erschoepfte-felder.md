# PR-Beschreibung — `fix/ai-harvest-exhausted-field`

> **Diese Datei ist der PR-Text zum Einfügen, 1:1.** Sie folgt der Vorlage des
> Repos; die Belege hängen eingeklappt darunter, damit der PR kurz bleibt.
>
> **Vom FORK** `arn-c0de/Project_Nova` (`fix/ai-harvest-exhausted-field`,
> `574c06d`) **ins HAUPTREPO** `VibecodingGermany/HashKrieg` → `main`.
> Basis ist `upstream/main` (`75973d3`), ein Commit, drei Dateien.
> Bezeichner bleibt `r7.E34435F9` — Begründung unten.
>
> **Schliesst Issue #85.**

## Titel des PR

```
KI: nicht länger endlos auf dem leeren Aetheriumfeld ernten (#85)
```

> [!IMPORTANT]
> **Die Beschreibung muss ersetzt werden.** GitHub füllt sie beim Öffnen mit
> dem Rumpf der Commit-Nachricht vor. Alles ab „Was & Warum" hier einsetzen.

---

## Was & Warum

Die KI kam nach Erschöpfung ihres Startvorkommens wirtschaftlich zum Stillstand
(#85) — kein Strategiemangel, sondern ein **Livelock**: `TryGetOwnFieldCell`
wählte das Erntefeld allein nach Distanz zum HQ, der `EconomySystem` räumte den
Befehl auf dem leeren Feld sofort wieder, und genau dieses Räumen liess den
Harvester in die Leerlaufliste fallen, aus der die KI ihn jeden Entscheidungstick
auf dasselbe leere Feld zurückschickte. Die Erntewahl überspringt erschöpfte
Felder jetzt; ist keines mehr übrig, ruhen Nachbestellung und Erntebefehle,
statt Kommandos ins Leere zu schicken.

**Im laufenden Spiel gesehen:** Eine Partie auf genau diesem Stand gespielt — die
Harvester fahren nach dem Startvorkommen zu anderen Quellen, und es kommen weiter
neue Einheiten, bis alles zerstört ist.

## Checkliste
- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [ ] ~~Echte Entscheidung getroffen? → D-ID im [DecisionLog](../docs/production/DecisionLog.md)~~ — keine; die zwei Abwägungen unten sind Umsetzungsentscheidungen innerhalb des Einheitenstrangs und stehen als Begründung im Code
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement

---

<details>
<summary><b>Zwei Entscheidungen über das im Issue geforderte Minimum hinaus</b></summary>

**Der Platzierungsanker filtert bewusst weiter *nicht*.** `TryGetOwnFieldCell`
hat eine zweite Aufrufstelle: `TryPlaceBuilding` benutzt das nächste Feld als
Anker für „wo ist meine Basis". Filtert man dort mit, setzt sich ein nachgebautes
Refinery ans nächste Feld *mit* Reserve — auf der kanonischen Karte das
umkämpfte Zentrum oder die Gegenecke. Wohin eine Refinery gehört, wenn das
Heimatfeld leerläuft, ist eine echte und strategische Frage; sie als Nebenwirkung
eines Livelock-Fixes zu beantworten wäre schlechter, als sie offen zu lassen.
Deshalb ein `mustHaveReserve`-Schalter und nicht ein Filter in der Schleife.

**Der Rückweg bleibt bedient.** Den ganzen Wirtschaftsschritt zu überspringen,
sobald kein Feld mehr übrig ist, hätte einen kleineren *neuen* Defekt erzeugt:
ein Harvester mit letzter Ladung ausserhalb der Abladereichweite bleibt dann
stehen, weil ein gehaltener Rückwegbefehl nur durch die KI aufgelöst wird.
Gegattert sind deshalb nur die feldabhängigen Teile — Nachbestellung,
Erntebefehle, Hinweg-Eskort.

</details>

<details>
<summary><b>Belege</b></summary>

| Prüfung | Ergebnis |
|---|---|
| Suite | **708/708** grün |
| Baselines | keine der vier Dateien angefasst |
| Fremdes Terrain | nichts; `TryGetField`/`IsExhausted` werden benutzt, nicht geändert |
| Determinismus | `match --repeat 2` → Exit 0 |
| Kanonische Partie | **byte-identisch**: Entscheidung Tick 3.213, Endzustand `0xE002DD893916967B`, alle Laborartefakte gleich |

**Neuer Test** `SkirmishAiFieldExhaustionTests`: ein kleines Feld näher am HQ
wird leergemint; danach darf es nie wieder zugewiesen werden, und die KI muss
anderswo weiterernten. Der Aufbau *mint das Feld leer*, statt es leer zu
registrieren (`TryAddField` weist eine Reserve von 0 ab) — er läuft damit genau
die Abfolge, die auch der Betatest gelaufen ist.

**Gegenprobe gefahren.** Mit abgeschalteter Prüfung fällt der Test mit genau
diesem Befund:

```
1) the AI stopped mining altogether once its near field ran out
2) no income arrived after the near field ran out
```

</details>

<details>
<summary><b>Warum keine Baseline rot wird — anders als im Issue vermutet</b></summary>

Das Issue erwartet, dass die Änderung „sehr wahrscheinlich die
Determinismus-Baseline der kanonischen KI-Partie" bewegt. Sie tut es nicht: in
der kanonischen Partie tragen die Startfelder 2.000.000 AE und erschöpfen sich
nie, die neue Bedingung greift dort also überhaupt nicht. Die Partie läuft Tick
für Tick gleich, und die Trennungsregel aus 13B ist gar nicht berührt — es
braucht keinen zweiten PR.

**Der Bezeichner bleibt deshalb `r7.E34435F9`.** Die kanonische Partie
entscheidet unverändert, und `8` ist bereits an das Basisverteidigungs-Verhalten
eines anderen Zweigs vergeben; zwei verschiedene Stände dürfen sich keine Kennung
teilen. Wenn der Inhaber das anders sieht, ist der Bump eine Zeile.

</details>

<details>
<summary><b>Ein Nebenbefund für die Werkzeugkiste</b></summary>

Der Executor **nimmt** einen Erntebefehl auf ein leeres Feld an — geprüft wird
nur, ob das Feld registriert ist (`AetheriumFieldExists`) —, und die Economy
räumt ihn danach. Der Defekt erzeugte also **null abgelehnte Intents**. Die
Spalte `intentsRejected`, die im Labor als Frühwarnsignal für „die KI rennt
gegen Executor-Regeln an" gilt, kann diese Fehlerklasse grundsätzlich nicht
sehen. Wer nach ähnlichen Livelocks sucht, braucht ein anderes Signal — etwa
Befehle, die je Kadenz identisch wiederholt werden.

Nebenbei aufgefallen und **nicht** in diesem PR: `FindBestVisibleEnemyByScore`
trägt zwei `<summary>`-Blöcke übereinander (Stand `upstream/main`). Ein Einzeiler,
gehört aber nicht in einen Livelock-Fix.

</details>
