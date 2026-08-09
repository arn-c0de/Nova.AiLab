# PR — Die Welle kann in Kampfstärke messen statt in Köpfen

> **Ziel-Repo:** Pull Request vom **FORK** `arn-c0de/Project_Nova` nach
> **HAUPTREPO** `VibecodingGermany/HashKrieg` (`main`) ·
> **Branch:** `feat/ai-strength-wave-gate` · **Basis:** `upstream/main` (`f13f4d5`)
> · **Bezeichner:** `r5.779A1B5B` → **`r6.E34435F9`**
>
> **Gebaut und gemessen.** Tests 638/638 grün, Determinismus Exit 0.
> Der Abschnitt „Im laufenden Spiel gesehen" ist leer und bleibt es, bis ein
> Mensch gespielt hat.
>
> Journal: [`../reports/behavior-log.md`](../reports/behavior-log.md) V007 ·
> Plan: [`../KAMPFSTAERKE.md`](../KAMPFSTAERKE.md) §3 und §5.

## Was dieser PR tut — und was ausdrücklich nicht

Er gibt der Welle eine **Masseinheit**. Sie marschierte auf eine *Anzahl*
gesammelter Einheiten, und eine Anzahl weiss nicht, was eine Einheit wert ist.

**Mit der ausgelieferten Armeeobergrenze von 12 ändert er noch kein Verhalten,
und ein Test nagelt das fest.** Die kanonische Partie endet Byte für Byte wie
bisher. Das ist keine Schwäche des PRs, das ist sein Zweck: **Wellengrösse und
Produktionsobergrenze hängen danach nicht mehr an derselben Zahl** — und erst
dann darf man an dieser Zahl drehen. Wer es umgekehrt macht, macht die Legion
messbar schlechter (Zahlen unten).

Die Obergrenze zu drehen ist allerdings **nicht unsere Zeile**: sie steht als
Literal in `MatchRunner`. Der Vorschlag samt Messreihe steht weiter unten unter
„Rückfrage".

## Warum — zwölf ist nicht zwölf

```
S(u) = AttackDamage(u) × CurrentHealth(u) / AttackCooldownTicks(u)
```

Schaden mal Zähigkeit je Feuerintervall. Bei voller Gesundheit:

| Rolle | Allianz | Legion |
|---|---:|---:|
| BasicInfantry | **100** | **44** |
| ScoutVehicle | 264 | 180 |
| LightTank | 962 | 672 |
| AntiArmorInfantry *(T2)* | 200 | 144 |
| BattleTank *(T2)* | 2.640 | 2.500 |
| Artillery *(T2)* | 550 | 274 |

Zwölf Allianz-Schützen wiegen **1.200** Punkte, zwölf Legions-Rekruten **528**.
Dieselbe Regel, dasselbe Wort „volle Welle" — und die Legion marschiert mit
**44 %** der Angriffsstärke los. In der Referenzpartie steht das in der
Verlustspalte: 51 gegen 23.

Ganzzahlig, **eine** Division, eine festgeschriebene Abschneidung (LightTank ist
962, nicht 963). Unbewaffnet ist `AttackDamage == 0` und ergibt 0 — Builder,
Harvester und acht der neun Gebäudearten fallen ohne Sonderfall heraus. Die
Werte kommen aus `WeaponProfiles`; die Definitionstabelle wird **gelesen, nicht
geändert** (`0x6326FA3E56CFF5A3` unverändert).

## Die Regel

```
WaveReady  =  S_sammelnd >= schwelle

erreichbar = max(0, TargetArmySize − committed − sammelnd) × S(BasicInfantry, voll)
schwelle   = min(WaveStrengthPoints, S_sammelnd + erreichbar)
```

`S_sammelnd` summiert `S(u)` über die Kampfeinheiten **innerhalb** des Rings ums
eigene HQ — dieselbe Menge, die `IsCommittedToTheWave` schon abgrenzt, nur
summiert statt gezählt.

**Die r5-Regel wandert mit, in Punkten statt in Köpfen.** Ohne sie kehrt die
Blockade zurück, die `f13f4d5` beseitigt hat: Überlebende früherer Wellen
belegen die Obergrenze, also kann die Produktion die Schwelle nie mehr liefern.
Der Deckel „was der Ring noch **erreichen** kann" sagt dasselbe wie r5s
`reachable = TargetArmySize − committed`, nur in der neuen Einheit.

**Es gibt keinen Boden darunter**, und die erste Fassung hatte einen. Siehe
„Ein Fund unterwegs".

### Warum das bei Obergrenze 12 nichts tut

Arithmetik, nicht Nachlässigkeit: der Deckel bindet zuerst. Zwölf
Allianz-Schützen **sind** 1.200 Punkte und eine dreizehnte Einheit lässt die
Kappe nicht zu; bei der Legion liegt der Deckel mit 528 dauerhaft unter der
Schwelle. Das Tor kann erst wirken, wenn die Obergrenze mehr Stärke zulässt, als
es verlangt.

### Die Aus-Stellung

`waveStrengthPoints: 0` ⇒ der Zählpfad von `f13f4d5`, unverändert, **Bit für
Bit**. Ohne sie ist die Regel im Selbstspiel nicht messbar (M001): eine
Coderegel erreicht beide Seiten zugleich.

## Gemessen

Labor, einseitig, dasselbe Binary, **ein** Profilwert Unterschied, je Zeile ist
**ein** Sitz umgestellt und der andere bleibt heutig. Heutige Auslieferung auf
beiden Sitzen: Tick 5.773, Verluste 23 / 51, Austausch 221 / 45, grösste Armee
12 / 12.

| Sitz | Kandidat | Tick | Sieger | eig. Verluste | eig. Austausch | grösste eig. Armee |
|---|---|---:|---|---:|---:|---:|
| **Legion** | *(heute)* | 5.773 | Allianz | 51 | 45 | 12 |
| **Legion** | Obergrenze 24 **allein** | 5.921 | Allianz | **64** | **34** | 16 |
| **Legion** | Obergrenze 20 + Tor | 5.599 | **Legion** | 42 | 64 | 20 |
| **Legion** | Obergrenze 24 + Tor | 17.350 | **Legion** | 178 | 69 | 24 |
| **Legion** | Obergrenze 36 + Tor | 7.747 | **Legion** | 63 | **90** | **35** |
| Allianz | *(heute)* | 5.773 | Allianz | 23 | 221 | 12 |
| Allianz | Obergrenze 24 **allein** | 4.154 | Allianz | 6 | 633 | 24 |
| Allianz | Obergrenze 24 + Tor | 3.914 | Allianz | 12 | 225 | 23 |
| Allianz | Obergrenze 36 + Tor | 3.914 | Allianz | 12 | 225 | 23 |

Die tragende Zeile ist die zweite: **die Obergrenze allein macht die Legion
schlechter** (Verluste 51 → 64, Austausch 45 → 34). Mehr Bauplatz nützt nichts,
solange die Welle bei zwölf Köpfen losläuft, egal was zwölf Köpfe wiegen. Mit
dem Tor kippt derselbe Sitz — und die grösste Legionsarmee wächst von 12 auf
bis zu 35, sie sammelt also während des Gefechts weiter.

**`intentsRejected` bleibt 0** über alle 23 Laborkandidaten.

### Was schlechter wird

- **APM steigt deutlich**: Legionssitz 13 → 43 bei Obergrenze 36, Allianzsitz
  29 → 47. Das ist die Kennzahl, an der `DefendBase` gescheitert ist (+23 % und
  schlechteres Spiel, Journal V002). Ein Teil ist erklärbar — eine dreimal so
  grosse Armee wird häufiger gruppiert —, aber erklärbar ist nicht geprüft.
- **Der Allianz-Sitz gewinnt durch das Tor nichts.** Obergrenze 24 *ohne* Tor
  ist dort besser (6 Verluste gegen 12, Austausch 633 gegen 225). Bei Obergrenze
  12 kostet es sie nichts, weil dort nichts passiert.
- **Obergrenze 24 auf dem Legionssitz dauert 17.350 Ticks** und kostet 178
  eigene Einheiten. Gewonnen ist gewonnen, aber das ist Zermürbung, kein
  Angriff — dieselbe Form, die `retreat-75` disqualifiziert hat.

### Eine Annahme aus dem Plan, die der Lauf gekippt hat

> Der Plan schrieb die Klippe zwischen Obergrenze 18 und 20 dem an die Kappe
> gekoppelten Wellentor zu. **Das ist falsch.** Mit dem Tor bleibt sie stehen:
> `army-18` braucht auf dem Legionssitz 17.908 Ticks und 203 eigene Verluste.
> Die Kopplung war *eine* Ursache, nicht *die*. Was 18 von 20 unterscheidet,
> ist unerklärt, und dieser PR behauptet nicht, es erklärt zu haben.

## Ein Fund unterwegs

Die erste Fassung hob den Schwellwert auf mindestens **eine vollgesunde
produzierte Einheit** an — als Absicherung gedacht, tatsächlich eine zweite
Regel. Damit wartete ein einzelner **verwundeter** Nachzügler (weniger wert als
ein frischer Rekrut, während die Kappe von den Kämpfenden draussen belegt ist)
auf eine Einheit, die nie gebaut werden konnte: die Blockade aus `f13f4d5`, eine
Nummer kleiner. Das Labor hat sie sofort gezeigt — die kanonische Partie lief
**1.650 Ticks länger** (7.423 statt 5.773). Ohne den Boden ist die Aus-Stellung
wieder bitgenau. Der Boden ist raus, der Grund steht als Kommentar im Code.

## Was sich ändert

| Datei | Was |
|---|---|
| `Assets/_Project/Scripts/AI/CombatStrength.cs` **(neu)** | die Formel. Statische Klasse, kein System, kein Zustand |
| `Assets/_Project/Scripts/AI/SkirmishAiSystem.cs` | zweiter Zweig in `ResolveArmyPosture` plus `StrengthThreshold`; der Zählpfad bleibt unberührt daneben stehen |
| `Assets/_Project/Scripts/AI/AiFactionProfile.cs` | reicht das neue Feld durch — Signatur unverändert, `MatchRunner` bleibt unangetastet |
| `Assets/_Project/Scripts/AI.Data/AiProfile.cs` | Feld `WaveStrengthPoints`, Konstruktor, `Equals`, `GetHashCode` |
| `Assets/_Project/Scripts/AI.Data/AiProfiles.cs` | `Ms1Canonical: 1200`, `LegacyDefaults: 0` |
| `Assets/_Project/Scripts/AI.Data/AiBehaviorId.cs` | `Revision` 5 → **6**, neues Feld am Ende des Profil-Hashes |
| `tools/Nova.SimRunner.Tests/CombatStrengthTests.cs` **(neu)** | die Formel gegen die Tabelle |
| `tools/Nova.SimRunner.Tests/SkirmishAiTests.cs` | Wellentest über Positionen, Bezeichner-Pin nachgezogen |
| `tools/Nova.SimRunner.Tests/AiProfileTests.cs` | das neue Feld im Tuning-Pfad |
| `CHANGELOG.md` | ein Eintrag unter `[Unreleased]` |

**Warum die Formel in `AI/` liegt und nicht in `AI.Data/`:** `Nova.AI.Data`
referenziert nur `Nova.Core` — bewusst nicht `Nova.Simulation`, damit ein Profil
keine `UnitRole` nennen und keine zweite Definitionstabelle werden kann. Die
Formel braucht `UnitRole`, `FactionId` und `WeaponProfiles`. Verhalten in C#,
Zahlen in `AI.Data`.

`AiProfile.SchemaVersion` bleibt **1**: ein Feld anhängen ist keine
Bedeutungsänderung.

## Kein neues System, keine neue Tick-Position

Alles liegt in `SkirmishAiSystem`, das zwischen Combat und Victory bereits
eingeordnet ist. `MatchRunner` nicht angefasst, Tick-Reihenfolge nicht,
`ICommandTransport` und `ICommandSubmissionReadiness` werden benutzt und nicht
geändert. Ganzzahlig durchgehend, kein `System.Random`, keine Wanduhr, keine
Abhängigkeit von Iterationsreihenfolge.

## Tests — 638/638 grün (vorher 619)

1. **Formel gegen die Tabelle**, zwölf Werte, beide Fraktionen, inklusive der
   Abschneidung.
2. **Unbewaffnet ⇒ 0** über *jede* Rolle beider Fraktionen, nicht über eine
   Liste von Namen — die Regel folgt der Definitionstabelle.
3. **Verwundet wiegt weniger**, und tot wiegt 0 statt negativ (ein negativer
   Summand liesse eine sammelnde Welle sich wieder auflösen).
4. **Wellentor an Positionen, nicht an Intents** — Negativkontrolle: zwei Läufe,
   die sich in **einem** Profilwert unterscheiden, Armeeobergrenze 24. Auf dem
   Zählpfad marschiert die Legion mit zwölf Rekruten, auf dem Punktpfad mit
   mehr. Auf `r5` sind beide Zahlen gleich und der Test fällt.
5. **Der Bezeichner-Pin** hält Entscheidungstick 2.548 und Endzustand
   `0x14472B2B943ED2BB` — **unverändert**. Nur `AiBehaviorId.Value` wird
   nachgezogen, und der Kommentar daneben sagt, warum das hier richtig ist.
6. **Ein Test nagelt fest, dass das Tor schlafend ausgeliefert wird**:
   `TargetArmySize × 44 <= WaveStrengthPoints`, also kann der Deckel nie über
   die Schwelle kommen. Er geht rot, sobald jemand die Obergrenze anhebt — und
   das ist der Moment, in dem er gelesen werden soll. Die Fehlermeldung verweist
   auf den r6-Eintrag in `AiBehaviorId`.

**Keine der vier Determinismus-Baselines ist angefasst.** Sie fahren kein
KI-System und bleiben grün.

## Im laufenden Spiel gesehen

**Nichts.** Alles oben ist im Labor gemessen — Diagnose, kein Nachweis. Der
Abschnitt bleibt leer und als leer erkennbar, bis ein Mensch eine Partie
gefahren hat; ausgefüllt wird er von einem Menschen, nicht von einem Agenten.

Bei der ausgelieferten Obergrenze 12 gibt es hier auch nichts zu sehen — die
Partie läuft nachweislich identisch. Zu sehen ist erst der PR, der die
Obergrenze anfasst.

## Rückfrage: die Armeeobergrenze liegt in eurer Datei

Damit das Tor überhaupt greifen kann, muss die Armeeobergrenze über **28**
liegen — 1.200 Punkte sind 28 Legions-Rekruten zu je 44. Sie steht aber nicht in
`AiProfiles`, sondern als Literal in
[`MatchRunner.cs:252`](../../Project_Nova/Assets/_Project/Scripts/Gameplay/Match/MatchRunner.cs),
und `Scripts/Gameplay/Match/` ist Netzstrang. **Wir fassen das nicht an** und
schlagen es stattdessen vor:

```csharp
new AiFactionProfile(_config.FactionPerSlot[aiSlot].ToString(),
    targetPowerMargin: 0,
    targetArmySize: 30,   // war 12
    attackSquadThreshold: 6,
    targetHarvesterCount: 2),
```

**Warum 30 und nicht die beste Zahl der Kurve.** 1.200 Punkte sind 28 Rekruten
(27 sind 1.188, einer zu wenig). Unter Obergrenze 28 ist die Schwelle
unerreichbar, der Deckel bindet, und die Welle marschiert wieder auf Kopfzahl —
nur auf eine grössere. Bei 28 exakt bleibt kein Kopf übrig, um während des
Sammelns weiterzubauen. **28 + 2 = 30** räumt beides frei. Das ist aus dem
Schwellwert abgeleitet, nicht aus der Kurve gewählt — und das ist wichtig, weil
die Kurve trügt: Obergrenze **20 gewinnt, 19 und 21 verlieren**. Wer dort das
Maximum nimmt, trifft eine Einzelpartie.

| Obergrenze | Legion: Tick | Sieger | eig. Verl. | Austausch | APM |
|---:|---:|---|---:|---:|---:|
| **12** *(heute)* | 5.773 | Allianz | 51 | 45 | 13 |
| 20 | 5.599 | **Legion** | 42 | 64 | 34 |
| 24 | 17.350 | **Legion** | 178 | 69 | 35 |
| 28 | 15.735 | **Legion** | 154 | 64 | 33 |
| **30** | **5.005** | **Legion** | **23** | **139** | **29** |
| 32 | 5.253 | **Legion** | 31 | 100 | 34 |
| 36 | 7.747 | **Legion** | 63 | 90 | 43 |

Bei 30 entscheidet die Legion **schneller als die heutige KI** (5.005 gegen
5.773) mit 23 statt 51 eigenen Verlusten. Der Allianzsitz: 3.914 statt 5.773
Ticks, 12 statt 23 Verluste, Austausch 225 gegen 221 — und APM 46 gegen 29, das
ist der Preis. Nach oben wird nur die APM teurer; die Allianz sättigt bei 23
Einheiten, ab Obergrenze 40 ist die Kappe reine Intent-Erzeugung.

**Ein Nebenbefund, der euch gehört:** `MatchRunner` liest `AiProfiles` nicht,
sondern trägt vier eigene Literale. Heute stimmen sie mit `Ms1Canonical`
überein, und nichts erzwingt das — sie können still auseinanderlaufen. Aufgefallen
ist es hier, weil der gepinnte Endzustand nach einer Änderung an `Ms1Canonical`
**nicht** wanderte: `SkirmishAiTests.BuildMatch` spiegelt denselben Aufruf.

## Was ausdrücklich **nicht** drin ist

| Nicht drin | Warum |
|---|---|
| **Armeeobergrenze anheben** | Siehe oben — sie liegt in `MatchRunner`, also in fremdem Terrain. Der Vorschlag steht, die Änderung nicht |
| **Nachschub-Doktrin** (nachschicken solange die Welle intakt ist, sammeln wenn sie gebrochen ist) | Zweite Verhaltensregel, eigener PR — sie braucht diese Schwelle als Bezugsgrösse. KAMPFSTAERKE.md §6 |
| **Fahrzeugfabrik, Fahrzeuge, Kaufregel** | KAMPFSTAERKE.md §7/§8 |
| **Schwierigkeitsgrade** | Reine Zahlen, kommen zuletzt |
| **Panzerungs- oder Reichweitenterm im Kampfwert** | Bewusste Auslassung, KAMPFSTAERKE.md §3.3. Auslöser wären gemischte Armeen — also die Fahrzeuge, nicht dieser PR |

## Checkliste

- [x] Branch frisch von `upstream/main`, nicht aus `lab/` gecherrypickt
- [x] `dotnet test tools/Nova.SimRunner.Tests/Nova.SimRunner.Tests.csproj -c Release` — 638/638
- [x] Determinismus zuerst: `match --repeat 2 --hash-every 100`, **Exit 0**
- [x] Aus-Stellung bitgenau gegen die Referenz geprüft (Tick 5.773, `0x2B34B4E194257940`)
- [x] `AiBehaviorId.Revision` auf 6, neues Feld im Profil-Hash
- [x] Zeile unter `[Unreleased]` in `CHANGELOG.md`
- [x] Journaleintrag V007 **mit** Abschnitt „Schlechter" und „Widerlegt"
- [x] Keine der vier Determinismus-Baselines im selben PR geändert
- [x] Abschnitt „Im laufenden Spiel gesehen" leer gelassen
- [ ] Vor dem Push gefragt und das Ziel benannt: **FORK** `arn-c0de/Project_Nova`
