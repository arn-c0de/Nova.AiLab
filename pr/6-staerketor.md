# PR-Beschreibung — `feat/ai-strength-wave-gate-de`

> **Diese Datei ist der PR-Text zum Einfügen, 1:1.** Sie folgt der Vorlage des
> Repos; alles unterhalb der Checkliste ist eingeklappt, damit der PR kurz
> bleibt und die Belege trotzdem dranhängen.
>
> **Vom FORK** `arn-c0de/Project_Nova` (`feat/ai-strength-wave-gate-de`,
> `9079c7d`) **ins HAUPTREPO** `VibecodingGermany/HashKrieg` → `main`.
> Bezeichner `r5.779A1B5B` → `r6.E34435F9`.

## Titel des PR

```
KI: Die Welle misst Kampfstärke statt Kopfzahl (r6)
```

> [!IMPORTANT]
> **Die Beschreibung muss ersetzt werden.** GitHub füllt sie beim Öffnen mit
> dem Rumpf der Commit-Nachricht vor; der ist zwar deutsch, folgt aber nicht der
> PR-Vorlage. Alles ab „Was & Warum" hier einsetzen. Der Titel oben ist kürzer
> als die Commit-Zeile und passt besser in eine PR-Liste.

> [!NOTE]
> **Warum die Commit-Nachricht deutsch ist, obwohl `CLAUDE.md` §6 Englisch
> vorsieht.** §6 teilt die Sprachen auf — Deutsch für Projektinhalte und
> PR-Texte, Englisch für Code, Identifier, Pfade und Commit-Messages. Hier ist
> das auf ausdrückliche Entscheidung des Beitragenden anders. Der Branchname
> bleibt englisch, weil er ein Identifier ist.
>
> Der ältere Branch `feat/ai-strength-wave-gate` (`308d8cf`) liegt mit
> englischer Nachricht noch im Fork und trägt **denselben Baum, byteweise**.
> Er ist gegenstandslos, sobald dieser hier verwendet wird.

---

## Was & Warum

Die Welle marschierte auf einer **Anzahl** gesammelter Einheiten, und eine Anzahl
weiss nicht, was eine Einheit wert ist: zwölf Allianz-Schützen wiegen 1.200
Kampfpunkte, zwölf Legions-Rekruten 528 — dieselbe Regel nennt beides „volle
Welle", und die Legion greift mit 44 % der Angriffsstärke an, die sie der
Allianz gibt (Verluste 51 gegen 23). Das Tor summiert jetzt Kampfpunkte statt
Köpfe.

**Ausgeliefert ändert das noch kein Verhalten, und ein Test nagelt das fest:**
die Punktschwelle kann erst greifen, wenn die Armeeobergrenze höher liegt als
heute, und die liegt in `MatchRunner` — also nicht bei uns. Der Endzustands-Pin
steht unverändert bei Tick 2.548 / `0x14472B2B943ED2BB`. Was der Schritt bringt,
ist die **Entkopplung**: die Wellenschwelle hängt nicht mehr an der
Produktionsobergrenze, und das ist die Voraussetzung dafür, an dieser Zahl
überhaupt drehen zu dürfen.

**Im laufenden Spiel gesehen: nichts.** Alle Zahlen unten sind Labormessung —
Diagnose, kein Nachweis.

## Checkliste

- [x] `dotnet test tools/Nova.SimRunner.Tests` lokal grün — **649/649**
- [x] Zeile unter `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [ ] ~~Echte Entscheidung getroffen? → D-ID im DecisionLog~~ — **gestrichen:**
      hier wurde keine Inhaberentscheidung getroffen. Die eine Frage, die eine
      wäre (Armeeobergrenze), steht unten als Rückfrage und **nicht** als
      Änderung.
- [x] Bei Simulationsänderung: keine Determinismus-Baseline im selben PR geändert

## Externe Beiträge

- [ ] I agree to the Contributor License Agreement

---

<details>
<summary><b>Die Regel, in einem Absatz</b></summary>

```
S(u)       = AttackDamage(u) × CurrentHealth(u) / AttackCooldownTicks(u)

erreichbar = max(0, TargetArmySize − draussen − sammelnd) × S(Rekrut, voll)
             // 0, wenn keine Kaserne steht
WaveReady  = S_sammelnd >= min(WaveStrengthPoints, S_sammelnd + erreichbar)
```

Ganzzahlig, **eine** Division, eine festgeschriebene Abschneidung (Allianz
LightTank ist 962, nicht 963). Unbewaffnet ist `AttackDamage == 0` und ergibt 0 —
Builder, Harvester und acht der neun Gebäudearten fallen ohne Sonderfall heraus.
Die Werte kommen aus `WeaponProfiles`; die Definitionstabelle wird **gelesen,
nicht geändert** (`0x6326FA3E56CFF5A3` unverändert).

| Rolle, volle Gesundheit | Allianz | Legion |
|---|---:|---:|
| BasicInfantry | **100** | **44** |
| ScoutVehicle | 264 | 180 |
| LightTank | 962 | 672 |
| AntiArmorInfantry | 200 | 144 |
| BattleTank | 2.640 | 2.500 |
| Artillery | 550 | 274 |

**Die r5-Regel wandert mit, in Punkten statt in Köpfen.** Ohne den Deckel kehrt
die Blockade zurück, die `f13f4d5` beseitigt hat: Überlebende früherer Wellen
belegen die Obergrenze, also kann die Produktion die Schwelle nie mehr liefern.
Ein freier Kopf zählt dabei nur, solange eine **Kaserne** hineinbauen kann —
sonst wartet die Welle die Partie aus. Kredite gehen bewusst nicht ein: pleite
ist vorübergehend, und ein Tor, das mit der Kasse flackert, ordnet die Armee
jede Kadenz neu (Journal V002).

**Aus-Stellung:** `waveStrengthPoints: 0` ⇒ der Zählpfad, unverändert, Bit für
Bit. Ohne sie ist die Regel im Selbstspiel nicht messbar — eine Coderegel
erreicht beide Seiten zugleich.

</details>

<details>
<summary><b>Warum das ausgeliefert nichts ändert — und was das festnagelt</b></summary>

Der Schwellwert ist gegen das gekappt, was die Produktion noch liefern kann. Die
Punktklausel kann deshalb nur entscheiden, solange **noch ein Kopf der
Armeeobergrenze frei** ist — also bei elf Schützen, 1.100 Punkten, gegen eine
Schwelle von 1.200. Das Tor entscheidet damit exakt wie die Kopfzahl, die es
ersetzt.

- Endzustands-Pin `SkirmishAiTests`: Tick **2.548**, `0x14472B2B943ED2BB` —
  unverändert.
- KI-gegen-KI-Lauf im Labor: Tick **5.773**, `0x2B34B4E194257940` — mit Tor und
  mit `waveStrengthPoints: 0` **byteidentisch**.
- Nur `AiBehaviorId.Value` bewegt sich.

**Die Reserve ist neun Punkte pro Schütze.** Deshalb rechnet der Dormanz-Test die
Ungleichung `(Kappe − 1) × stärkste produzierte Einheit < Schwelle` aus
`CombatStrength` aus statt aus einer abgeschriebenen Zahl: Waffenwerte sind der
Auftrag genau dieses Strangs, und **ein Schadenspunkt mehr beim Allianz-Schützen
weckt das Tor**. Eine frühere Fassung des Tests hätte genau das verschlafen.

</details>

<details>
<summary><b>Gemessen — einseitig, im Labor</b></summary>

Dasselbe Binary, **ein** Profilwert Unterschied, je Zeile ist **ein** Sitz
umgestellt. Heutige Auslieferung auf beiden Sitzen: Tick 5.773, Verluste 23 / 51,
Austausch 221 / 45, grösste Armee 12 / 12.

| Sitz | Stellung | Tick | Sieger | eig. Verluste | Austausch | grösste Armee |
|---|---|---:|---|---:|---:|---:|
| **Legion** | *(heute)* | 5.773 | Allianz | 51 | 45 | 12 |
| **Legion** | Obergrenze 30 **allein** | 5.921 | Allianz | **64** | **34** | 16 |
| **Legion** | Obergrenze 30 **+ Tor** | 5.005 | **Legion** | 23 | **139** | **30** |
| Allianz | *(heute)* | 5.773 | Allianz | 23 | 221 | 12 |
| Allianz | Obergrenze 30 + Tor | 3.914 | Allianz | 12 | 225 | 23 |

Die tragende Zeile ist die zweite: **die Obergrenze allein macht die Legion
schlechter.** Mehr Bauplatz nützt nichts, solange die Welle bei zwölf Köpfen
losläuft, egal was zwölf Köpfe wiegen.

`intentsRejected` bleibt **0** über alle Laborkandidaten.

**Was schlechter wird**, und es steht hier, weil es dazugehört:

- **APM steigt deutlich** (Legionssitz 13 → 29, Allianzsitz 29 → 46). Das ist die
  Kennzahl, an der `DefendBase` gescheitert ist (Journal V002).
- **Der Allianz-Sitz gewinnt durch das Tor nichts** — die Obergrenze ohne Tor ist
  dort besser. Die Allianz zahlt das Tor, damit die Legion es bekommt. Bei
  Obergrenze 12 kostet es sie nichts, weil dort nichts passiert.

</details>

<details>
<summary><b>Rückfrage: die Armeeobergrenze liegt in eurer Datei — und ist noch nicht reif</b></summary>

Damit das Tor greifen kann, muss die Armeeobergrenze bei mindestens **29**
liegen: 1.200 Punkte sind 28 Legions-Rekruten zu je 44, und die Punktklausel
entscheidet nur, solange noch ein Kopf frei ist. Diesen Wert überschreibt
`MatchRunner` mit einem eigenen Literal (`MatchRunner.cs:254`), und
`Scripts/Gameplay/Match/` ist Netzstrang. **Wir fassen das nicht an.**

Der Vorschlag wäre:

```csharp
targetArmySize: 30,   // war 12
```

**30, weil es aus dem Schwellwert folgt**, nicht weil es der beste Punkt der
Kurve ist. Unter 29 fällt die Welle *nicht* auf eine Kopfzahl zurück, sondern
degeneriert zu „sammle die gesamte Armeeobergrenze" — das sind die
Zermürbungspartien bei 22, 24 und 28 (78, 178 und 154 eigene Verluste). 30 ist
die erste Stellung, in der die Schwelle greift und zwei Köpfe zum Nachbauen frei
bleiben. Der Kurve ist nicht zu trauen: **Obergrenze 20 gewinnt, 19 und 21
verlieren beide.**

> [!WARNING]
> **Diese Änderung ist noch nicht reif, und das ist ein Befund von uns, nicht
> von euch.** Beim Ansehen einer Laboraufnahme fiel auf: die KI sammelt am
> Sammelpunkt weiter, während ihr eigenes HQ beschossen wird. Nachgemessen — in
> der **ausgelieferten** KI stehen in 19 % der Zeit, in der ihr HQ unter Feuer
> liegt, mindestens drei eigene Einheiten am Sammelpunkt, im Spitzenwert neun.
> Ursache: eine angekommene Verstärkung bekommt **gar keinen Befehl** (Absicht,
> gegen Intent-Churn), hängt also an der Auto-Zielerfassung — und die reicht
> 6 Zellen, während der Sammelpunkt 12 Zellen vom HQ entfernt liegt.
>
> Der Defekt ist **älter als dieser PR** und durch ihn unverändert. Aber die
> Obergrenze 30 **verdreifacht die Fläche**, auf der er auftritt: die Zeit, die
> Einheiten wartend herumstehen, steigt von 3.502 auf 12.326 Einheit-Ticks je
> 1.000 Ticks, und eine einzelne Einheit wartet bis zu 3.214 Ticks.
>
> **Deshalb bitten wir nicht um die Obergrenze, sondern melden sie an.** Vorher
> gehört eine Abbruch-/Verteidigungsregel gebaut. Reihenfolge also: dieser PR →
> Abbruchregel → Obergrenze.

</details>

<details>
<summary><b>Zwei Funde, die euch gehören</b></summary>

**1. `MatchRunner`s vier Literale konnten still von `AiProfiles` abdriften.**
`MatchRunner` holt fünfzehn der neunzehn Profilwerte über den historischen
`AiFactionProfile`-Konstruktor aus `Ms1Canonical` — nur so kommt
`waveStrengthPoints` überhaupt im Spiel an — und **überschreibt vier** mit
eigenen Literalen. Diese vier bewachte nichts: der Profil-Hash rechnet über
`Ms1Canonical`, der Endzustands-Pin spiegelt MatchRunners Literale, beide
Wächter sehen von je einer Seite an der Lücke vorbei. Aufgefallen ist es, weil
der Pin nach einer Änderung an `Ms1Canonical` **nicht** wanderte.
`AiProfileTests.MatchRunnerPassesTheSameFourNumbersTheShippedProfileCarries`
liest jetzt euren Quelltext und wird rot, wenn die vier abweichen — **gelesen,
nicht geändert.**

**2. `NoFloatInSimulationTests` bewacht `Scripts/AI*` nicht.** Es scannt `Core`
und `Simulation`. Der Determinismus-Vertrag nennt `Scripts/AI*` aber
ausdrücklich mit, und ein `float` dort käme heute lautlos durch die CI. Der neue
Code ist float-frei; wir haben nur unseren Kommentar korrigiert, der das
Gegenteil behauptete. **Die Lücke selbst haben wir nicht geschlossen**, weil der
Test zweimal existiert und die EditMode-Kopie unter `Assets/Tests/` nicht
unserem Scope zugeteilt ist. Sollen wir `"AI"` und `"AI.Data"` in beiden Kopien
zu `ScannedRoots` ergänzen?

</details>

<details>
<summary><b>Zwei Fehler, die unterwegs gefunden und behoben wurden</b></summary>

**Der „Boden von einer Einheit" war eine zweite Regel.** Die erste Fassung hob
den Schwellwert auf mindestens eine vollgesunde produzierte Einheit an. Damit
wartete ein einzelner **verwundeter** Nachzügler auf eine Einheit, die nie gebaut
werden konnte — die Blockade aus `f13f4d5`, eine Nummer kleiner. Das Labor zeigte
es sofort: die kanonische Partie lief **1.650 Ticks länger**. Ohne den Boden ist
die Aus-Stellung wieder bitgenau.

**Die Arithmetik war nicht prüfbar.** In einer Mutationsprobe blieb die ganze
Suite grün, als der Negativ-Clamp gelöscht **und** als `waveStrengthPoints`
komplett ignoriert wurde — die ausgelieferte Obergrenze erreicht die
unterscheidenden Zustände schlicht nie. Deshalb liegt die Schwelle jetzt als
reine Funktion in `WaveStrengthGate` mit eigener Fixture, geprüft an Zuständen,
die eine Partie nicht auf Bestellung erzeugt: mehr Einheiten am Leben als die
Kappe erlaubt, eine gerade zerstörte Kaserne, eine Schwelle die statt der Decke
bindet. Beide Mutationen fallen jetzt.

</details>

<details>
<summary><b>Was sich ändert, und was ausdrücklich nicht drin ist</b></summary>

| Datei | Was |
|---|---|
| `Scripts/AI/CombatStrength.cs` **(neu)** | die Formel — statische Klasse, kein System, kein Zustand |
| `Scripts/AI/WaveStrengthGate.cs` **(neu)** | die Schwellwert-Arithmetik als reine Funktion |
| `Scripts/AI/SkirmishAiSystem.cs` | zweiter Zweig in `ResolveArmyPosture`; der Zählpfad bleibt unberührt daneben |
| `Scripts/AI/AiFactionProfile.cs` | reicht das neue Feld durch — Signatur unverändert |
| `Scripts/AI.Data/AiProfile.cs` | Feld `WaveStrengthPoints` samt `Equals`/`GetHashCode` |
| `Scripts/AI.Data/AiProfiles.cs` | `Ms1Canonical: 1200`, `LegacyDefaults: 0` |
| `Scripts/AI.Data/AiBehaviorId.cs` | `Revision` 5 → **6**, neues Feld am Ende des Profil-Hashes |
| `tools/Nova.SimRunner.Tests/` | zwei neue Fixtures, drei erweitert |
| `CHANGELOG.md` | ein Eintrag unter `[Unreleased]` |

**Kein neues System, keine neue Tick-Position.** Alles liegt in
`SkirmishAiSystem`, das zwischen Combat und Victory bereits eingeordnet ist.
`MatchRunner` nicht angefasst, `ICommandTransport` und
`ICommandSubmissionReadiness` benutzt und nicht geändert. Ganzzahlig durchgehend,
kein `System.Random`, keine Wanduhr, keine Abhängigkeit von Iterationsreihenfolge.

**Warum die Formel in `AI/` liegt und nicht in `AI.Data/`:** `Nova.AI.Data`
referenziert nur `Nova.Core` — bewusst nicht `Nova.Simulation`, damit ein Profil
keine `UnitRole` nennen und keine zweite Definitionstabelle werden kann.
`AiProfile.SchemaVersion` bleibt **1**: ein Feld anhängen ist keine
Bedeutungsänderung.

Nicht drin: Armeeobergrenze (siehe Rückfrage), Nachschub-Doktrin,
Fahrzeugfabrik, Schwierigkeitsgrade, und ein Panzerungs- oder Reichweitenterm im
Kampfwert — der wird erst mit gemischten Armeen fällig, also mit den Fahrzeugen.

</details>
