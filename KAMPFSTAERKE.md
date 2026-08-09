# Kampfstärke — Nachschub, Ausbau und die eine Zahl darunter

**Notiert am:** 2026-08-09 · **Status:** Plan, **nichts davon gebaut** ·
**Ausgangsstand:** KI-Verhalten `r4.779A1B5B`, Commit `97a9e5b1`,
Definitionstabelle `0x6326FA3E56CFF5A3` ·
**Messgrundlage:** [`reports/latest.md`](reports/latest.md) ·
**Vorher lesen:** [`reports/behavior-log.md`](reports/behavior-log.md),
[`NEXT-STEPS.md`](NEXT-STEPS.md) §0 und §7, [`AGENTS.md`](AGENTS.md) §3–§4

Dieses Dokument plant drei Wünsche, die wie drei Themen aussehen und eines sind:

1. **Nachschub.** Wenn die Welle losläuft und sich hinten neue Einheiten sammeln,
   sollen die direkt hinterhergeschickt werden, damit die Welle stärker wird.
2. **Abbruch.** Wenn die Welle im Gefecht steht und ihre Angriffsstärke schnell
   fällt — fast alle tot oder auf dem Rückzug —, wird **nichts** mehr
   hinterhergeschickt. Erst wenn sich am Sammelpunkt wieder genug Stärke
   gesammelt hat, geht die nächste Welle los.
3. **Ausgeben.** Die KI soll ihre Punkte ausgeben: weiterbauen bis zur
   Fahrzeugfabrik, Fahrzeuge sammeln, damit angreifen.

Alle drei fragen dieselbe Frage — **wieviel ist das wert, was da steht?** — und
keine Stelle im heutigen Code kann sie beantworten. Die KI zählt Einheiten. Ein
Rekrut und ein Koloss sind für sie dasselbe. Deshalb steht unter den drei
Punkten **ein** System, und deshalb lässt sich danach ein Schwierigkeitsgrad als
Zahlensatz einstellen statt als zweite KI.

---

## 1 · Der Ausgangsstand, in Zahlen

Aus dem letzten vollständigen Lauf ([`reports/latest.md`](reports/latest.md),
Partie entschieden bei Tick 9.164):

| Kennzahl | Slot 0 · Allianz | Slot 1 · Legion |
|---|---:|---:|
| **Credits am Ende** | **28.850** | **29.400** |
| Armeegrösse | 12 | 1 |
| Verluste, kumuliert | 39 | 90 |
| Gebäude | 4 | 3 |
| Harvester | 2 | 2 |

Drei Dinge stehen in dieser Tabelle:

- **Die Bank ist voll und wird nie geleert.** 28.850 AE sind 32 Lynx-Panzer oder
  240 Rifleman. Die Credits-Kurve steigt über die ganze Partie monoton — die KI
  hat kein Problem beim Verdienen, sondern beim Ausgeben.
- **Vier Gebäude** heisst HQ, Refinery, Barracks, Power. Danach hört die Bauliste
  auf (`SkirmishAiSystem.cs:299-314`): es gibt keinen Eintrag hinter der Kaserne.
- **90 zu 39 Verluste.** Die Legion verliert mehr als doppelt so viele Einheiten.
  Warum, sagt §3.4 — und es ist kein Balancing-Problem, sondern eine Folge davon,
  dass das Wellentor Einheiten *zählt*.

---

## 2 · Warum eine Zahl und nicht drei Regeln

Man könnte jede der drei Anforderungen einzeln bauen: Nachschub an einem
Zähler, Abbruch an einer Verlustquote, Fahrzeugbau an einer Credit-Schwelle. Das
wären drei Sonderfälle mit drei eigenen Profilwerten, die sich gegenseitig nicht
kennen — und beim vierten Wunsch käme der vierte dazu.

Die Alternative ist eine **gemeinsame Währung**: jede Einheit trägt eine
Punktzahl, und alle vier Entscheidungen — Welle voll, Welle gebrochen, Armee
gross genug, was kaufen — sind Vergleiche in derselben Einheit. Das ist die
Voraussetzung für den Schwierigkeitsgrad in §9: Ein Regler, der „schwer" heisst,
kann nicht an fünf unvergleichbaren Zählern gleichzeitig drehen.

Dasselbe Argument hat die Zielwahl schon einmal gewonnen: `ScoreTarget`
(`SkirmishAiSystem.cs:1236-1277`) ersetzt „HQ vor Gebäude vor Einheit" durch
einen ganzzahligen Score aus vier gewichteten Termen. Der Kampfwert ist
dieselbe Bauform, angewandt auf die eigene Armee statt auf das Ziel.

---

## 3 · Das Kampfstärke-System

### 3.1 Die Formel

```
S(u) = AttackDamage(u) × CurrentHealth(u) / AttackCooldownTicks(u)
```

Drei Werte, eine Ganzzahldivision, eine Abschneidung. Gelesen: **Schaden mal
Zähigkeit je Feuerintervall.**

- `AttackDamage` und `AttackCooldownTicks` kommen aus
  `WeaponProfiles.Get(faction, role)` — eine O(1)-Tabelle ohne Allokation, die
  die KI in `CollectVisibleThreatCells` und `ScoreTarget` bereits benutzt.
- `CurrentHealth` steht in `UnitState` und wird ohnehin je Entscheidung gelesen
  (`IsRetreating`, `ScoreTarget`).
- Unbewaffnet ist eindeutig `AttackDamage == 0` → **S = 0**. Builder, Harvester
  und acht der neun Gebäudearten fallen damit ohne Sonderfall heraus; das ist
  dieselbe Konvention wie `WeaponProfile.IsArmed`.

**Warum genau diese Form:**

- **Ganzzahlig.** Kein Float, keine Wurzel, kein Prozentzwischenschritt —
  `NoFloatInSimulationTests` prüft mit, und die Formel besteht sie ohne
  Ausnahme.
- **Ein Rifleman ist 100 Punkte.** Reiner Zufall der Zahlen (10 × 90 / 9), aber
  ein brauchbarer Massstab: Wellenstärken lassen sich in „so viele Rifleman"
  lesen, ohne umzurechnen.
- **Die Gesundheit steckt drin, nicht daneben.** Eine Welle, die zusammenbricht,
  verliert Punkte, *bevor* die erste Einheit stirbt. Genau das verlangt
  Anforderung 2 („Angriffsstärke fällt rapide"), und keine Zählregel kann es.
- **Eine einzige Abschneidung**, an einer festgeschriebenen Stelle. Die
  Reihenfolge `dmg × hp` vor der Division ist Teil der Definition und gehört in
  den Test, nicht in den Kommentar: `35 × 550 / 20 = 962`, nicht
  `35 / 20 × 550 = 550`.

### 3.2 Die Tabelle, die daraus folgt

Alle Werte bei voller Gesundheit, gerechnet aus
`Simulation/Definitions/SimDefinitions.cs`. Die letzte Spalte ist die
Kaufentscheidung aus §7: Punkte je 100 AE.

**Allianz**

| Rolle | Schaden | HP | Cooldown | **S** | Kosten AE | **S je 100 AE** | Tier |
|---|---:|---:|---:|---:|---:|---:|---:|
| BasicInfantry *Rifleman* | 10 | 90 | 9 | **100** | 120 | 83 | 1 |
| ScoutVehicle *Jackal* | 12 | 220 | 10 | **264** | 300 | 88 | 1 |
| LightTank *Lynx* | 35 | 550 | 20 | **962** | 600 | **160** | 1 |
| AntiArmorInfantry *Rocket Soldier* | 50 | 100 | 25 | **200** | 250 | 80 | 2 |
| BattleTank *Aegis* | 60 | 1100 | 25 | **2640** | 900 | **293** | 2 |
| Artillery *Longbow* | 110 | 350 | 70 | **550** | 1000 | 55 | 2 |

**Legion**

| Rolle | Schaden | HP | Cooldown | **S** | Kosten AE | **S je 100 AE** | Tier |
|---|---:|---:|---:|---:|---:|---:|---:|
| BasicInfantry *Rekrut* | 8 | 55 | 10 | **44** | 60 | 73 | 1 |
| ScoutVehicle *Hyäne* | 10 | 180 | 10 | **180** | 220 | 81 | 1 |
| LightTank *Räuber* | 28 | 480 | 20 | **672** | 450 | **149** | 1 |
| AntiArmorInfantry *Raketenschütze* | 40 | 90 | 25 | **144** | 200 | 72 | 2 |
| BattleTank *Koloss* | 50 | 1250 | 25 | **2500** | 700 | **357** | 2 |
| Artillery *Donnerkanone* | 60 | 320 | 70 | **274** | 800 | 34 | 2 |

### 3.3 Was die Formel bewusst nicht kennt

Eine Kennzahl, die alles einrechnet, ist wieder die Gesamtnote aus
Entscheidung 11. Drei Dinge fehlen absichtlich, und für jedes gibt es einen
Grund und einen Ausbauweg:

| Fehlt | Warum jetzt richtig | Wann es dazu muss |
|---|---|---|
| **Panzerung / Gegentabelle** | Der Kampfwert bewertet die eigene Armee, nicht ein Duell. Gegen wen sie kämpft, steht bei der Zielwahl (`ScoreTarget` rechnet `DamageMatrix.Resolve` bereits ein) | Sobald die KI die Armeezusammensetzung nach dem Gegner wählt — dann als zweite Funktion `S(u, gegen Panzerklasse)`, nicht als Änderung an dieser |
| **Reichweite** | Gemessen: Artillerie richtet ohne Aufklärung über 2.000 Ticks **null** Schaden an, und 100 der 576 Duelle bleiben kontaktlos, weil Waffenreichweite (20) über Sichtweite (10) liegt. Reichweite ist heute kein Wert, sondern eine Behauptung | Sobald Aufklärung existiert (NEXT-STEPS §6). Dass die Formel Artillerie **heute** als schlechtesten Kauf ausweist (55 bzw. 34 Punkte je 100 AE), ist keine Schwäche der Formel, sondern die Übereinstimmung mit einer Messung |
| **Anzahl / Konzentration** | Lanchester-Effekte (zwölf Einheiten sind mehr wert als zwölfmal eine) brauchen eine quadratische Form und damit grössere Zahlen ohne bessere Entscheidungen | Nur, wenn eine Messung zeigt, dass die Summe falsch entscheidet. Vorher nicht |

### 3.4 Der Befund, den die Tabelle sofort liefert

Das heutige Wellentor vergleicht **Einheitenzahlen** (`waveSize: 12`
gegen `gathered`, `SkirmishAiSystem.cs:628-634`). In Punkten heisst das:

| Volle Welle heute | Einheiten | Punkte |
|---|---:|---:|
| Allianz, 12 Rifleman | 12 | **1.200** |
| Legion, 12 Rekruten | 12 | **528** |

**Die Legion greift mit 44 % der Angriffsstärke an und hält sich für voll.**
Bei gleichem Einsatz gerechnet ist der Unterschied klein (12 Rifleman kosten
1.440 AE; 24 Rekruten kosten dasselbe und bringen 1.056 Punkte) — die Legion
ist nicht schwächer, sie ist **billiger und zahlreicher**, und der Zähler
verbietet ihr, das auszuspielen. Die 90 zu 39 Verluste aus §1 sind die Folge
davon, nicht der Waffenwerte.

Damit ist das Kampfstärke-System nicht nur eine Tuning-Vorbereitung, sondern
greift genau die Sache an, die in unserem Auftrag steht: **Legion-Waffen­identität.**
Und zwar ohne `Simulation/Definitions/` anzufassen — geteilte Vertragsfläche,
Absprache nötig, hier nicht berührt.

---

## 4 · Drei Aggregate, alle zustandslos

Die KI ist eine reine Funktion des committeten Zustands: keine Timer, kein
Gedächtnis, kein Sidecar (Inhaberentscheidung, gesperrt). Die drei Grössen,
auf denen alle Regeln unten stehen, werden je Entscheidung neu ausgerechnet.

Vorher zwei Mengen, beide schon vorhanden:

- **draussen** — `IsCommittedToTheWave(u)` ist wahr: die Einheit steht ausserhalb
  des Rings `StagingDistanceCells + StagingToleranceCells` um das **eigene HQ**
  (`SkirmishAiSystem.cs:797-805`). Am HQ gemessen, nicht am Ziel: das HQ läuft
  nicht weg.
- **sammelnd** — innerhalb des Rings.

Rückzügler zählen in **keine** von beiden. `IsRetreating`
(`SkirmishAiSystem.cs:710-729`) beantwortet das bereits je Einheit; die Antwort
muss nur nach vorn wandern (§10.1). Das erledigt die Hälfte „…oder flüchten"
aus Anforderung 2 ohne eine neue Regel.

| Grösse | Definition | Was sie beantwortet |
|---|---|---|
| **S_draussen** | Summe von `S(u)` über alle draussen stehenden, nicht flüchtenden Kampfeinheiten | Was die laufende Welle noch wert ist |
| **S_sammelnd** | dieselbe Summe über die sammelnden | Was am Sammelpunkt bereitsteht |
| **S_voll** | Profilwert `WaveStrengthPoints` | Was eine volle Welle wert sein soll |

**Die entscheidende Auslassung:** Es gibt **keine** „Stärke beim Losmarsch".
Sie wäre die naheliegende Bezugsgrösse für „fällt rapide" und ist nicht
speicherbar, ohne die Zustandslosigkeit aufzugeben. Der Ersatz ist `S_voll`:
verglichen wird nicht mit dem, was die Welle einmal war, sondern mit dem, was
eine volle Welle wert ist. Das ist eine **Niveauregel, keine Ratenregel** — sie
sieht nicht, wie schnell etwas fällt, nur wie tief es steht. Für die drei
Wünsche reicht das (siehe §6.3); wo es nicht reicht, steht es in §12.

---

## 5 · Regel 1 — das Wellentor über Stärke

**Heute:** die Welle marschiert, wenn `gathered >= waveSize` Einheiten im Ring
stehen.

**Neu:** die Welle marschiert, wenn `S_sammelnd >= S_voll`.

```
WaveReady  =  S_sammelnd >= WaveStrengthPoints
```

- **Aus-Stellung:** `WaveStrengthPoints: 0` → die Zählregel von heute, Bit für
  Bit dieselbe Entscheidung. Ohne diese Stellung ist die Regel im Selbstspiel
  nicht messbar (Methodenbefund M001).
- **Startwert zum Messen:** `1200` — genau die heutige volle Allianz-Welle. Die
  Allianz verhält sich damit nahezu wie bisher, und die Änderung wird auf der
  Legion-Seite sichtbar, wo sie hingehört.
- Die Kappung gegen die Armeeobergrenze aus `EffectiveWaveSize()`
  (`SkirmishAiSystem.cs:648-652`) bleibt sinngemäss nötig: ein Stärkeziel über
  dem, was die Armeeobergrenze je liefern kann, lässt die Armee bis zum
  Zeitlimit am Sammelpunkt stehen. Neue Fassung: gegen das **Armee-Stärkeziel**
  aus §7 kappen, nicht gegen eine Einheitenzahl.

**Was zu prüfen ist, bevor das als gut gilt:** Die Legion sammelt bei 1200
Punkten rund **27 Rekruten** statt 12. Drei Folgen, alle messbar:

1. Formationsverteilung am Sammelpunkt — 27 Einheiten breiten sich weiter aus
   als 12. Der **Ring** (Distanz + Toleranz = 16 Zellen) zählt, nicht die
   Toleranz allein; genau daran ist die erste Wellenfassung gescheitert
   (Journal V004). Die Regel trägt das, aber es ist die erste Zahl, die man
   nachsieht.
2. Produktionsdauer — 27 Rekruten aus **einer** Kaserne bei 5 Warteschlangen­plätzen
   (`ProductionSystem.MaxQueueEntries`) dauern. Das ist das Argument für die
   zweite Kaserne in §6, nicht gegen die Regel.
3. Befehlsgrösse — 100 Entity-IDs je Kommando (`CommandLimits.MaxEntityIdsPerCommand`),
   die Chunk-Schleife in `SubmitEntityList` ist vorhanden. Bei 27 kein Thema,
   ab 100 gemessen prüfen.

---

## 6 · Regel 2 — die Nachschub-Doktrin

Das ist der Kern der Anfrage. Drei Lagen, ein Vergleich.

```
S_gebrochen  =  WaveStrengthPoints × ReinforceMinStrengthPercent / 100

(a) S_draussen == 0                     → Erstschlag:  warten bis S_sammelnd >= S_voll   (Regel 1)
(b) S_draussen >= S_gebrochen           → Nachschub:   jede sammelnde Einheit marschiert SOFORT los
(c) 0 < S_draussen < S_gebrochen        → Welle gebrochen: niemand marschiert,
                                          bis Regel 1 wieder greift (S_sammelnd >= S_voll)
```

### 6.1 Was das im Spiel heisst

- **(b) ist der Wunsch „direkt hinterhersenden".** Solange die Welle draussen
  noch ein ernstzunehmender Verband ist, ist eine einzelne nachlaufende Einheit
  keine Verschwendung, sondern Verstärkung — sie trifft auf einen Kampf, der
  noch läuft. Das ist wörtlich die Fortsetzung, die der Spieler selbst benannt
  hat (Journal B002): *„Automatischer Wechsel zu Tröpfeln, wenn die Armee
  bereits auf dem Angriffsweg ist, als Unterstützung."*
- **(c) ist der Wunsch „nichts mehr hinterhersenden".** Wenn draussen nur noch
  Reste stehen, ist eine nachlaufende Einheit genau das Förderband, gegen das
  die Wellenregel gebaut wurde — sie stirbt einzeln. Also sammeln.
- **(a) ist der heutige Zustand** und bleibt unverändert.

Der Übergang von (b) nach (c) ist der Punkt, an dem sich die Regel bezahlt
macht: er passiert **während** des Gefechts, ohne dass jemand mitzählt, allein
weil die Summe der Lebenspunkte draussen fällt.

### 6.2 Aus-Stellung und Startwerte

- `ReinforceMinStrengthPercent: 0` → Fall (b) tritt nie ein, alles verhält sich
  wie heute. Das ist die Aus-Stellung.
- Startwert zum Messen: **50**. Gelesen: *„Nachschub läuft nach, solange die
  Welle draussen noch mindestens eine halbe volle Welle wert ist."*
- Ein hoher Wert (80) macht die Regel fast zur heutigen; ein niedriger (20)
  schickt Einheiten in einen verlorenen Kampf nach. Die Kurve wird gemessen,
  nicht geraten — und die Erfahrung aus V006 mahnt: `waveSize 6` war schlechter
  als beide Ränder. **Ein mittlerer Wert ist nicht automatisch ein
  Kompromiss.**

### 6.3 Die ehrliche Einschränkung: „rapide" ist nicht messbar, „tief" schon

Die Anfrage sagt *„Angriffsstärke fällt rapide"*. Eine Rate braucht zwei
Zeitpunkte und damit Gedächtnis. Was diese Regel stattdessen prüft, ist ein
Niveau. Praktisch fällt beides zusammen — eine Welle, die zusammenbricht,
unterschreitet das Niveau, und zwar innerhalb weniger Kadenzen —, aber es ist
nicht dasselbe, und der PR-Text sagt das statt es zu verschweigen.

Falls die Niveauregel gemessen zu träge ist, gibt es einen **zweiten
zustandslosen Näherungswert**, der die Abnutzung statt der Verluste sieht:

```
S_draussen_nominal = Σ  AttackDamage(u) × MaxHealth(u) / AttackCooldownTicks(u)
Abnutzung          = 100 − (S_draussen × 100 / S_draussen_nominal)
```

Das ist *„wie angeschlagen sind die Überlebenden"* und braucht ebenfalls kein
Gedächtnis. Es ist ausdrücklich **nicht** Teil des ersten Baus: eine
Verhaltensänderung je PR, und die beiden Kriterien sind zwei.

### 6.4 Die Falle, die diese Regel mitbringt

Am Schwellwert kann eine Einheit zwischen (b) und (c) kippen: eine Kadenz
Marschbefehl zum Ziel, die nächste Rückbefehl zum Sammelpunkt. Das ist **exakt
der Fehlermodus, an dem `DefendBase` gescheitert ist** (Journal V002: +23 %
Intents ohne besseres Spiel), und die erste Zahl, die nach dem Bau angesehen
wird, ist deshalb nicht die Siegquote, sondern **Intents je 1.000 Ticks** bzw.
APM (heute 18).

Dämpfung, falls die Zahl steigt — in dieser Reihenfolge zu erwägen, jede einzeln
messbar:

1. **Einmal losgeschickt, nicht zurückbeordert.** Eine Einheit, deren stehender
   Befehl bereits auf die Marschzelle zeigt (`TargetGridPos == posture.MoveCell`),
   gilt als committed, auch wenn sie den Ring noch nicht verlassen hat. Der
   stehende Befehl ist das einzige zulässige Gedächtnis und übersteht
   Save/Restore, weil er Teil der Welt ist.
2. **Hysterese über zwei Prozentwerte** (Eintritt (c) bei 50, Rückkehr zu (b)
   erst bei 70). Kostet einen Profilwert.
3. Nichts tun, wenn die Intent-Zahl flach bleibt. Der wahrscheinlichste Fall:
   `S_draussen` **fällt monoton** — MS-1-Einheiten heilen nie —, und steigen kann
   die Summe nur dadurch, dass Nachschub den Ring verlässt. Das ist kein
   Flattern, das ist die Regel bei der Arbeit.

---

## 7 · Regel 3 — bauen bis zur Fahrzeugfabrik

### 7.1 Die Bauliste

Heute ist die Bauliste ein verschachtelter Ternär­ausdruck mit zwei Einträgen
(`SkirmishAiSystem.cs:301-303`). Neu ist sie eine geordnete Liste, und
`BuildOutDepth` sagt, wie weit sie gegangen wird:

| # | Rolle | Voraussetzung im Code | Kosten AE (All./Leg.) | Strom (All./Leg.) | Was sie freischaltet |
|---:|---|---|---|---|---|
| 1 | Refinery | keine (D-077) | 700 / 550 | −20 / −15 | Harvester, Wirtschaft |
| 2 | Barracks | keine | 500 / 400 | −15 / −10 | Infanterie |
| 3 | **VehicleFactory** | **fertige Refinery** ✓ | **900 / 700** | **−25 / −20** | ScoutVehicle, **LightTank** |
| 4 | ResearchLab | fertige Barracks | 1000 / 800 | −30 / −25 | T2: BattleTank, Artillery, AntiArmorInfantry |
| 5 | 2. Barracks / 2. VehicleFactory | wie oben | dito | dito | Durchsatz, keine neue Technik |

- **Aus-Stellung:** `BuildOutDepth: 2` → heutige Bauliste, Bit für Bit.
- **Erster Schritt:** `3`. Das ist der Wunsch „bis Fahrzeugfabrik" wörtlich, und
  es ist der grösste Sprung je AE: Der Lynx bringt **160** Punkte je 100 AE
  gegen **83** des Rifleman, der Räuber **149** gegen **73**. Fahrzeuge sind
  rund **doppelt so viel Kampfwert je Aetherium** wie Infanterie — mit
  Tier-1-Technik, ohne Forschungslabor.
- Der Stromvorlauf trägt: HQ liefert 30, ein Kraftwerk 100 (Allianz) bzw. 80
  (Legion). Refinery + Barracks + VehicleFactory + ResearchLab ziehen 90
  (Allianz) bzw. 70 (Legion) — **ein** Kraftwerk deckt die ganze Liste.

### 7.2 Der Fehler, der vorher weg muss

```csharp
// SkirmishAiSystem.cs:245-247
case UnitRole.Power:
    powerCompleted = true;
    break;
```

`powerCompleted` ist ein **Bool**. Die Vorrangregel für das Kraftwerk
(`Zeile 307-311`) greift nur, solange **kein** Kraftwerk steht — die KI baut
also **nie ein zweites**. Heute egal, weil die Liste nach der Kaserne endet; ab
Eintrag 5 oder einer zweiten Refinery ist es eine stille Sperre, die sich als
„die KI baut nicht weiter" zeigt und nicht als Fehler.

Ersatz: ein Zähler und der Vergleich, den `ConstructionSystem` selbst anlegt —
`ConstructionSystem.cs:343` lehnt eine Platzierung ab, wenn
`PowerProvided − PowerRequired < def.PowerRequired`. Die KI prüft dieselbe
Bedingung vor (`powerMargin < nextDef.PowerRequired + PowerReserve`) und muss sie
nur ohne den Bool prüfen.

Ausserdem: der Scan (`Zeile 228-250`) merkt sich nur HQ, Refinery, Barracks und
den Power-Bool. VehicleFactory und ResearchLab brauchen dieselbe Behandlung —
Raw-ID für die Warteschlange, Vorhandensein für die Bauliste.

### 7.3 Rücklage, sonst wird nie gebaut

Wenn §8 die Bank leerkauft, ist die Fahrzeugfabrik (900 AE) an keiner Kadenz
bezahlbar, und die Liste bleibt bei Eintrag 2 stehen — ohne dass irgendetwas rot
wird. Deshalb gehört ein Profilwert dazu:

`ConstructionReserveAE` — solange ein Eintrag der Bauliste fehlt, wird dieser
Betrag nicht für Einheiten ausgegeben. **Aus-Stellung 0**, Startwert = Kosten
des nächsten Listeneintrags.

---

## 8 · Regel 4 — die Kaufregel und das Armee-Stärkeziel

### 8.1 Was gekauft wird

Heute: Infanterie, bis `TargetArmySize` (12) erreicht ist
(`SkirmishAiSystem.cs:456-466`).

Neu, in einem Satz: **kaufe die Rolle mit dem besten Kampfwert je AE, die
gerade produzierbar ist.**

```
Nutzen(rolle) = S(rolle bei voller Gesundheit) × 100 / CostAE(rolle)
```

Produzierbar heisst: das Produktionsgebäude steht **fertig**, der Tier ist
freigeschaltet (`ConstructionSystem.IsT2Unlocked`), die Warteschlange hat Platz
(5 Einträge), und der Preis ist gedeckt. Gleichstand → niedrigere Definitions-ID,
damit die Reihenfolge nie von einem Scan abhängt.

**Warum das keine eigene Aus-Stellung braucht:** Mit `BuildOutDepth: 2` gibt es
nur die Kaserne, und der einzige verfügbare Tier-1-Bau daran ist
`BasicInfantry` — der Raketenschütze ist T2 und braucht das Forschungslabor
(Eintrag 4). Die Regel wählt also aus einer einelementigen Menge und
reproduziert das heutige Verhalten exakt. Die Aus-Stellung ist die Bauliste.

### 8.2 Wieviel gekauft wird

`TargetArmySize: 12` ist eine Einheitenzahl und damit derselbe fraktionsblinde
Zähler wie das Wellentor. Ersatz:

```
TargetArmyStrengthPoints  —  produziere, solange S(Armee lebend + in Warteschlange) < Ziel
```

- **Aus-Stellung: 0** → `TargetArmySize` wie heute.
- Startwert zum Messen: **2400** — zwei volle Wellen zu 1200. Das ist die
  Voraussetzung dafür, dass Regel 2 überhaupt etwas nachzuschicken hat: bei
  Obergrenze 12 und zwölf Einheiten draussen produziert die KI **nichts**, bis
  jemand stirbt. Der Nachschub von heute besteht ausschliesslich aus Ersatz für
  Gefallene.
- Die Warteschlange zählt mit (`CountQueuedAt`), sonst kauft die KI je Kadenz
  denselben Bedarf erneut.

### 8.3 Wird die Bank damit leer?

Rechnung, keine Zusage. Bei `TargetArmyStrengthPoints: 2400`, gedeckt mit Lynx
(962 Punkte / 600 AE): rund 2,5 Panzer, also ~1.500 AE stehende Armee statt
heute 1.440 AE Infanterie. **Das allein leert die Bank nicht.** Was sie leert,
ist die Kombination:

| Hebel | Wirkung auf die Ausgaben |
|---|---|
| Bauliste bis Eintrag 4 | einmalig 2.600 AE (Allianz) an Gebäuden |
| Höheres Stärkeziel | dauerhaft, linear im Ziel |
| **Ersatzkauf bei Verlusten** | der grosse Posten: 39 bzw. 90 verlorene Einheiten je Partie werden bei höherem Ziel teurer nachgekauft |
| Zweite Kaserne / zweites Werk (Eintrag 5) | hebt den **Durchsatz** — ohne ihn ist die Warteschlange der Engpass, nicht die Bank |

Der Punkt, an dem man das Stärkeziel weiter hochzieht, ist erreicht, wenn die
Credits-Kurve trotzdem monoton steigt. Diese Kurve steht in jedem Bericht und
ist die ehrlichste Antwort auf „gibt die KI ihre Punkte aus".

### 8.4 Was Fahrzeuge am Verhalten sonst ändern

- **Kein Sonderfall bei den Wellen.** `IsCombatRole` deckt die Rollen 12..17 ab
  (`SkirmishAiSystem.cs:1335-1338`) — Fahrzeuge sind bereits Kampfeinheiten,
  sammeln bereits, ziehen sich bereits zurück. Es gibt hier nichts zu bauen.
- **Gemischte Geschwindigkeiten.** Rifleman 4, Lynx 4, Aegis 3, Longbow 2,5,
  Jackal 6. Eine gemischte Welle kommt gestaffelt an. Das Stärketor macht das
  nicht schlimmer als das Zähltor — es ist aber der erste Ort, an dem ein
  „warte auf die Langsamen" später Sinn ergäbe. **Nicht in diesem Plan.**
- **Der Kampfwert ist gemischtarmee-fähig, `ScoreTarget` nur bedingt.** Die
  Zielwahl mittelt Schaden über die ganze Armee
  (`SkirmishAiSystem.cs:1260-1262`); bei gemischter Armee ist der Mittelwert eine
  gröbere Auskunft als bei zwölf gleichen Einheiten. Das ist bereits im Code
  vermerkt und wird durch Fahrzeuge **relevant**, nicht falsch. Zielwahl je
  Einheit ist ein eigener Schritt und bleibt von F001 blockiert.

---

## 9 · Die Profilwerte in einer Tabelle

Alle `int`, alle in `AiProfile` (`Assets/_Project/Scripts/AI.Data/AiProfile.cs`),
alle mit Aus-Stellung.

| Feld | Aus | Startwert | Wirkt in |
|---|---:|---:|---|
| `WaveStrengthPoints` | 0 | 1200 | §5 Wellentor |
| `ReinforceMinStrengthPercent` | 0 | 50 | §6 Nachschub |
| `BuildOutDepth` | 2 | 3 | §7 Bauliste |
| `ConstructionReserveAE` | 0 | 900 | §7.3 Rücklage |
| `TargetArmyStrengthPoints` | 0 | 2400 | §8 Kaufmenge |

Vier Dinge, die dabei zu tun sind und leicht vergessen werden:

1. **`AiBehaviorId.ComputeProfileHash` erweitern**, in derselben Reihenfolge, in
   der die Felder stehen (`AiBehaviorId.cs:96-116`). Die Feldreihenfolge *ist*
   der Hash. Der angezeigte Bezeichner ändert sich dadurch — das ist der Zweck
   des Hashes, kein Nebeneffekt.
2. **`AiProfile.SchemaVersion` bleibt 1.** Felder anhängen ist keine
   Bedeutungsänderung; die Version bumpt, wer ein Feld entfernt oder umdeutet.
3. **`Equals`/`GetHashCode` mitziehen.** Die Gleichheit vergleicht jeden Wert —
   ein vergessenes Feld meldet „keine Änderung", wo eine ist. Genau dieser Fehler
   ist am alten `AiFactionProfile` schon einmal passiert.
4. **`Compare/LabProfiles.cs` im Labor** nachziehen — die Kandidatenprofile
   konstruieren `AiProfile` mit allen Argumenten.

---

## 10 · Wie der Code dafür aussieht

### 10.1 Eine Umstellung in `Decide()`, sonst nichts Strukturelles

Heute: Haltung → je Einheit Zuweisung → gruppiertes Einreichen. Die Haltung
braucht jetzt die Rückzugsentscheidung, die heute erst in der Zuweisung fällt.
Die Reihenfolge löst das ohne neue Zustände:

```
1  Sammelzelle bestimmen          GetStagingCell(hq, feind)      // hängt an nichts
2  Bedrohungszellen sammeln       CollectVisibleThreatCells()    // hängt nur an der Sicht
3  je Einheit: flüchtet sie?      IsRetreating(u, sammelzelle, bedrohungen)
4  S_draussen / S_sammelnd        Summen über die Nicht-Flüchtenden
5  Haltung auflösen               Regel 1 und Regel 2 -> WaveReady, Nachschub an/aus
6  je Einheit: Zuweisung          wie heute, mit dem Nachschub-Zweig
7  gruppiert einreichen           unverändert
```

Schritt 1 und 2 hängen nicht von der Haltung ab — deshalb geht das, und deshalb
bleibt es eine reine Funktion. Kein neues System, keine neue Tick-Position;
`SkirmishAiSystem` liegt zwischen Combat und Victory und bleibt dort.
`MatchRunner` wird nicht angefasst.

### 10.2 Wo der Kampfwert wohnt

`Nova.AI.Data` referenziert **nur** `Nova.Core` — bewusst nicht
`Nova.Simulation`, damit ein Profil keine `UnitRole` nennen und keine zweite
Definitionstabelle werden kann. Die Formel braucht `UnitRole`, `FactionId` und
`WeaponProfiles`; sie gehört also nach **`Assets/_Project/Scripts/AI/`**, als
neue Datei `CombatStrength.cs`: eine statische Klasse, kein System, keine
Registrierung, kein Zustand.

Die Zahlen (§9) bleiben in `AI.Data`. Verhalten in C#, Zahlen an einer Stelle —
dieselbe Trennung wie bisher.

### 10.3 Tests, die dazugehören

- Kampfwert je Rolle und Fraktion **gegen die Tabelle aus §3.2**, Wert für Wert
  — inklusive der Abschneidung (`962`, nicht `963`).
- Unbewaffnet ⇒ 0, für Builder, Harvester und die acht unbewaffneten
  Gebäudearten.
- Wellentor: mit `WaveStrengthPoints: 0` fährt die kanonische Partie in
  denselben Endzustand — die Aus-Stellung als Test, nicht als Zusage.
- Nachschub: eine gesetzte Lage mit voller Welle draussen ⇒ die sammelnde
  Einheit marschiert; dieselbe Lage mit angeschlagener Welle ⇒ sie bleibt.
  Geprüft an **Positionen**, nicht an Intents — so hat der Wellentest von V004
  seinen Wert bekommen.
- `AiProfileTests` um die fünf Felder erweitern.

Alle in `tools/Nova.SimRunner.Tests/` — neue Dateien dürfen wir dort jederzeit
anlegen. **Keine der vier Baseline-Dateien** wird angefasst; eine reine
KI-Änderung macht sie ohnehin nicht rot, die Trennungsregel gilt trotzdem, und
`check_baseline_guard.py` führt `Scripts/AI/` in seinen Pfaden.

---

## 11 · Schwierigkeitsgrade — wofür das Ganze modular ist

Erst wenn §5–§8 stehen, ist ein Schwierigkeitsgrad ein **Zahlensatz** und keine
zweite KI. Ein Vorschlag als Ausgangspunkt, nicht als Empfehlung — jede Spalte
ist zu messen:

| Wert | `ms1-easy` | `ms1-canonical` (heute) | `ms1-hard` |
|---|---:|---:|---:|
| `WaveStrengthPoints` | 700 | 1200 | 2000 |
| `ReinforceMinStrengthPercent` | 0 *(aus)* | 50 | 65 |
| `BuildOutDepth` | 2 | 3 | 4 |
| `TargetArmyStrengthPoints` | 1200 | 2400 | 5000 |
| `TargetHarvesters` | 2 | 3 | 4 |
| `RetreatHealthPercent` | 0 *(aus)* | 60 | 60 |
| `DecisionTickInterval` | 20 | 20 | 20 |

Vier Regeln für diese Tabelle, die wichtiger sind als die Werte darin:

1. **Nicht über die Kadenz regeln.** `fast-cadence` (20→10) ist gemessen: 0 %
   Siege, +103 % Spieldauer, doppelte Intents. Schneller denken macht die KI
   nicht besser, sondern zappeliger — und übermenschliche APM lesen sich als
   unfair.
2. **Nicht über Ressourcenboni.** Unsere KI geht denselben Befehlsweg, hat
   dieselben Startmittel und sieht nur die committete Team-Sicht. Das ist ein
   Aktivposten, den ein Bonus wegwerfen würde.
3. **Nicht über den Rückzug „nach oben".** `retreat-75` hat den besseren
   Austausch (166 gegen 131) und **0 % Siege bei 17.770 Ticks**. Eine Kennzahl
   allein hätte hier die schlechtere KI gewählt.
4. **Kein mittlerer Wert ohne Messung.** `waveSize 6` lag unter `wave-off` —
   eine halbvolle Welle ist schlechter als gar keine. Für jeden neuen Regler
   gilt dieselbe Warnung, bis die Kurve dagegen spricht.

„Leicht" heisst hier also: greift früher mit weniger an, schickt keinen
Nachschub nach, baut keine Fahrzeuge, zieht sich nicht zurück. Es ist **nicht**
gedrosselt, sondern schlechter beraten — was laut Literatur auch der bessere
Weg ist: überzeugende Fehler sind schwerer zu bauen als perfektes Spiel.

---

## 12 · Was das Labor dafür braucht

Ohne diese drei Erweiterungen ist die zentrale Behauptung des Plans nicht
messbar. Alles davon ist **Laborarbeit**, kein Spielcode:

| Wo | Was | Wofür |
|---|---|---|
| `Metrics/SlotMetrics.cs`, `Metrics/TraceCollector.cs` | Spalten `armyStrength`, `stagingStrength`, `fieldStrength` je Metriktick | Die Kurve, an der „Angriffsstärke fällt rapide" überhaupt sichtbar wird |
| `Metrics/FeelMetrics.cs` | Wellenzyklen: Ticks zwischen zwei Losmärschen, Stärke beim Losmarsch, Stärke beim Bruch | Rhythmus statt Endstand — die Kennzahl, die bei den Wellen schon gefehlt hat |
| `Compare/LabProfiles.cs` | Kandidaten `strength-gate`, `reinforce-50`, `reinforce-off`, `vehicles`, `army-strength-2400` | Einseitige Messung: dasselbe Binary, ein Profilwert Unterschied (M001) |
| `report/lab_data.py`, `report/markdown_report.py` | die neuen Spalten in beide Berichtsformen | Ohne das steht die Zahl nur in `trace.ndjson` |

Die **Stärkekurve je Slot** ist dabei die eine, die vor dem ersten
Verhaltens-PR fertig sein muss — sie vermisst den heutigen Stand und ist damit
die Referenz, gegen die alles weitere gehalten wird.

---

## 13 · PR-Serie

Gestapelt, je eine Verhaltensänderung, je eine Aus-Stellung, je ein
Journaleintrag mit Abschnitt „Schlechter".

| # | Branch | Was | `Revision` | Erste Zahl, die man ansieht |
|---:|---|---|---:|---|
| 0 | *(nur Labor)* | Stärkemetrik, Kandidatenprofile, Berichtsspalten | — | die heutige Stärkekurve als Referenz |
| 1 | `feat/ai-combat-strength` | `CombatStrength.cs` + Profilfelder, **von keiner Regel gelesen** | 4 *(kein Bump)* | Endzustands-Hash unverändert — der Nachweis, dass es verhaltensneutral war |
| 2 | `feat/ai-strength-wave-gate` | §5 Wellentor über Stärke | **5** | Legion: Wellengrösse in Einheiten; Formationsverteilung am Sammelpunkt |
| 3 | `feat/ai-reinforce-doctrine` | §6 Nachschub / Abbruch | **6** | **Intents je 1.000 Ticks** (V002-Fehlermodus) |
| 4 | `feat/ai-vehicle-buildout` | §7 Bauliste bis Fahrzeugwerk **+** §8.1 Kaufregel | **7** | `buildingsByRole`, Credits-Kurve, Armeezusammensetzung |
| 5 | `feat/ai-army-strength-target` | §8.2 Stärkeziel statt Einheitenzahl | **8** | Credits-Kurve: steigt sie immer noch monoton? |
| 6 | `feat/ai-difficulty-profiles` | §11, **nur Zahlen** | 8 *(kein Bump)* | drei Profile nebeneinander, keine Rangliste |

**Reihenfolge, die nicht verhandelbar ist:**

- **0 vor allem.** Wer die Stärkekurve erst nach der ersten Verhaltensänderung
  baut, hat keine Referenz mehr, gegen die er sie hält.
- **2 vor 3.** Der Abbruchwert ist ein Prozentsatz **von** `WaveStrengthPoints`.
  Ohne den Schwellwert hat er keinen Bezug.
- **4 vor 5.** Ein Stärkeziel ohne Fahrzeugfabrik kauft nur mehr Infanterie —
  das misst den halben Effekt und verbrennt die Messung.
- **1 kann entfallen**, wenn 2 klein genug bleibt. Getrennt gebaut hat es einen
  Vorteil: Der byte-identische Nachweis trennt „die Formel ist falsch" von „die
  Regel ist falsch", und genau diese Trennung hat Schritt 0 der Wellenarbeit
  erst auswertbar gemacht.

Je PR, ohne Ausnahme: Referenz vorher sichern, **Determinismus zuerst**
(`match --repeat 2`, Exit 2 = sofort Schluss), Hash-Kette gegen die Referenz
diffen (der erste abweichende Tick ist die wertvollste Zahl), beide Suiten
fahren, `AiBehaviorId.Revision` bumpen, Journaleintrag schreiben, Changelog-Zeile
unter `[Unreleased]`. Der Abschnitt „Im laufenden Spiel gesehen" bleibt **leer
und als leer erkennbar**, bis ein Mensch gespielt hat.

---

## 14 · Scope

**Angefasst wird ausschliesslich:**

| Pfad | Was |
|---|---|
| `Assets/_Project/Scripts/AI/SkirmishAiSystem.cs` | Haltung, Nachschub-Zweig, Bauliste, Kaufregel |
| `Assets/_Project/Scripts/AI/CombatStrength.cs` *(neu)* | die Formel |
| `Assets/_Project/Scripts/AI.Data/AiProfile.cs`, `AiProfiles.cs`, `AiBehaviorId.cs` | fünf Felder, Startwerte, Hash, Revision |
| `tools/Nova.SimRunner.Tests/` *(neue Dateien)* | Tests |
| `CHANGELOG.md` | eine Zeile je PR |
| `Nova.AiLab/` | Metriken, Kandidatenprofile, Berichte |

**Gelesen, aber nie geändert** — und das ist die Grenze, an der dieser Plan
steht und stehen bleibt:

- `Simulation/Definitions/` — die Kampfwerte kommen aus `SimDefinitions` und
  `WeaponProfiles`. Geteilte Vertragsfläche: **Absprache, nicht Umsetzung.**
  Der Plan ändert dort keine Zahl; er macht die vorhandenen Zahlen nur
  vergleichbar.
- `Simulation/Construction/`, `Simulation/Economy/` — `ValidatePlacement`,
  `IsCellFree`, `IsT2Unlocked`, `PlayerEconomyState` werden aufgerufen wie
  heute. Netzstrang ab Sprint 16, in 13–15 fasst sie niemand an.
- `Simulation/Production/` — `TryGetProducer`, `TryGetQueueEntry` wie heute.
- `ICommandTransport` / `ICommandSubmissionReadiness`, Match-Fingerprint,
  **Tick-Reihenfolge** — benutzt, nicht geändert. Kein neues System.
- Die vier Baseline-Dateien.

---

## 15 · Woran der Plan scheitern darf

Damit später nicht nachträglich umdefiniert wird, was Erfolg war — je Regel ein
Satz, der sie kippt:

| Regel | Gilt als widerlegt, wenn |
|---|---|
| Kampfwert | Der Wert ordnet Rollen anders, als eine Duellmessung sie ordnet. Die Gegentabelle (`duel`, 576 Duelle) ist die unabhängige Gegenprobe und existiert bereits |
| Stärke-Wellentor | Die Legion sammelt zwar mehr, verliert aber genauso viel — dann trägt „Punkte statt Köpfe" nicht, und der Zähler war nicht die Ursache |
| Nachschub-Doktrin | **Intents je 1.000 Ticks steigen** ohne besseres Spiel. Das ist V002, und es zählt beim zweiten Mal genauso |
| Bauliste / Fahrzeuge | Die Credits-Kurve bleibt monoton steigend. Dann ist die Bank nicht das Problem, sondern der Durchsatz — und die zweite Kaserne wäre der richtige PR gewesen |
| Stärkeziel | Die Partie endet nur noch im Zeitlimit. Grössere Armeen, die einander nicht mehr entscheiden können, sind der Fehlermodus von `retreat-75` in neuer Form |

Und die Regel über allen: **Ein grüner Laborlauf ist Diagnose, kein Nachweis.**
Fertig ist keiner dieser Schritte vor einer gespielten Partie, und was nicht
gesehen wurde, steht als ungesehen im PR-Text.

---

## 16 · Was hier nicht drinsteht, und warum

| Nicht in diesem Plan | Warum |
|---|---|
| **`DefendBase`** — Armee heim, wenn die Basis brennt | Eigene Verhaltensänderung, eigener PR. Sie wird durch diesen Plan **dringender**: eine KI, die auf 2.400 Punkte spart, steht länger am Sammelpunkt und ist länger konterbar. In V002 gescheitert, braucht ein Mass für „echte Bedrohung" — der Kampfwert wäre genau dieses Mass, aber das ist der übernächste Schritt |
| **Zielwahl je Einheit** | Blockiert von Befund F001: `Stop()` löscht `AttackTarget` nicht, ein Ziel ist nicht freigebbar. Vier Fassungen gemessen, alle teurer als gar nicht zielen |
| **Verteidigungsanlagen** (`DefensePlatform`) | `InstallDefenseModule` wird deterministisch abgelehnt (G2/G4-Inhalt). Ob das **Gebäude** selbst platzierbar ist — es ist bewaffnet, 20 Schaden, 10 Reichweite — ist **ungeprüft**. Das nachzusehen lohnt, es gehört aber zur Verteidigung und nicht hierher |
| **Abnutzungskriterium** aus §6.3 | Zweite Verhaltensregel. Erst messen, ob die Niveauregel reicht |
| **Aufklärung / Abstandhalten** | Voraussetzung dafür, dass Artillerie je ein sinnvoller Kauf wird. Eigener Strang (NEXT-STEPS §6), Paar aus `Movement/` und `AI/` |
| **Lanchester-Terme, Panzerungsgewichtung im Kampfwert** | §3.3. Erst wenn eine Messung zeigt, dass die Summe falsch entscheidet |
| **Sim-RNG für Wiederspielwert** | Erlaubt (§4 verbietet `System.Random`, nicht den Kernel-RNG), aber es koppelt KI-Verhalten an jedes künftige System, das ebenfalls zieht. Vorschlag, keine Umsetzung |
