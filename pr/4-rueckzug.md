> **Stufe 4 von 5 des Einheitenstrangs.**
> Reihenfolge: 1 Datenschicht → 2 Zielwahl → 3 Wellen → **4 Rückzug** → 5 HUD *(optional)*.
> **Setzt Stufe 3 voraus** — ein Rückzug braucht einen Ort, zu dem er zurückläuft,
> und das ist der Sammelpunkt von dort.

## Was der Spieler bisher sah

Man schiesst auf die KI, und sie merkt es nicht. Angeschlagene Einheiten kämpfen
bis zum letzten Lebenspunkt.

## Die Regel

Eine Einheit unter **60 % Leben**, in deren Nähe (8 Zellen) ein **bewaffneter**
Feind sichtbar ist, läuft zum Sammelpunkt zurück — auch wenn sie längst draussen
ist. Zu Hause ist sie eine normale wartende Einheit und zieht mit der nächsten
Welle wieder los. Ein unbewaffneter Harvester am Zaun löst nichts aus:
Überreaktion auf Belangloses ist der Fehlermodus, an dem ein früherer
Verteidigungszweig gescheitert ist.

Ein zurückweichender Soldat bekommt kein Angriffsziel; die D-087-Automatik
schiesst weiter auf das, was ihn verfolgt.

## Bewusst ohne Lebens-Hysterese

Eintritt bei 25 % und Austritt bei 60 % setzt voraus, dass eine Einheit heilt.
**In MS-1 heilt keine:** `ValidateRepair` verlangt als Ziel eine fertiggestellte
Platzierung, also ein Gebäude, und die einzige Stelle im Repo, die Einheitenleben
erhöht, ist `EvolvedFactionSystem` — G2-Prototyp, in keiner MS-1-Partie
registriert. Ein Austrittswert wäre nie erreicht worden: Verwundete hätten sich
zu Hause gestapelt, die Armeeobergrenze belegt und die Welle nie wieder voll
werden lassen. Gedämpft wird deshalb über **Gefahr und Entfernung** statt über
Leben.

## Einseitig gemessen

Gegen dasselbe Binary mit `retreatHealthPercent: 0`, beide Fraktionsrollen:

| Kennzahl | ohne Rückzug | mit Rückzug |
|---|---:|---:|
| Austauschverhältnis | 89 | **131** |
| Verluste (Mittel) | **56** | 62 |
| Entscheidungstick | **7.125** | 9.164 |

**Die Regel kostet Tempo, nicht Einheiten** — ohne sie entscheidet die Partie
2.000 Ticks früher und mit geringfügig *weniger* eigenen Verlusten. Bezahlt wird
das mit dem Austausch: ohne Rückzug stirbt die Armee schneller und nimmt weniger
mit. Das ist ein Handel, kein reiner Gewinn, und er gehört genauso in den Text
wie der Gewinn.

Die Schwelle ist über fünf Stufen gemessen und nicht gewählt: 25, 40, 60, 75, 90.
Bei 75 liegt der Austausch höher (166), erkauft das aber mit 0 % Siegen und einer
Partie über 17.770 Ticks — **eine Kennzahl allein hätte hier die schlechtere KI
gewählt.**

## Was das kostet, offen gesagt

Ein Spieler kann mit einer einzelnen billigen Einheit eine ganze Welle nach Hause
schicken. Im Labor tritt das nicht auf, weil keine Seite absichtlich ködert;
gegen einen Menschen ist es die naheliegende Gegenstrategie und **ungemessen**.
Wie bei den Wellen gilt: Der feste Schwellwert ist eine Zwischenstufe — ob ein
Rückzug richtig ist, hängt von der Lage ab, und die soll die KI später selbst
beurteilen.

## Was sich ändert

| Datei | Was |
|---|---|
| `AI/SkirmishAiSystem.cs` | Rückzugszweig vor allem anderen; sichtbare bewaffnete Feinde einmal je Entscheidung gesammelt |
| `AI.Data/AiProfile.cs`, `AiProfiles.cs` | `retreatHealthPercent`, `retreatDangerCells` |
| `AI.Data/AiBehaviorId.cs` | Revision 4 |
| `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` | Rückzugstest, Bezeichner-Pin nachgezogen |

Der Bezeichner-Pin **kann diese Regel nicht sehen**: sein Gegner ist passiv und
besitzt keine bewaffnete Einheit, also wird nie eine Bedrohung sichtbar und nie
jemand zurückgezogen. Der Endzustand bleibt byte-identisch — ein Pin, der seine
Arbeit tut, und ein Argument für den zweiten Test, nicht gegen ihn. Der prüft an
den Positionen: eine verwundete Einheit mit einem bewaffneten Feind in
Reichweite läuft näher an ihr eigenes HQ heran, als sie steht.

## Tests

**565/565 grün**, die vier Determinismus-Baselines **nicht angefasst**.

## Im laufenden Spiel gesehen

**Nicht geprüft.** Was man sehen müsste: drehen angeschlagene Einheiten sichtbar
ab, und sieht das nach Absicht aus oder nach Fehler.

## Checkliste

- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — **565/565**
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [ ] ~~Echte Entscheidung getroffen? → D-ID~~ — keine Entscheidung dieser Art, gestrichen
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement
