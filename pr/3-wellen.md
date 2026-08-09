> **Stufe 3 von 5 des Einheitenstrangs.**
> Reihenfolge: 1 Datenschicht → 2 Zielwahl → **3 Wellen** → 4 Rückzug → 5 HUD *(optional)*.
> **Setzt Stufe 2 voraus** (der Verhaltensbezeichner entsteht dort und wird hier
> auf `r3` gebumpt).

## Was der Spieler bisher sah

Kein Angriff, sondern ein Förderband: Soldat läuft los, stirbt, nächster Soldat
läuft los, stirbt. Man kann sich mit drei Einheiten an den Weg stellen und die
halbe Partie lang einen nach dem anderen abräumen.

## Die Regel

**Wer schon draussen ist, marschiert weiter; wer noch drinnen ist, wartet, bis
die Armee voll ist.**

- „Draussen" heisst ausserhalb eines Rings von 16 Zellen um das eigene HQ —
  gemessen am **HQ**, nicht am Ziel, damit eine Einheit nicht zwischen draussen
  und wartend kippt, weil der Gegner ein paar Zellen gelaufen ist.
- Der Sammelpunkt liegt zwölf Zellen vom HQ auf der Geraden zum Feindgebiet und
  ist für die ganze Partie derselbe: statisches Kartenwissen auf beiden Seiten,
  also kein Befehlsrauschen.
- Eine **wartende** Einheit bekommt bewusst **kein** Angriffsziel. Ein
  `AttackTarget` wird nur vom Tod des Ziels wieder frei — `Stop()` lässt es
  stehen —, also hielte eine stehende Einheit ein veraltetes Ziel und feuerte
  nicht mehr, während die D-087-Automatik geschossen hätte.
- Eine **angekommene** Einheit bekommt gar keinen Befehl, sonst geht derselbe
  Marschbefehl jede Kadenz neu hinaus.

`waveSize: 12` ist die Armeeobergrenze — die Regel als eine Zahl.

## Einseitig gemessen, weil anders nicht messbar

Eine Coderegel steckt im Binary und erreicht im Selbstspiel **beide** KIs
zugleich; dort ist „später entschieden, mehr Verluste" nicht von „zwei stärkeren
Armeen" zu unterscheiden. Deshalb trägt die Regel einen Profilwert mit
**Aus-Stellung**: `waveSize: 1` reproduziert das bisherige Verhalten bitgenau.
Gemessen wurde dasselbe Binary mit gegen ohne, in beiden Fraktionsrollen:

| Kennzahl | ohne Wellen | mit Wellen | |
|---|---:|---:|---|
| Verluste (Mittel) | 143 | **62** | −57 % |
| Austauschverhältnis | 84 | **131** | +56 % |
| Intervalle mit Verlusten | 59 | **18** | aus dem Tröpfeln werden Zusammenstösse |

## Der Wert gehört an den Rand, nicht in die Mitte

Über fünf Grössen gemessen: **eine halbvolle Welle ist schlechter als gar
keine.** `waveSize 6` liegt im Austausch bei 74 gegen 84 ohne Wellen. Sechs
Einheiten warten lange genug, um den Nachschub zu bremsen, und sind zu wenige,
um die Schlacht zu entscheiden — sie kommen zu spät und zu schwach.

## Das ist kein Endzustand

Heute ist „Welle oder Tröpfeln" eine Einstellung für die ganze Partie. Beide
Verhaltensweisen haben Lagen, in denen sie richtig sind: einzeln nachschieben,
wenn die Armee im Gefecht steht und jede Einheit sofort zählt oder der Gegner
vor der eigenen Basis steht — sammeln vor dem ersten Vorstoss und nach einer
verlorenen Welle. **Ziel ist, dass die KI das situationsabhängig entscheidet**,
statt dass ein Profilwert es vorgibt. Die gemessene Kurve ist die Begründung
dafür, dass es überhaupt eine Entscheidung ist und keine Geschmacksfrage. Der
Profilwert bleibt danach als Aus-Stellung erhalten, weil ohne ihn keine
einseitige Messung möglich wäre.

## Was sich ändert

| Datei | Was |
|---|---|
| `AI/SkirmishAiSystem.cs` | Haltung → Zuweisung je Einheit → gruppiertes Einreichen; Sammelpunkt, Ring, Wellentor |
| `AI.Data/AiProfile.cs`, `AiProfiles.cs` | `waveSize`, `stagingDistanceCells`, `stagingToleranceCells` |
| `AI.Data/AiBehaviorId.cs` | Revision 3 |
| `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` | Wellentest, Bezeichner-Pin nachgezogen |

Voraus geht eine **verhaltensneutrale Formänderung**: Schritt (6) erteilt keinen
Armeebefehl mehr, sondern löst in drei Stufen auf. Ohne sie ist „diese eine
Einheit wartet" nicht schwer zu formulieren, sondern **nicht formulierbar**. Der
Nachweis ist keine Testzusage, sondern eine Zahl: Entscheidungstick und
Endzustand bleiben identisch, der Bezeichner-Pin geht ohne Änderung durch.

Der neue Test prüft beide Hälften an den **Positionen**, nicht an den Intents:
niemand verlässt den Ring, solange die Welle nicht voll ist, und bei voller Welle
verlässt ihn jemand. Mit `waveSize 1` fällt er — so muss ein Test einer neuen
Regel aussehen.

## Kein neues System

Alles liegt in `SkirmishAiSystem`, das zwischen Combat und Victory bereits
eingeordnet ist. Die Tick-Reihenfolge ist nicht angefasst, `MatchRunner` nicht.

## Tests

**564/564 grün**, die vier Determinismus-Baselines **nicht angefasst**.

## Im laufenden Spiel gesehen

**Nicht geprüft.** Was man sehen müsste: kommt die Armee als Welle oder als
Kette, und kündigt sich die zweite Welle an.

## Checkliste

- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — **564/564**
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [ ] ~~Echte Entscheidung getroffen? → D-ID~~ — keine Entscheidung dieser Art, gestrichen
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement
