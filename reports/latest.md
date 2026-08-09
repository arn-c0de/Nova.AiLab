# Laborlauf 20260809-1519-f13f4d5f

> [!IMPORTANT]
> **DIAGNOSE, kein Nachweis.** Nichts in diesem Bericht wurde im laufenden Spiel gesehen.
> Alle Zahlen stammen aus headless-Läufen derselben Quelldateien, die Unity lädt — das
> macht sie vergleichbar, nicht wahr. Es gibt bewusst **keine Rangfolge**: die Werte stehen
> nebeneinander, die Auswahl trifft ein Mensch.

| Herkunft | Wert |
| --- | --: |
| gemessen am | 2026-08-09T15:19:26Z |
| Commit | `f13f4d5f21ed30c3820c8b2731fdd3d806e30528` |
| Definitionstabelle | `0x6326FA3E56CFF5A3` |
| KI-Verhalten | `r6.E34435F9` |
| Seed | `0xA17E57DE57` |
| Tickbudget | 27.000 |
| Slots | 2 |
| specVersion | 1 |
| Fingerabdruck | `575513ae2c4e0253` |

Interaktive Fassung derselben Zahlen: [`dashboard.html`](../out/dashboard.html) — Kurven mit Fadenkreuz, Heatmap mit Abstandsdetail, Scrubber. Lokal öffnen, kein Server nötig; GitHub zeigt HTML nicht an, dafür ist dieser Bericht da.

## Laufart 1 · `match` — die Partie: KI gegen KI

Eine kanonische Partie über beide Slots, Metriken alle 50 Ticks, reine Beobachtung — ein Lauf mit und ohne Trace liefert dieselbe Hash-Kette.

| Kennzahl | Wert | Kontext |
| --- | --: | --- |
| Ausgang | VictoryElimination | Slot 0 · alliance |
| Entschieden bei Tick | 5.773 | von 27.000 Budget |
| Rechenzeit | 957 ms | 6.032 Ticks/s |
| Metrikproben | 116 | alle 50 Ticks |
| Hash-Kette | 12 | alle 500 Ticks |
| Endzustands-Hash | `0x2B34B4E194257940` | bei Tick 5.773 |

| Endwert je Slot | Slot 0 · alliance | Slot 1 · legion |
| --- | --: | --: |
| Credits | 17.240 | 18.420 |
| Armeegrösse | 12 | 8 |
| Verluste, kumuliert | 23 | 51 |
| Harvester | 2 | 2 |
| Gebäude | 4 | 3 |
| Sichtbare Feindeinheiten | 5 | 4 |

1. Linie **Slot 0 · alliance** · 2. Linie **Slot 1 · legion**. `xychart-beta` kennt keine Legende, deshalb steht die Zuordnung hier. x-Achse Tick 0 bis 5.750, alle Werte ganzzahlig — kein Float verlässt die Simulation.

**Credits** — Kassenstand je Slot

```mermaid
xychart-beta
    title "Credits"
    x-axis "Tick" 0 --> 5750
    y-axis "Credits" 0 --> 20000
    line [3000, 2300, 2300, 1150, 980, 980, 920, 770, 1190, 1520, 1850, 2510, 2840, 3500, 3710, 4130, 4460, 5120, 5450, 6110, 6770, 6530, 7190, 7850, 8510, 8390, 8810, 9470, 10010, 10220, 10550, 10610, 10940, 11360, 11690, 12350, 12200, 12860, 13190, 13520, 14180, 14510, 14930, 15260, 15920, 16250, 16910, 17240]
    line [3000, 2450, 1500, 1500, 1200, 1380, 1680, 2280, 2880, 3180, 3480, 4080, 4680, 5280, 5160, 5160, 5760, 6360, 6960, 6960, 7560, 7980, 8220, 8760, 9360, 9360, 9840, 10440, 11040, 11280, 11580, 11940, 12540, 13140, 13440, 14040, 13800, 14280, 14880, 15180, 15780, 16080, 16200, 16680, 16980, 17520, 18120, 18420]
```

**Armeegrösse** — lebende Kampfeinheiten

```mermaid
xychart-beta
    title "Armeegrösse"
    x-axis "Tick" 0 --> 5750
    y-axis "Armeegrösse" 0 --> 20
    line [0, 0, 0, 0, 0, 0, 0, 1, 3, 4, 5, 6, 7, 9, 8, 8, 9, 11, 12, 12, 12, 10, 11, 12, 12, 11, 10, 11, 11, 11, 12, 8, 9, 8, 9, 11, 8, 9, 10, 11, 12, 12, 10, 11, 12, 12, 12, 12]
    line [0, 0, 0, 0, 1, 2, 4, 5, 7, 8, 10, 11, 12, 12, 7, 2, 3, 5, 6, 7, 9, 3, 2, 4, 5, 6, 7, 8, 10, 10, 11, 9, 10, 12, 12, 11, 2, 4, 5, 6, 8, 9, 1, 2, 4, 5, 6, 8]
```

**Verluste, kumuliert** — verlorene Einheiten seit Tick 0

```mermaid
xychart-beta
    title "Verluste, kumuliert"
    x-axis "Tick" 0 --> 5750
    y-axis "Verluste, kumuliert" 0 --> 60
    line [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 3, 3, 3, 3, 3, 5, 5, 5, 5, 6, 8, 8, 9, 10, 10, 15, 15, 17, 17, 17, 21, 21, 21, 21, 21, 21, 23, 23, 23, 23, 23, 23]
    line [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 12, 12, 12, 12, 12, 12, 19, 22, 22, 22, 23, 24, 24, 24, 25, 25, 29, 29, 29, 29, 30, 40, 40, 40, 40, 40, 40, 50, 50, 50, 51, 51, 51]
```

**Harvester** — Erntefahrzeuge im Feld

```mermaid
xychart-beta
    title "Harvester"
    x-axis "Tick" 0 --> 5750
    y-axis "Harvester" 0 --> 2
    line [0, 0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
    line [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
```

**Verworfene Intents:** 0 von 286 (Slot 0) und 0 von 134 (Slot 1). Diese Spalte ist die unterschätzte — sie zeigt, wo die KI gegen Executor-Regeln anrennt, und ist überall sonst stumm, weil `Submit()` das Verdikt nicht auswertet.

**Der Seed ändert die Partie nicht.** Kein Simulationssystem zieht aus dem Kernel-PRNG; der Seed geht in Zustands-Hash und Snapshot, sonst nirgendwohin. Ein Sweep über 24 Seeds ist *eine* Beobachtung.

## Laufart 4 · `compare` — Kandidatenprofile gegen die eingefrorene Referenz

Jeder Kandidat spielt gegen `ms1-canonical`, in beiden Fraktionsrollen — das hebt die Spawnreihenfolge auf. Die Zeilenreihenfolge ist die Kandidatenliste, nicht die Güte.

| Kandidat | geändert gegen Referenz | Sieg % | S/N/U | Entsch. Tick | Credits | Armee | verloren | Intents | verworfen |
| --- | --- | --: | --: | --: | --: | --: | --: | --: | --: |
| **ms1-canonical** _Referenz_ | — | 50 % | 1/1/0 | 5.773 | 17.515 | 9 | 37 | 416 | 0 |
| **early-push** | armySize 12→10; squadThreshold 6→3 | 50 % | 1/1/0 | 4.409 (-24 %) | 13.540 (-23 %) | 9 | 20 (-46 %) | 340 | 0 |
| **late-push** | armySize 12→20; squadThreshold 6→12 | 50 % | 1/1/0 | 10.278 (+78 %) | 30.945 (+77 %) | 11 (+22 %) | 95 (+157 %) | 1.023 | 0 |
| **greedy-economy** | harvesters 2→4; armySize 12→16; squadThreshold 6→8 | 50 % | 1/1/0 | 5.381 (-7 %) | 30.680 (+75 %) | 8 (-11 %) | 37 | 570 | 0 |
| **power-buffer** | powerReserve 0→30 | 50 % | 1/1/0 | 6.483 (+12 %) | 19.325 (+10 %) | 8 (-11 %) | 39 (+5 %) | 564 | 0 |
| **fast-cadence** | cadence 20→10 | 50 % | 1/1/0 | 6.978 (+21 %) | 22.075 (+26 %) | 8 (-11 %) | 48 (+30 %) | 847 | 0 |
| **wave-off** | waveSize 12→1 | 50 % | 1/1/0 | 6.844 (+19 %) | 19.320 (+10 %) | 6 (-33 %) | 60 (+62 %) | 781 | 0 |
| **wave-6** | waveSize 12→6 | 50 % | 1/1/0 | 5.773 | 17.515 | 9 | 37 | 416 | 0 |
| **wave-10** | waveSize 12→10 | 50 % | 1/1/0 | 5.773 | 17.515 | 9 | 37 | 416 | 0 |
| **wave-6-far** | waveSize 12→6; staging 12→70; stagingTol 4→6 | 50 % | 1/1/0 | 7.944 (+38 %) | 24.505 (+40 %) | 8 (-11 %) | 49 (+32 %) | 735 | 0 |
| **retreat-off** | retreatAt 60→0% | 50 % | 1/1/0 | 5.378 (-7 %) | 16.165 (-8 %) | 7 (-22 %) | 38 (+3 %) | 271 | 0 |
| **retreat-40** | retreatAt 60→40% | 50 % | 1/1/0 | 8.203 (+42 %) | 25.410 (+45 %) | 7 (-22 %) | 62 (+68 %) | 658 | 0 |
| **retreat-75** | retreatAt 60→75% | 50 % | 1/1/0 | 9.627 (+67 %) | 31.225 (+78 %) | 7 (-22 %) | 62 (+68 %) | 784 | 0 |
| **retreat-25-near** | retreatAt 60→25%; retreatDanger 8→4 | 50 % | 1/1/0 | 5.317 (-8 %) | 16.150 (-8 %) | 6 (-33 %) | 39 (+5 %) | 327 | 0 |
| **army-16** | armySize 12→16 | 50 % | 1/1/0 | 4.697 (-19 %) | 14.140 (-19 %) | 10 (+11 %) | 28 (-24 %) | 470 | 0 |
| **army-18** | armySize 12→18 | 50 % | 1/1/0 | 10.741 (+86 %) | 33.240 (+90 %) | 9 | 103 (+178 %) | 1.063 | 0 |
| **army-20** | armySize 12→20 | 100 % | 2/0/0 | 4.638 (-20 %) | 13.660 (-22 %) | 16 (+78 %) | 24 (-35 %) | 516 | 0 |
| **army-24** | armySize 12→24 | 100 % | 2/0/0 | 10.632 (+84 %) | 33.070 (+89 %) | 21 (+133 %) | 95 (+157 %) | 1.291 | 0 |
| **army-36** | armySize 12→36 | 100 % | 2/0/0 | 5.830 (+1 %) | 16.720 (-5 %) | 24 (+167 %) | 37 | 877 | 0 |
| **strength-off** | waveStrength 1200→0 | 50 % | 1/1/0 | 5.773 | 17.515 | 9 | 37 | 416 | 0 |
| **army-24-count** | armySize 12→24; waveStrength 1200→0 | 50 % | 1/1/0 | 5.037 (-13 %) | 14.560 (-17 %) | 13 (+44 %) | 35 (-5 %) | 702 | 0 |
| **army-36-count** | armySize 12→36; waveStrength 1200→0 | 50 % | 1/1/0 | 5.083 (-12 %) | 13.975 (-20 %) | 16 (+78 %) | 35 (-5 %) | 772 | 0 |
| **army-16-count** | armySize 12→16; waveStrength 1200→0 | 50 % | 1/1/0 | 6.327 (+10 %) | 19.600 (+12 %) | 9 | 38 (+3 %) | 732 | 0 |

**Was hier _nicht_ steht.** Keine Spalte ist zu einer Note verrechnet und nichts ist nach Güte sortiert. Eine einzelne Zahl belohnt zuverlässig das Falsche — eine KI, die 5 % häufiger gewinnt, weil sie den Gegner mit Bauarbeitern zumüllt, ist keine bessere KI.

**1 Seed je Kandidat** — die Seed-Achse ist heute leer, also sind das 2 Partien je Kandidat, nicht 2 unabhängige Stichproben.

## Laufart 2 · `duel` — die Gegentabelle, gemessen statt abgelesen

AE-Parität statt Stückzahlparität, drei Startabstände, jede Paarung in beide Richtungen. Zeile = die zuerst gespawnte Seite. Der Wert ist ihre Bilanz über die drei Abstände (Siege − Niederlagen, Bereich −3…+3); die Abstände einzeln stehen im Dashboard.

| Zeile gewinnt ↓ / Spalte → | All.AntiArmorInfantry | All.Artillery | All.BasicInfantry | All.BattleTank | All.LightTank | All.ScoutVehicle | Leg.AntiArmorInfantry | Leg.Artillery | Leg.BasicInfantry | Leg.BattleTank | Leg.LightTank | Leg.ScoutVehicle |
| --- | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: | --: |
| **All.AntiArmorInfantry** | +3 | +2&nbsp;⚠ | -2 | 0 | +2 | -1 | +2 | +2&nbsp;⚠ | -2 | -1 | -1 | -1 |
| **All.Artillery** | -2&nbsp;⚠ | +2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | +2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ |
| **All.BasicInfantry** | +2 | +2&nbsp;⚠ | +1 | -2 | +1 | +2 | +1 | +2&nbsp;⚠ | -2 | -2 | +1 | +1 |
| **All.BattleTank** | +2 | +2&nbsp;⚠ | +2 | +2 | +2 | +2 | -1 | +2&nbsp;⚠ | +2 | -2 | +2 | +2 |
| **All.LightTank** | -2 | +2&nbsp;⚠ | -1 | -2 | +1 | +2 | -2 | +2&nbsp;⚠ | -1 | -3 | -1 | +2 |
| **All.ScoutVehicle** | +1 | +2&nbsp;⚠ | -2 | -2 | -2 | +1 | +1 | +2&nbsp;⚠ | -2 | -2 | -2 | -1 |
| **Leg.AntiArmorInfantry** | 0 | +2&nbsp;⚠ | -1 | +2 | +2 | -1 | +3 | +2&nbsp;⚠ | -2 | +1 | +3 | -1 |
| **Leg.Artillery** | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | +2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ | -2&nbsp;⚠ |
| **Leg.BasicInfantry** | +2 | +2&nbsp;⚠ | +2 | -2 | +1 | +2 | +2 | +2&nbsp;⚠ | +3 | -1 | +1 | +2 |
| **Leg.BattleTank** | +1 | +2&nbsp;⚠ | +2 | +2 | +3 | +2 | 0 | +2&nbsp;⚠ | +1 | +3 | +2 | +2 |
| **Leg.LightTank** | +1 | +2&nbsp;⚠ | +1 | -2 | +1 | +2 | -3 | +2&nbsp;⚠ | -1 | -2 | +1 | +2 |
| **Leg.ScoutVehicle** | +1 | +2&nbsp;⚠ | -1 | -2 | -2 | +1 | +1 | +2&nbsp;⚠ | -2 | -2 | -2 | +1 |

`·` — in keinem Abstand Kontakt · `⚠` — kein Kontakt in mindestens einem Abstand

| Duelle | entschieden | ins Tickbudget | ohne Kontakt | wackelnde Parität | AE-Budget je Paarung |
| --: | --: | --: | --: | --: | --: |
| 576 | 395 | 181 | 100 | 6 | 360–6.000 AE (12 Werte) |

**Das Budget gilt je Paarung, nicht für die Tabelle.** Es ist so bemessen, dass die teurere Seite die eingestellte Stückzahl aufstellt — die billigere stellt, was dieselben AE kaufen. Ein globales Budget wäre falsch: bei 10.000 AE stellte eine billige Paarung 83 Einheiten je Seite und maß damit Formation und Pathfinding, nicht die Waffe.

**100 Duelle ohne einen einzigen Schuss.** Wo die Waffenreichweite über der Sichtweite liegt, kann sie ohne Aufklärung nicht benutzt werden — `CombatSystem` verlangt das Ziel als sichtbar in der committed Team-Sicht. Das ist ein Balance-Befund, kein Messfehler.

**6 Paarungen** liessen über 10 % eines Budgets ungenutzt — dort wackelt die AE-Parität selbst, ihr Ausgang ist schwaches Material.

**5 Richtungsabweichungen** — Rollenpaarungen, bei denen A gegen B anders ausgeht als B gegen A. Sie sind so knapp, dass die Spawnreihenfolge sie kippt; das gehört in den Bericht, nicht wegkalibriert. Spiegelpaarungen (dieselbe Rolle gegen sich selbst) zählen nicht mit: dort entscheidet die dokumentierte Duell-Asymmetrie.

<details><summary>Die betroffenen Paarungen</summary>

- All.AntiArmorInfantry ↔ All.BattleTank
- All.AntiArmorInfantry ↔ Leg.AntiArmorInfantry
- All.BasicInfantry ↔ Leg.LightTank
- All.BattleTank ↔ Leg.AntiArmorInfantry
- Leg.AntiArmorInfantry ↔ Leg.BattleTank

</details>

## Laufart 2 · Belagerungsstaffel — womit reisst man eine Basis ein

Gebäude schiessen nicht zurück — ausser der `DefensePlatform`. Gemessen wird deshalb die Zeit bis zum Abriss, gegen das eigene Fraktionsgebäude, Staffel *Berührung*. Sortierung alphabetisch, nicht nach Zeit.

> [!WARNING]
> **Diese Staffel läuft nicht auf AE-Parität**, anders als die Einheitenduelle. Jeder
> Angreifer stellt sechs Einheiten *seiner* Kosten, die Tickzahlen sind untereinander
> deshalb nicht direkt vergleichbar — die AE-Spalte gehört zur Tickspalte dazu. Wer
> Waffenwirkung vergleichen will, rechnet `Ticks × AE`.

| Angreifer | Gebäude | Ticks bis Abriss | Einheiten | AE | eigene Verluste |
| --- | --- | --: | --: | --: | --: |
| **All.AntiArmorInfantry** | All.Barracks | 52 | 6 | 1.500 | 0 |
| **All.AntiArmorInfantry** | All.DefensePlatform | 52 | 6 | 1.500 | 1 |
| **All.AntiArmorInfantry** | All.Power | 27 | 6 | 1.500 | 0 |
| **All.Artillery** | All.Barracks | 72 | 6 | 6.000 | 0 |
| **All.Artillery** | All.DefensePlatform | 72 | 6 | 6.000 | 0 |
| **All.Artillery** | All.Power | 2 | 6 | 6.000 | 0 |
| **All.BasicInfantry** | All.Barracks | 353 | 6 | 720 | 0 |
| **All.BasicInfantry** | All.DefensePlatform | 292 | 6 | 720 | 6 |
| **All.BasicInfantry** | All.Power | 236 | 6 | 720 | 0 |
| **All.BattleTank** | All.Barracks | 127 | 6 | 5.400 | 0 |
| **All.BattleTank** | All.DefensePlatform | 127 | 6 | 5.400 | 0 |
| **All.BattleTank** | All.Power | 77 | 6 | 5.400 | 0 |
| **All.LightTank** | All.Barracks | 182 | 6 | 3.600 | 0 |
| **All.LightTank** | All.DefensePlatform | 182 | 6 | 3.600 | 0 |
| **All.LightTank** | All.Power | 122 | 6 | 3.600 | 0 |
| **All.ScoutVehicle** | All.Barracks | 332 | 6 | 1.800 | 0 |
| **All.ScoutVehicle** | All.DefensePlatform | 382 | 6 | 1.800 | 2 |
| **All.ScoutVehicle** | All.Power | 222 | 6 | 1.800 | 0 |
| **Leg.AntiArmorInfantry** | Leg.Barracks | 52 | 6 | 1.200 | 0 |
| **Leg.AntiArmorInfantry** | Leg.DefensePlatform | 52 | 6 | 1.200 | 1 |
| **Leg.AntiArmorInfantry** | Leg.Power | 27 | 6 | 1.200 | 0 |
| **Leg.Artillery** | Leg.Barracks | 72 | 6 | 4.800 | 0 |
| **Leg.Artillery** | Leg.DefensePlatform | 72 | 6 | 4.800 | 0 |
| **Leg.Artillery** | Leg.Power | 72 | 6 | 4.800 | 0 |
| **Leg.BasicInfantry** | Leg.Barracks | 632 | 6 | 360 | 0 |
| **Leg.BasicInfantry** | Leg.DefensePlatform | 172 | 6 | 360 | 6 |
| **Leg.BasicInfantry** | Leg.Power | 422 | 6 | 360 | 0 |
| **Leg.BattleTank** | Leg.Barracks | 52 | 6 | 4.200 | 0 |
| **Leg.BattleTank** | Leg.DefensePlatform | 52 | 6 | 4.200 | 0 |
| **Leg.BattleTank** | Leg.Power | 27 | 6 | 4.200 | 0 |
| **Leg.LightTank** | Leg.Barracks | 202 | 6 | 2.700 | 0 |
| **Leg.LightTank** | Leg.DefensePlatform | 202 | 6 | 2.700 | 0 |
| **Leg.LightTank** | Leg.Power | 142 | 6 | 2.700 | 0 |
| **Leg.ScoutVehicle** | Leg.Barracks | 332 | 6 | 1.320 | 0 |
| **Leg.ScoutVehicle** | Leg.DefensePlatform | 492 | 6 | 1.320 | 4 |
| **Leg.ScoutVehicle** | Leg.Power | 222 | 6 | 1.320 | 0 |

**Nur 16 von 72 Belagerungen auf Waffenreichweite wurden entschieden**, gegen 72 von 72 auf Berührung. Dieselbe Ursache wie in der Gegentabelle: ohne Sicht kein Feuer.

## Laufart 3 · `movement` — Bewegung: vier Szenarien

Hindernisse sind Daten, nicht Code: eine Fussabdruckliste, eine Gruppe, ein Befehl.

**Überlauf im Szenario `standoff`** — Zellen, die Fernkämpfer mit Angriffsbefehl über die Entfernung hinaus vorrücken, auf der sie zum ersten Mal Schaden angerichtet haben.

| Fraktion / Rolle | Überlauf (nutzbar) | Reichweite | Sicht | Feuer ab | nächster Abstand | angekommen |
| --- | --: | --: | --: | --: | --: | --: |
| Alliance Artillery | 7 von 7 | 20 | 10 | 7 | 0 | 0/8 |
| Legion Artillery | 7 von 7 | 18 | 10 | 7 | 0 | 0/8 |

**Nur die erste Spalte ist Issue 03.** Der Abstand zwischen nominaler Reichweite und „Feuer ab" gehört der Aufklärung, nicht der Bewegung: `CombatSystem` verlangt das Ziel als sichtbar in der committed Team-Sicht. Ein Kontrolllauf, der die Gruppe auf voller Reichweite stehen liess, richtete über 2.000 Ticks null Schaden an — „auf Reichweite stehenbleiben" wäre also keine Verbesserung, sondern eine wirkungslose Waffe. Die Werte sind absichtlich fraktionsasymmetrisch (Allianz 20, Legion 18 Tiles). Im `standoff` ist „angekommen 0" kein Befund, sondern der Auftrag: die Gruppe greift an, sie reist nicht.

**Alle Szenarien, gemessene Rohwerte je Fraktion**

| Szenario | Fraktion | Rolle | Gruppe | angekommen | erster/letzter | Streuung | Weg | Luftlinie | blockiert | Durchlass | Überlauf (nutzbar) |
| --- | --- | --- | --: | --: | --: | --: | --: | --: | --- | --- | --: |
| Arrival | Alliance | BasicInfantry | 8 | 8 | 274/280 | 3 | 130 | — | — | — | — |
| Arrival | Legion | BasicInfantry | 8 | 8 | 274/280 | 3 | 130 | — | — | — | — |
| Blocking | Alliance | BasicInfantry | 16 | 16 | 158/178 | 2 | — | — | 0 Einh. · 0 Ticks | 2 Zellen @ y=63 | — |
| Blocking | Legion | BasicInfantry | 16 | 16 | 158/178 | 2 | — | — | 0 Einh. · 0 Ticks | 2 Zellen @ y=63 | — |
| Standoff | Alliance | Artillery | 8 | 0 | — | 0 | — | — | — | — | 7 |
| Standoff | Legion | Artillery | 8 | 0 | — | 0 | — | — | — | — | 7 |
| Detour | Alliance | BasicInfantry | 8 | 8 | 427/440 | 3 | 175 | 50 | — | 5 Zellen @ y=18 | — |
| Detour | Legion | BasicInfantry | 8 | 8 | 427/440 | 3 | 175 | 50 | — | 5 Zellen @ y=18 | — |

## Reproduktion

Alle Zahlen dieses Berichts stammen aus vier Kommandos und einem Berichtslauf:

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project tools/Nova.AiLab -c Release -- match --trace-every 50 --hash-every 500 --view-every 25 --fog --out tools/Nova.AiLab/out/match
dotnet run --project tools/Nova.AiLab -c Release -- duel     --out tools/Nova.AiLab/out/duel
dotnet run --project tools/Nova.AiLab -c Release -- movement --out tools/Nova.AiLab/out/movement
dotnet run --project tools/Nova.AiLab -c Release -- compare  --out tools/Nova.AiLab/out/compare
python3 tools/Nova.AiLab/report/build_reports.py tools/Nova.AiLab/out
```

---

Nova.AiLab ist lokales Werkzeug, kein Beitrag — es gerät in keinen `feat/`-Branch und wird nie gemergt. Diese Ergebnismenge ist an Commit `f13f4d5f` und Definitionstabelle `0x6326FA3E56CFF5A3` gebunden. Nach dem nächsten Merge-Fenster des Maintainers sind die Zahlen nicht mehr vergleichbar und werden neu vermessen, nicht über die Grenze hinweg verglichen.

*DIAGNOSIS — never proof. No scalar score, no ranking: the numbers sit side by side and a human picks.*
