> **Stufe 2 von 5 des Einheitenstrangs.**
> Reihenfolge: 1 Datenschicht → **2 Zielwahl** → 3 Wellen → 4 Rückzug → 5 HUD *(optional)*.
> **Setzt Stufe 1 voraus** (die Zielgewichte sind Profilfelder). Der Branch
> zweigt von Stufe 1 ab und rebased sich von selbst, sobald die gemergt ist.

## Was & Warum

Die Zielwahl lautete „HQ, sonst das **erste** sichtbare Gebäude, sonst die
**erste** sichtbare Einheit". Diese Reihenfolge ist die des Sichtbarkeitsscans,
also der Entitätsindex — die Armee lief an einem Panzer vorbei, um ein Lagerhaus
zu beschiessen. Kinetischer Schaden landet auf Medium mit 50 % und auf Building
mit 30 %, und die alte Regel konnte das nicht sehen.

Stattdessen ein ganzzahliger Score aus vier Profilgewichten:

```
score = W_dmg    · Ø DamageMatrix.Resolve(Waffe, Rüstungsklasse Ziel)
      + W_threat ·   Waffenschaden des Ziels
      + W_finish ·   fehlende Lebenspunkte in Prozent
      - W_dist   · Ø Chebyshev-Abstand Armee → Ziel
Gleichstand → niedrigere rohe Entity-Id, nie die Listenposition
```

Das feindliche **HQ bleibt ein Kurzschluss** und ist bewusst kein Gewicht: sein
Verlust entscheidet die Partie (D-077), und eine Siegbedingung ist keine
Vorliebe, die ein Gewicht überstimmen darf.

Eigene Einheiten filtert diese Stelle — und nur sie. `ValidateDomain` hat für
`AttackTarget` keinen Case, und die Feuerphase prüft Reichweite und Sicht, nie
den Besitzer: ein ausdrücklicher Angriffsbefehl auf eine eigene Einheit **würde**
feuern.

## Gemessen

Referenzpartie `ms1-canonical` gegen sich selbst, Seed `0xA17E57DE57`:

| Kennzahl | vorher | nachher | |
|---|---:|---:|---|
| Entscheidungstick | 12.975 | **8.715** | −33 % |
| Verluste Slot 0 | 113 | **70** | −38 % |
| Verluste Slot 1 | 137 | **97** | −29 % |
| Eingereichte Intents | 443 / 578 | 343 / 363 | weniger Rauschen |

Beide Seiten verlieren weniger, obwohl beide dasselbe neue Zielverhalten fahren.

**Nicht für jedes Profil eine Verbesserung, und das gehört hierher:**
`greedy-economy` entscheidet 8.635 → 11.470 bei 86 → 114 Verlusten,
`fast-cadence` 11.454 → 12.948 bei 110 → 132. Bei zwei Partien je Kandidat ist
ein Siegquotensprung von 50 Punkten allerdings genau **eine** gekippte Partie —
diese Spalte trägt keine Tendenz.

## Was sich ändert

| Datei | Was |
|---|---|
| `AI/SkirmishAiSystem.cs` | `FindPreferredVisibleEnemy` → `FindBestVisibleEnemyByScore` plus `ScoreTarget` |
| `AI.Data/AiProfile.cs`, `AiProfiles.cs` | vier Gewichte, ohne Konstruktor-Vorgabewerte |
| `AI.Data/AiBehaviorId.cs` *(neu)* | der Verhaltensbezeichner |
| `AI/AiFactionProfile.cs` | reicht die Gewichte durch |
| `Presentation/UI/DebugHud.cs` | eine Zeile: `AI behaviour r2.…` |
| `Presentation/UI/Nova.Presentation.UI.asmdef` | Referenz auf `Nova.AI.Data` |
| `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` | Bezeichner-Pin und ein Zielwahltest |

**`AiBehaviorId`** beantwortet „welche KI ist das" in einem String, den man von
einem Screenshot ablesen kann: eine von Hand gebumpte Revision für Coderegeln,
ein Hash über alle Profilzahlen für die Werte. Ein Test nagelt ihn zusammen mit
dem Endzustand der kanonischen Partie fest — Verhalten ändern ohne Bump wird rot.
Er steht im F3-Panel, weil dieses Repo eine gesehene Runde als Nachweis verlangt
und ein Screenshot sonst nicht sagt, was er zeigt.

Der neue Test ist so geschrieben, dass die **alte** Regel ihn reisst: Ein
Harvester (zuerst gespawnt, also niedrigerer Index) und ein BattleTank erscheinen
gleichzeitig neben der Armee. Der End-to-End-Test blieb grün, während sich die
Partie um 4.260 Ticks verschob — er prüft Ausgang und Sieger, nie die Wahl.

## Tests

**562/562 grün.** Die vier Determinismus-Baselines sind **nicht angefasst** — und
sie werden von einer reinen KI-Änderung auch nicht rot: sie erwähnen `SkirmishAi`
mit keiner Zeile und fahren kein KI-System. Die Trennungsregel gilt trotzdem.

## Im laufenden Spiel gesehen

**Nicht geprüft.** Alle Zahlen oben stammen aus headless-Läufen und sind
Diagnose, kein Nachweis.

## Checkliste

- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — **562/562**
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement
