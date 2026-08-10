# F001 · `Stop` löscht `AttackTarget` nicht — ein Angriffsbefehl ist unumkehrbar

**Gefunden am:** 2026-08-08 · **Beim Bauen von:** Zielwahl unterhalb der
Angriffsschwelle (Verhaltensjournal V003) · **Status: ERLEDIGT vom Inhaber,
2026-08-10** — siehe den Abschnitt ganz unten

> Fremdes Terrain. `Simulation/State/` ist Inhaberentscheidung mit D-ID und für
> uns gesperrt (`CLAUDE.md` §2). Dieser Befund beschreibt, er repariert nicht.
> Der Weg nach draussen ist Mail oder Issue.

## Beobachtung

`UnitState.AttackTarget` wird an genau drei Stellen geschrieben:

| Stelle | Wirkung |
|---|---|
| `UnitCommandStateView.cs:300` (Befehl `AttackTarget`) | setzt das Ziel |
| `CombatSystem.cs:207` (Auto-Acquisition, D-087) | setzt ein Ziel **nur, wenn das Feld leer ist** |
| `CombatSystem.cs:222` und `:328` | löscht, sobald das Ziel tot oder ungültig ist |

`Stop` löscht es **nicht**. Die Anwendungsstelle
`Assets/_Project/Scripts/Simulation/State/UnitCommandStateView.cs:264-286` ruft
`unit.Stop()`, setzt `HarvestFieldId = 0`, `IsReturningCargo = false` und
löscht den Reparaturauftrag — und trägt dabei den Kommentar:

> `// Stop cancels every standing order, economy and repair orders included;`
> `// the unit keeps its cargo.`

`UnitState.Stop()` (`Assets/_Project/Scripts/Simulation/State/UnitState.cs:140-145`)
setzt jedoch nur `TargetGridPos`, `GoalGridPos` und `IsMoving` zurück. Der
Angriffsbefehl überlebt jedes `Stop`.

## Warum das zählt

Es gibt damit **keinen Weg, eine Einheit an die Auto-Acquisition
zurückzugeben.** Für eine stehende Einheit ist das eine echte Verschlechterung
gegenüber gar keinem Befehl:

- `CombatSystem` Phase 2 überspringt jede Einheit mit belegtem `AttackTarget` —
  die Automatik greift nicht mehr.
- Phase 3 **hält** einen Befehl, dessen Ziel ausser Reichweite oder unsichtbar
  ist, statt ihn zu verwerfen.
- Eine Einheit, die nicht auf ihr Ziel zuläuft, feuert deshalb nicht mehr,
  sobald sich das Ziel entfernt — bis das Ziel stirbt.

Für einen **Spieler** ist derselbe Effekt erreichbar: Einheit auf ein Ziel
klicken, Ziel läuft weg, `Stop` drücken — die Einheit bleibt auf das
entfernte Ziel gerichtet und schiesst nicht auf den Gegner direkt vor ihr.
Ob das gewollt ist, ist eine Inhaberentscheidung; der Kommentar an der
Fundstelle sagt das Gegenteil.

## Gemessene Folge

Verhaltensjournal V003, Referenzpartie `ms1-canonical` gegen sich selbst,
Seed `0xA17E57DE57`, Tickbudget 27.000:

| Fassung | Entscheidungstick | Verluste 0/1 |
|---|---:|---:|
| ohne Zielbefehle unter der Angriffsschwelle | 8.715 | 70 / 97 |
| mit Zielbefehlen (beste von vier Fassungen) | 9.664 | 77 / 107 |

Die Zielwahl selbst ist dieselbe, die oberhalb der Schwelle nachweislich
**hilft** (V001: −33 % Entscheidungstick). Der Unterschied ist allein, dass die
Einheit dort auf ihr Ziel zuläuft und hier steht.

## Reproduktion

Kein Sonderaufbau nötig — der Befund ist am Code ablesbar (die drei Fundstellen
oben). Die gemessene Folge reproduziert man mit dem Labor:

```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project Nova.AiLab -c Release -- \
    match --trace-every 50 --hash-every 500 --out out/ref
```

MatchSpec: kanonische Zwei-Slot-Partie, `specVersion` 1, Seed `0xA17E57DE57`,
Tickbudget 27.000, beide Slots `ms1-canonical`; Definitionstabelle
`0x6326FA3E56CFF5A3`.

## Was wir uns wünschen würden — als Vorschlag, nicht als Änderung

Eine Möglichkeit, das Ziel freizugeben. Zwei Formen sind denkbar, beide
Inhaberentscheidung:

1. **`Stop` löscht `AttackTarget` mit** — passt zum bestehenden Kommentar,
   ändert aber Simulationsverhalten und damit die Determinismus-Baselines.
   Gehört nach der Trennungsregel in einen eigenen PR mit altem Wert, neuem
   Wert und Begründung.
2. **`AttackTarget` mit Ziel-Id 0** als „Ziel aufgeben" — additiv, ändert
   bestehendes Verhalten nicht, braucht aber einen `case` in `ValidateDomain`
   (den es für `AttackTarget` heute gar nicht gibt).

Wir setzen keins von beiden um.


---

## Erledigt — vom Inhaber, in PR #83 (`75973d3`)

**Nachgeprüft am 2026-08-10 am Checkout `5635009`.** Der Befund ist aufgelöst,
und zwar in der ersten der beiden vorgeschlagenen Formen:
`UnitCommandStateView` setzt im `Stop`-Zweig jetzt ausdrücklich
`unit.AttackTarget = EntityId.Invalid;`, und der Kommentar daneben sagt es auch
(„Stop cancels every standing order: attack, economy and repair included").
Der PR heisst entsprechend *surface blockers and clear attack targets*.

**Was das aufmacht.** Ein Angriffsbefehl ist nicht mehr unumkehrbar, und damit
fällt der Blocker unter [NEXT-STEPS §4](../NEXT-STEPS.md) und unter
[ROADMAP §4](../ROADMAP.md) weg: „Zielen unterhalb der Angriffsschwelle" ist
nicht länger dieselbe Sackgasse mit anderen Zahlen, weil eine stehende Einheit
ihr Ziel wieder abgeben kann. **Das heisst nicht, dass die vier Fassungen aus
V003 jetzt richtig wären** — sie sind gemessen und waren teurer als gar nicht
zielen, und der Grund dafür war strukturell. Eine fünfte Fassung ist damit
*erlaubt*, nicht *begründet*: sie braucht eine eigene Messung und den
Journaleintrag, der sagt, was sie besser und was sie schlechter macht.

**Eine Warnung bleibt.** Der Executor löscht das Ziel — die
D-087-Auto-Acquisition greift danach wieder zu. Wer eine Einheit vom Ziel
lösen will, muss also `Stop` einreichen und damit auch ihren Marschbefehl
verlieren; „nur das Ziel aufgeben, weiterlaufen" gibt es weiterhin nicht.
Ob das reicht, entscheidet eine Messung, nicht dieser Absatz.
