## Was & Warum

Die Wellenlogik bewertet eine „volle Welle" bisher ausschließlich nach der **Anzahl** gesammelter Einheiten, und eine Anzahl bildet die Kampfstärke der Fraktionen nicht ab: zwölf Allianz-Schützen entsprechen 1200 Kampfpunkten, zwölf Legions-Rekruten nur 528 — die Legion greift unter derselben Regel also mit rund 44 % der Stärke an, die dieselbe Regel der Allianz gibt. In der Referenzpartie steht das in der Verlustspalte: 51 gegen 23.

Dieser PR führt `CombatStrength` ein (Schaden × Leben / Feuerintervall, ganzzahlig) und lässt die Wellenfreigabe über das neue Profilfeld `waveStrengthPoints` **Kampfpunkte summieren statt Köpfe zu zählen**. Ziel ist nicht, die Armeeobergrenze zu verändern, sondern Wellenstärke und Kopfzahl zu **entkoppeln** — erst dadurch lässt sich `targetArmySize` später unabhängig bewegen.

**Im ausgelieferten Profil ist die Regel absichtlich noch schlafend, und ein Test hält das fest.** Die kanonische Partie bleibt byteidentisch: Endzustand Tick `2548` / `0x14472B2B943ED2BB`, KI-gegen-KI-Laborlauf Tick `5773` / `0x2B34B4E194257940`. Nur der Bezeichner bewegt sich (`r5` → `r6`).

**Im laufenden Spiel gespielt.** Eine Partie mit diesem Stand zeigt keinen Unterschied zum bisherigen Verhalten — und genau das ist das erwartete Ergebnis, weil das Tor bei der ausgelieferten Armeeobergrenze nachweislich identisch zur Kopfzahl entscheidet. Die gespielte Partie bestätigt damit die Neutralität; sie belegt keine Verbesserung. Sichtbar wird der Effekt erst mit den darauf aufbauenden Änderungen.

<details>
<summary><b>Die Regel im Detail</b></summary>

`CombatStrength` ist eine deterministische Ganzzahlbewertung einer Einheit:

`Schaden * Leben / Feuerintervall`

Genau eine Ganzzahldivision mit festgelegter Abschneidung. Unbewaffnete Rollen ergeben ohne Sonderfall automatisch `0`. Die Werte kommen aus `WeaponProfiles`; die Definitionstabelle wird gelesen, nicht geändert.

| Rolle, volle Gesundheit | Allianz | Legion |
|---|---:|---:|
| BasicInfantry | **100** | **44** |
| ScoutVehicle | 264 | 180 |
| LightTank | 962 | 672 |
| AntiArmorInfantry | 200 | 144 |
| BattleTank | 2640 | 2500 |
| Artillery | 550 | 274 |

Der ausgelieferte Wert von `waveStrengthPoints` beträgt `1200`; `0` deaktiviert die Regel bitgenau und stellt den Zählpfad wieder her.

**Die bestehende r5-Regel wandert in Punkten mit.** Der Schwellwert bleibt auf das begrenzt, was die Produktion unter der aktuellen Armeeobergrenze noch liefern kann — ein Überlebender außerhalb des Sammelrings darf die nächste Welle nicht auf Verstärkung warten lassen, die gar nicht mehr gebaut werden kann. Ein freier Platz zählt dabei nur, solange eine Kaserne hineinbauen kann; sonst wartet die Welle die Partie aus.

**Kredite gehen bewusst nicht ein.** Zahlungsunfähigkeit ist vorübergehend; ein von der Kasse abhängiges Tor würde die Armee bei jeder Prüfkadenz neu einordnen — die bereits dokumentierte Problematik aus Journal V002.

</details>

<details>
<summary><b>Warum die Arithmetik in einem eigenen Typ liegt</b></summary>

Die Schwellwertarithmetik liegt in `WaveStrengthGate` als reine Funktion. Dadurch lassen sich Zustände direkt testen, die eine vollständige Partie nicht zuverlässig auf Bestellung erzeugt:

* mehr lebende Einheiten, als die aktuelle Armeeobergrenze zulässt,
* eine gerade zerstörte Kaserne,
* ein durch die Stärkeschwelle statt durch die Kopfzahl begrenztes Tor.

Die Trennung folgt aus einer **Mutationsprobe**: Solange die Arithmetik im System selbst vergraben war, blieb die Testsuite sowohl beim Entfernen des Negativ-Clamps als auch beim vollständigen Ignorieren von `waveStrengthPoints` grün. Beide Mutationen fallen jetzt.

</details>

<details>
<summary><b>Was die Schlafstellung festnagelt</b></summary>

Die Punktklausel kann nur entscheiden, solange noch ein Platz unter der Armeeobergrenze frei ist: elf Schützen sind 1100 Punkte gegen eine Schwelle von 1200. Dadurch entscheidet das Tor weiterhin exakt wie die bisherige Kopfzahlregel.

**Die Reserve zur Aktivierung beträgt neun Punkte pro Schütze.** Der Test berechnet diesen Wert aus `CombatStrength`, statt eine Konstante abzuschreiben — damit reagiert er auch auf spätere Änderungen der Waffenwerte, und bereits ein zusätzlicher Schadenspunkt würde die neue Klausel aktivieren. Waffenwerte sind der Auftrag genau dieses Strangs, deshalb ist das kein theoretischer Fall.

</details>

<details>
<summary><b>Laborbefund zur späteren Weiterarbeit — ausdrücklich keine Änderung dieses PRs</b></summary>

* nur Armeeobergrenze erhöhen: eigene Verluste `51 → 64`, Austausch `45 → 34`
* Stärketor plus Obergrenze `30`: Entscheidung bei Tick `5005` statt `5773`, `23` eigene Verluste, Austausch `139`

**Vor einer solchen Aktivierung gehört eine Abbruchregel.** Im Labor steigt die Aussetzzeit am Sammelpunkt von `3.502` auf `12.326` Einheit-Ticks je `1.000`, einzelne Einheiten warten dabei bis zu `3.214` Ticks. Der zugrunde liegende Defekt — die KI sammelt weiter, während ihr eigenes Hauptquartier beschossen wird — ist **älter als dieser PR und durch ihn unverändert**, aber eine höhere Obergrenze vergrößert seine Fläche.

</details>

<details>
<summary><b>Rückfrage: <code>MatchRunner</code> überschreibt vier Profilwerte</b></summary>

`MatchRunner` holt fünfzehn der neunzehn Profilwerte über den historischen `AiFactionProfile`-Konstruktor aus `AiProfiles.Ms1Canonical` und **überschreibt vier mit eigenen Literalen**, darunter `targetArmySize`. Die Datei gehört dem Netzstrang und wurde nicht angefasst.

Diese vier Werte konnten still auseinanderlaufen: der Profil-Hash rechnet über `Ms1Canonical`, der Endzustands-Pin spiegelt MatchRunners Literale — beide bestehenden Wächter sehen von je einer Seite an der Lücke vorbei. Ein neuer Test liest den Quelltext von `MatchRunner` und schlägt fehl, sobald die vier abweichen. Gelesen, nicht geändert.

</details>

## Checkliste

* [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — `649/649`
* [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
* [x] ~~Echte Entscheidung getroffen? → D-ID im [DecisionLog](../docs/production/DecisionLog.md), sonst streichen~~ — gestrichen: keine Inhaberentscheidung getroffen. Offen bleiben die Armeeobergrenze, die als Literal in `MatchRunner` liegt, und die Frage, ob ein KI-Profil überhaupt in den Match-Fingerprint eingehen soll. Letzteres nennt `AiBehaviorId` selbst eine Inhaberentscheidung mit Auswirkungen auf Simulation und Replays; dieser PR beantwortet sie ausdrücklich nicht.
* [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

Determinismusprüfung: Exit `0`. Keine Baseline geändert.

## Externe Beiträge

- [x] I agree to the Contributor License Agreement
