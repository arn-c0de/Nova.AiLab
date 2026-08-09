> **Stufe 1 von 5 des Einheitenstrangs — diese zuerst.**
> Reihenfolge: **1 Datenschicht** → 2 Zielwahl → 3 Wellen → 4 Rückzug → 5 HUD *(optional)*.
> Die Branches sind gestapelt: 2 zweigt von 1 ab, 3 von 2 und so weiter. Wird 1
> gemergt, rebased sich 2 von selbst. Jede Stufe ist für sich grün und für sich
> lauffähig.

## Was & Warum

Die Stellschrauben der Skirmish-KI lagen an zwei Orten: als Konstruktor-Defaults
auf `AiFactionProfile` und als `const`-Felder in `SkirmishAiSystem`. Tunen hiess
damit Verhaltenscode editieren, und vier Werte — Kadenz, Suchradius, beide
Queue-Batches — waren von aussen gar nicht erreichbar. Sie liegen jetzt
vollständig in einem `AiProfile` unter `Nova.AI.Data`.

**Dieser PR ändert kein Verhalten.** Er ist bewusst der erste: er läuft den
ganzen CI-Weg samt der neuen Wächter einmal durch, ohne dass Verhalten zur
Debatte steht.

## Der Nachweis, dass er neutral ist

Das ausgelieferte Profil `ms1-canonical` trägt die bisherigen acht Zahlen
wertgleich — Strommarge 0, Armee 12, Angriffsschwelle 6, Harvester 2, Kadenz 20,
Suchradius 8, beide Batches 2. Nichts wurde gerundet oder „verbessert". Die vier
Determinismus-Baselines bleiben grün, und genau das ist der Nachweis.

## Was sich ändert

| Datei | Was |
|---|---|
| `AI.Data/AiProfile.cs` *(neu)* | der Werttyp, `readonly struct`, Gleichheit über **alle** Werte |
| `AI.Data/AiProfiles.cs` *(neu)* | `ms1-canonical` und `legacy-defaults` |
| `AI.Data/Nova.AI.Data.asmdef` | `noEngineReferences: true` |
| `AI/AiFactionProfile.cs` | dünner Griff über ein `AiProfile`, **Signatur unverändert** |
| `AI/Nova.AI.asmdef` | Referenz auf `Nova.AI.Data` |
| `AI/SkirmishAiSystem.cs` | liest die Zahlen aus dem Profil; zwei veraltete Doku-Behauptungen korrigiert |
| `tools/Nova.SimRunner.Tests/AiProfileTests.cs` *(neu)* | 8 Tests |
| `tools/Nova.SimRunner.Tests/…csproj` | `AI.Data/` in die geteilten Quellen |

`MatchRunner` ist **nicht** angefasst — deshalb blieb die Signatur von
`AiFactionProfile` unverändert.

Zwei Vorarbeiten sind mit erledigt: `AiFactionProfile` verglich bisher **nur den
Fraktionsnamen**, sodass zwei Profile mit gleichem Namen und verschiedenen Zahlen
als gleich galten — was erst beim Tunen auffällt, wo genau das der Regelfall ist.
Und `Nova.AI.Data` ist jetzt strukturell enginefrei statt zufällig, kann also
keine `FactionId` nennen und unbemerkt zur zweiten Definitionstabelle werden.

## Tests

`dotnet test tools/Nova.SimRunner.Tests -c Release` → **561/561 grün**, die vier
Determinismus-Baselines inbegriffen und **nicht angefasst**.

## Im laufenden Spiel gesehen

Nichts — dieser PR ändert kein Verhalten, es gibt nichts zu sehen. Die Stufen 2
bis 4 ändern Verhalten und sagen dort jeweils selbst, dass sie ungespielt sind.

## Was noch kommt

| Stufe | Inhalt | Verhalten | Stand |
|---|---|---|---|
| **1** | **Datenschicht** | unverändert | **dieser PR**, 561/561 |
| 2 | Zielwahl nach Score, Verhaltensbezeichner | `r1`→`r2` | gebaut, 562/562, einseitig gemessen |
| 3 | Angriff in Wellen | `r2`→`r3` | gebaut, 564/564, einseitig gemessen |
| 4 | Rückzug angeschlagener Einheiten | `r3`→`r4` | gebaut, 565/565, einseitig gemessen |
| 5 | Drei Werkzeuge zum Zusehen | Anzeige | gebaut, **optional**, fasst fremdes Terrain an |

Alle fünf sind gebaut, getestet und zusammen als Integrationsstand geprüft
(565/565, Determinismus über 24 Seeds ohne Abweichung). Sie kommen einzeln,
damit jede für sich beurteilbar bleibt.

## Checkliste

- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — **561/561**
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement
