# Roadmap — KI-Verhalten, eine Liste und eine Nummerierung

**Stand:** KI-Verhalten `r10.E75CB19D` · Branch `integration/ai-goals`
(Goal-System `r8` aus PR #96 und der Feld-Fix aus PR #97 lokal
zusammengeführt, **beides noch nicht in `upstream/main`**) ·
Definitionstabelle `0xD5F219A3F68088FF` · Referenzlauf auf diesem Stand:
Entscheidung **4.767**, Endzustand **`0x6AEA773463272F32`** · Messgrundlage
[`reports/latest.md`](reports/latest.md) · Historie
[`reports/behavior-log.md`](reports/behavior-log.md)

> [!IMPORTANT]
> **Diese Datei ist die einzige Nummerierung.** Vorher gab es drei, die sich
> widersprachen. Die anderen Dokumente behalten ihre Aufgabe und verweisen
> hierher:
>
> | Dokument | Aufgabe |
> |---|---|
> | **`ROADMAP.md`** (hier) | Reihenfolge, Stand, Scope, Rückfragen |
> | [`NEXT-STEPS.md`](NEXT-STEPS.md) | **warum** ein Punkt so eingeordnet ist — sieben Beobachtungen aus gespielten Partien |
> | [`KAMPFSTAERKE.md`](KAMPFSTAERKE.md) | Detailplan: Kampfpunkte, Wellengrösse, Nachschub, Ausbau, Schwierigkeitsgrade |
> | [`VERTEIDIGUNG.md`](VERTEIDIGUNG.md) | Detailplan: Sammeln abbrechen, wenn die eigene Basis brennt |
> | [`GOALS.md`](GOALS.md) | Detailplan: modulares Goal-System, Flanke, Admin-Panel |
> | [`reports/behavior-log.md`](reports/behavior-log.md) | was tatsächlich passiert ist — besser, schlechter, **widerlegt** |

---

## 1 · Was steht

Alles hier ist in `upstream/main` und einseitig gemessen. Die Zahl in der
letzten Spalte ist die, die die Entscheidung getragen hat — nicht die
schmeichelhafteste.

| Liefert | Was | Journal | Die Zahl |
|---|---|---|---|
| `r1` | Score-Zielwahl statt „HQ vor Gebäude vor Einheit" | [V001](reports/behavior-log.md) | Entscheidung 33 % früher; `early-push` fällt von 50 % auf 0 % |
| — | ~~`DefendBase`~~ **verworfen** | [V002](reports/behavior-log.md) | +23 % Intents durch Pendeln, schlechter auf jeder Achse |
| — | ~~Zielen unter der Angriffsschwelle~~ **zurückgenommen**, vier Fassungen | [V003](reports/behavior-log.md) | beste Fassung 11 % später, 10 % mehr Verluste. Blockiert von [F001](findings/F001-stop-loescht-attacktarget-nicht.md) |
| `r3` | Sammelpunkt und Wellengrösse | [V004](reports/behavior-log.md) | Verluste 41 statt 175, Intervalle mit Verlusten 11 statt 64 |
| `r4` | Rückzug als Filter je Einheit, **ohne** Lebens-Hysterese | [V005](reports/behavior-log.md) | Verluste 35 statt 62, Austausch 123 statt 93 |
| `r5` | Erreichbarkeitsdeckel am Wellentor (`f13f4d5`) | [V006](reports/behavior-log.md) | die Blockade, in der elf Einheiten bis zum Zeitlimit warteten |
| `r6` | **Stärke-Wellentor** — Kampfpunkte statt Köpfe (PR #72) | [V007](reports/behavior-log.md) | mit angehobener Obergrenze: Legion 23 Verluste statt 51, Austausch 139 statt 45 |
| — | ~~Rally-Punkt auf den Sammelpunkt~~ **gestrichen** | [F002](findings/F002-rallypoint-ist-die-spawnzelle.md) | der Rally-Punkt ist die Spawn-Zelle; das wäre Teleportation |
| — | **Goal-System als Form** (Punkt 2) — benannte Module statt Zweige, dazu Beobachter und Goal-Maske | noch kein Journaleintrag: **es gibt keine Verhaltensänderung zu berichten** | Entscheidungstick **3213** und Endzustand **`0xE002DD893916967B`** unverändert, Artefakte byte-identisch bis auf `elapsedMilliseconds`. Kein `Revision`-Bump |
| `r10` | **HQ-Gewicht statt Kurzschluss** (Punkt 4) — ein verteidigtes Hauptquartier kann gegen ein weiches Ziel abseits verlieren. `targetHqWeight: 100`, **0 = der alte Kurzschluss** | [V010](reports/behavior-log.md) | **Zielarten 3 → 5**, und der Anstieg hält über vier Armeeobergrenzen. Entscheidung 35 % früher, 14 statt 33 eigene Verluste, Intents unverändert |
| `r9` | **Nachschub-Doktrin** (Punkt 3) — das Nachrücken bekommt eine Bedingung und einen Schalter. **Ausgeschaltet ausgeliefert** (`reinforceMinStrengthPercent: 0`) | [V009](reports/behavior-log.md) | die zwei Sitze überschneiden sich in **einem** Prozentwert; bei Obergrenze 30 ist **jede** Stellung schlechter als ohne die Regel, drei davon verlieren eine gewonnene Partie |
| `r8` | **`DefendHome`** (Punkt 1) — Sammeln abbrechen, wenn die eigene Basis brennt. Aus-Stellung `defendHomeCells: 0` | [V008](reports/behavior-log.md), am Player angesehen ([B004](reports/behavior-log.md)) | „wehrlos im Beschussfenster" **96 % → 60 %** — und dafür **60 statt 18** eigene Verluste. **Verschiebt die Niederlage, wendet sie nicht ab** |

> [!WARNING]
> **`r6` ist gebaut und im ausgelieferten Spiel wirkungslos.** `waveStrengthPoints: 1200`
> steht in `AiProfiles.Ms1Canonical` — aber die Armeeobergrenze ist 12, und
> 12 Einheiten sind bei beiden Fraktionen weniger wert als die Schwelle. Der
> Erreichbarkeitsdeckel setzt das Tor damit auf „sammle die ganze Armee",
> also genau auf das Verhalten von `r3`. Das Tor beginnt erst zu entscheiden,
> wenn die Obergrenze steigt (Allianz ab 13, Legion ab 29).
>
> **Die Obergrenze ist nicht unsere Zahl** — `MatchRunner.cs:254` verdrahtet
> `targetArmySize: 12`, und `Gameplay/Match/` gehört dem Netzstrang. Der ganze
> Stärkestrang hängt an dieser einen Rückfrage → §4.

## 2 · Als nächstes

Die Nummer ist die **Reihenfolge**, nicht die Revision. Jede Zeile ist ein PR,
jede bringt einen Profilwert mit Aus-Stellung mit und wird **einseitig**
gemessen (Methodenbefund [M001](reports/behavior-log.md)).

> [!NOTE]
> **Die Spalte „Liefert" ist nachgezogen.** Sie war um eins verschoben: `r7`
> hat der Inhaber für die D-103-Voraussetzungskette vergeben (Refinery → Power
> → Barracks), und `r8` ist seit `DefendHome` ebenfalls vergeben. Die Nummern
> unten sind jetzt die tatsächlich nächsten freien. Die **Reihenfolge** in der
> ersten Spalte ist die verbindliche — die Revision ist nur eine Zusage darüber,
> wie der Bezeichner danach heisst.

| # | Was | Liefert | Ort | Aus-Stellung | Erste Zahl, die man ansieht | Plan |
|---:|---|---|---|---|---|---|
| ~~3~~ | ~~**Nachschub-Doktrin**~~ — **gebaut und gemessen, liegt aus** (`r9`, [V009](reports/behavior-log.md)). Der Code steht, der Schalter steht, kein Wert hat sich gehalten | — | — | — | — | — |
| ~~4~~ | ~~**Zweites lohnendes Ziel**~~ — **gebaut, gemessen, angenommen** (`r10`, [V010](reports/behavior-log.md)). Die Aus-Stellung wurde 0 statt „sehr gross": ein Sentinel lässt den alten Zweig denselben Code sein, „überstimmt alles" wäre ein Argument über Kartengrösse und Schadenstabelle | — | — | — | — | — |
| 5 | **Wellengrösse nach Lage** — Kampfpunkte der gesehenen Feindgruppe bestimmen die Schwelle | `r11` | `AI/`, `AI.Data/` | `waveOvermatchPercent: 0` | Verteilung der Wellengrössen (braucht **L1**) | [KAMPFSTAERKE §5a](KAMPFSTAERKE.md) |
| 6 | **Basis ausbauen und Fahrzeuge kaufen** — Bauliste bis Fahrzeugwerk, Kaufregel nach Punkten je 100 AE | `r12` | `AI/`, `AI.Data/` | `buildVehiclePlant: false` | `buildingsByRole`, Credits-Kurve, Armeezusammensetzung | [KAMPFSTAERKE §7–§8.1](KAMPFSTAERKE.md) |
| 7 | **Armee-Stärkeziel statt Kopfzahl** — produziert wird auf Punkte hin | `r13` | `AI/`, `AI.Data/` | `targetArmyStrengthPoints: 0` | Credits-Kurve: steigt sie immer noch monoton? | [KAMPFSTAERKE §8.2](KAMPFSTAERKE.md) |
| 8a | **Anmarsch über eine Route statt der Luftlinie** | `r14` | `AI/`, `Pathfinding/` | `detourDamageBudget: 0` | Schaden vor dem ersten eigenen Schuss | [NEXT-STEPS §2](NEXT-STEPS.md) |
| 8b | **Formationsangriff / Flanke** — zwei Anmarschwege auf dasselbe Ziel | `r15` | `AI/` | `flankMinGroupPoints: 0` | Austauschverhältnis; Schaden vor dem ersten Schuss (braucht **L2**) | [GOALS §5](GOALS.md) |
| 9 | **`DefendBase`, zweiter Anlauf** — mit Kampfpunkten als Mass für „echte Bedrohung" | `r16` | `AI/` | `defenseRadiusCells: 0` | Intents je 1.000 Ticks, dann Reaktionslatenz | [NEXT-STEPS §3](NEXT-STEPS.md) |
| 10 | **Abstandhalten *plus* Aufklärung** — nur als Paar | `r17` | `Movement/`, `AI/` | `standoffSlackCells: 0` | `usableRangeOvershootCells` (heute **7** von 20) | [NEXT-STEPS §6](NEXT-STEPS.md) |
| 11 | **Schwierigkeitsgrade** — drei Zahlensätze, keine zweite KI | kein Bump | `AI.Data/` | entfällt | drei Profile nebeneinander, **keine Rangliste** | [KAMPFSTAERKE §11](KAMPFSTAERKE.md) |

### Laborarbeit — kein PR ins Spiel, aber Voraussetzung

| # | Was | Wofür | Vor welchem Punkt |
|---|---|---|---|
| **L1** | **Wellenmetrik**: Anzahl der Wellen, Stärke beim Losmarschieren, Abstand zwischen Wellen, als Verteilung | „mehr verschiedene Wellengrössen" ist ohne diese Spalte eine Behauptung. Heute misst nichts eine Welle als Ereignis | **vor 5** |
| **L2** | **Szenario Anmarsch gegen stehende Verteidigung**: dieselbe Gruppe frontal, dann in zwei Hälften aus zwei Richtungen | Ohne dieses Szenario ist die Flanke nicht belegbar, nur plausibel | **vor 8b** |
| **L3** | ~~**Admin-Panel und Live-Modus**~~ — **gebaut, bis auf die Vorausschau**: `goals.ndjson`, das Goal je Einheit im Player samt Kipppunkten, und `live --port` mit Anhalten, Einzeltakt und Übersteuern. Offen bleibt allein die **Vorausschau** („was tut sie in den nächsten N Ticks") | Damit man *sieht*, was die KI vorhat, statt es aus Kennzahlen zu erschliessen | erledigt |

### Die vier neuen Punkte, in je einem Absatz

**5 · Wellengrösse nach Lage.** Heute ist die Wellenschwelle eine Konstante:
`waveStrengthPoints: 1200`, egal was drüben steht. Neu bemisst sich die Welle an
der **Kampfpunktsumme der Feindgruppe am Ziel** — gruppiert über die sichtbaren
Feindeinheiten im Umkreis der Zielzelle, mal einem Übermachtfaktor, begrenzt
durch eine Untergrenze (sonst kehrt das Förderband aus `r3` zurück) und die
heutige feste Zahl als **Obergrenze**. Daraus entstehen verschieden grosse
Wellen ohne Sonderfall: kleiner Stoss gegen einen einzelnen Harvester,
Grossangriff gegen eine verteidigte Basis. Vier Lagefaktoren gehen ein —
gesehene Feindstärke, Reststärke der eigenen Welle draussen, Wert des Ziels und
Ausbaustand als Partiephase. **Deshalb steht 4 vor 5:** solange jedes Ziel das
HQ ist, gibt es nur eine Lage. Formeln, Profilwerte und Widerlegungssätze in
[KAMPFSTAERKE §5a](KAMPFSTAERKE.md).

**6 · Basis ausbauen und Fahrzeuge kaufen.** Die Bauliste hört heute hinter der
Kaserne auf (`SkirmishAiSystem.cs:299-314`), und die Bank steht bei über 17.000
AE — die KI verdient, ohne auszugeben. Der Punkt baut bis zum Fahrzeugwerk
weiter und kauft danach nach **Punkten je 100 AE** statt nach Stückzahl; die
Tabelle dafür steht schon ([KAMPFSTAERKE §3.2](KAMPFSTAERKE.md)) und weist
Kampfpanzer als besten und Artillerie als schlechtesten Kauf aus — letzteres
deckt sich mit einer Messung, nicht mit einem Gefühl.

**8b · Formationsangriff / Flanke.** Die Welle teilt sich in zwei Hälften mit je
etwa gleicher Punktsumme und läuft aus zwei Richtungen auf **dasselbe** Ziel.
Gemacht wird das nur, wenn die KI aus ihrer eigenen Sicht ausrechnet, dass es
sich lohnt: erwarteter Schaden auf einem Weg gegen erwarteten Schaden auf zwei,
ganzzahlig, aus der committed Sicht. **Zwei ehrliche Grenzen:** Die Simulation
kennt **keine Blickrichtung** — es gibt keinen Flankenschadensbonus, der
Gewinn kann nur aus kürzerer Zeit im Feuer und aufgeteiltem gegnerischem Feuer
kommen, und das zweite ist eine Behauptung, bis **L2** sie stützt. Und eine
geteilte Welle ist zwei kleinere Wellen — genau das Förderband, das `r3`
beseitigt hat. Die Bedingung dagegen steht in [GOALS §5](GOALS.md).

**2 · Goal-System als Form — gebaut.** Die KI hatte keinen Goal-Begriff: eine
abgeleitete Haltung und eine Zuweisung je Einheit, verteilt über if-Zweige. Jetzt
wählt sie je Einheit und Kadenz **ein** Goal aus einer festen Prioritätsliste —
`Retreat`, `Attack`, `Hold`, `Advance` — und wendet dessen Wirkung aus einer
Tabelle an. Byte-identisch nachgewiesen, wie schon beim Umbau auf Absichten je
Einheit.

> **Eine Abweichung vom Plan, und sie ist der Kern des Nachweises.** Der Plan
> wollte die Prioritäten als Profilwerte („0 = Modul aus"). Ein angehängtes
> Profilfeld bewegt `AiProfile.ProfileHash` und damit `aiBehaviorId` — und das
> steht in jedem `result.json`. Der Schritt wäre damit **nicht** byte-identisch
> gewesen, und die Byte-Gleichheit ist genau das, was ihn auswertbar macht. Die
> Reihenfolge ist deshalb fest im Code. **Eine Aus-Stellung bringt jedes Modul
> mit, das eine Regel bekommt** — im PR, der ihm die Regel gibt, so wie es
> `waveSize`, `waveStrengthPoints` und `retreatHealthPercent` schon halten.

Der Nutzen ist doppelt: jeder Punkt danach ist ein Modul statt eines weiteren
Zweigs, und das Panel **zeigt**, was die KI vorhat, statt es zu erraten. Dazu
kamen zwei optionale Nähte in `AI/`, die der ausgelieferte Pfad nie füllt:
`IAiGoalObserver` (das Labor liest mit) und `IAiGoalOverride` (das Panel greift
ein — als *Eingabe* der Entscheidung, nicht als Zustand).

## 3 · Reihenfolgeregeln, die nicht verhandelbar sind

| Regel | Warum |
|---|---|
| ~~**1 vor jeder Anhebung der Obergrenze**~~ | **eingelöst** mit `DefendHome` (`r8`). Der Defekt „wartet, während das HQ brennt" wächst mit der Obergrenze — wartende Einheit-Ticks je 1.000 gingen von 3.502 auf 12.326 —, und deshalb musste er vor jeder Anhebung fallen. **Er ist gemildert, nicht behoben:** wehrlos im Beschussfenster 96 % → 60 % (V008) |
| ~~**2 vor 3**~~ | **eingelöst.** Die Module stehen; die Nachschub-Doktrin wird eins davon, kein vierter Zweig |
| ~~**3 vor 5**~~ | **eingelöst** — 3 ist gebaut und gemessen. Die Auflage bleibt inhaltlich stehen: wer den Schalter aus V009 wieder anfasst, tut das **nicht** im selben Zug wie 5, sonst misst man zwei Änderungen als eine |
| ~~**4 vor 5**~~ | **eingelöst** mit `r10`: es gibt jetzt fünf Zielarten statt faktisch einer, also gibt es Lagen |
| **L1 vor 5** | „Verschiedene Wellengrössen" ohne Wellenmetrik ist unbelegbar |
| **6 vor 7** | Ein Stärkeziel ohne Fahrzeugwerk kauft nur mehr Infanterie und misst den halben Effekt |
| **8a vor 8b** | Zwei Routen zu bewerten, ohne eine bewerten zu können, ist geraten |
| **L2 vor 8b** | Sonst ist die Flanke plausibel und nicht belegt |
| **10 als Paar** | „Auf Reichweite stehenbleiben" ohne Aufklärung hat im Kontrolllauf über 2.000 Ticks **null** Schaden angerichtet |

## 4 · Rückfragen an den Maintainer — nicht umsetzen, aufschreiben

| Was | Warum es unsere Grenze überschreitet | Was es blockiert |
|---|---|---|
| **Armeeobergrenze** `targetArmySize` | `MatchRunner.cs:254` verdrahtet die Zahl; `Gameplay/Match/` gehört dem Netzstrang. Unsere Profilwerte greifen nur im Labor und in Tests | **Den ganzen Stärkestrang.** Bei 12 ist `r6` wirkungslos; einseitig gemessen wirkt 30: Legion entscheidet früher (5.005 statt 5.773 Ticks) bei 23 statt 51 Verlusten. **Anheben ohne das Tor geht in die andere Richtung** (51 → 64) |
| ~~**Ein Angriffsziel freigeben**~~ | **erledigt vom Inhaber**, PR #83 (`75973d3`): der `Stop`-Zweig setzt `AttackTarget = EntityId.Invalid`. Befund [F001](findings/F001-stop-loescht-attacktarget-nicht.md) ist aufgelöst | Nichts mehr. **Aber:** V003 bleibt gemessen und teuer — die Sperre ist weg, die Begründung für eine fünfte Fassung nicht da. Und „nur das Ziel aufgeben, weiterlaufen" gibt es weiterhin nicht: `Stop` nimmt den Marschbefehl mit |
| **Sidecar-Block** | `MatchFingerprint` führt `SidecarSchemaVersion` bereits, unbelegt. Timer, Aufklärungsgedächtnis, Squad-Identität brauchen ihn | Alles mit echtem Gedächtnis. **Nicht** die Punkte 1–11: die sind absichtlich zustandslos formuliert |
| **Legion-Waffenwerte** | `Simulation/Definitions/` ist geteilte Vertragsfläche | Issue 01. Das Labor *misst* sie, die Umsetzung braucht Absprache |
| **Ein neues System einordnen** | Die Tick-Reihenfolge ist Vertrag; neu wird **eingeordnet, nicht angehängt** | Nichts auf dieser Liste — alle Punkte bleiben in `SkirmishAiSystem` |

## 5 · Nicht anfangen — begründet

| Vorhaben | Warum nicht |
|---|---|
| **Verteidigungsmodule bauen** (`InstallDefenseModule`) | `ValidateDomain` lehnt den Befehl **unbedingt** ab (G2/G4-Inhalt laut `mvp-v1.json`). Eine KI, die ihn benutzt, produziert nur `intentsRejected`. Ob das **Gebäude** `DefensePlatform` platzierbar ist, ist dagegen ungeprüft und gehört zu Punkt 9 |
| **Zielen unter der Angriffsschwelle, fünfte Fassung** | Vier gemessen, alle teurer als gar nicht zielen (V003). **Der Blocker F001 ist seit PR #83 weg** — die Sperre also, nicht der Befund: die vier Messungen stehen weiterhin. Wer eine fünfte anfängt, sagt vorher, was an ihr anders ist als an den vieren, und misst einseitig |
| **`DefendBase` mit anderem Radius** | Gemessen: 8 → 9.564, 16 → 9.470, 24 → byte-identisch zu 16. Am Radius liegt es nicht (V002). Punkt 9 ändert das Mass, nicht den Radius |
| **Kartenvarianz** | Erst nach Punkt 4. Vorher tunt man gegen die gebrochene `GetEnemyStartAreaCell`-Annahme |
| **Ein automatischer Optimierer** | Nicht vertagt, sondern nicht vorgesehen (Entscheidung 11): es gibt keine skalare Gütefunktion, und für „sieht im Spiel richtig aus" gibt es keine Kennzahl |
| **Adaptives Lernen, Gegnermodellierung** | Braucht Gedächtnis über Partien hinweg — jenseits der Zustandslosigkeit, Sidecar-Bereich |
| **Sim-RNG für Wiederspielwert** | Erlaubt und reizvoll (jede Partie anders, trotzdem bitgenau), koppelt aber KI-Verhalten an jedes künftige System, das ebenfalls zieht. Vorschlag, keine Umsetzung |
| **`Decide()` entschlacken** | Gemessen, kein Problem: 143.000 Ticks/s über 24 Kerne |

## 6 · Was jeder dieser PRs mitbringt

```
1  Referenz sichern, BEVOR etwas geändert wird
2  ändern — nur in AI/, AI.Data/, Combat/, Movement/, Pathfinding/, Factions/
3  Determinismus zuerst:  match --repeat 2     → Exit 2 = sofort Schluss
4  Wirkung einseitig messen: compare, Kandidat MIT gegen Referenz OHNE
5  Hash-Kette diffen — der erste abweichende Tick ist die wertvollste Zahl
6  beide Suiten; die vier Baseline-Dateien dürfen rot werden und bleiben unangetastet
7  AiBehaviorId.Revision von Hand bumpen
8  Journaleintrag MIT Abschnitt „Schlechter" — leer lassen ist verdächtig
9  eine Changelog-Zeile unter [Unreleased]
```

> [!IMPORTANT]
> **Aus-Stellung ist Pflicht, nicht Stil.** Eine Coderegel steckt im Binary und
> erreicht im Selbstspiel **beide** KIs; zwei stärkere Armeen sehen dort genauso
> aus wie eine schlechtere KI. Genau daran sind V002 und V003 beurteilt worden,
> und ob ihre Rückweisung trägt, ist deshalb offen (M001). Ein `int` und ein
> `if` beantworten die Frage, die gestellt war.

## 7 · Woran man merkt, dass es wirkt

Laborzahlen sind Diagnose. Ob ein Punkt im Spiel etwas taugt, entscheidet eine
gespielte Partie — die Fragen dazu stehen in
[`PLAYTEST-CHECKLIST.md`](PLAYTEST-CHECKLIST.md). Fünf, die man ohne eine Zahl
zu lesen beantworten kann:

1. Kommt die Armee als **Welle** oder als Kette? (steht seit `r3`)
2. **Drehen angeschlagene Einheiten ab?** (steht seit `r4`)
3. Lässt sie ihr **HQ zusammenschiessen**, während Einheiten daneben warten? (Punkt 1)
4. Sind die Wellen **verschieden gross**, oder immer gleich? (Punkt 5)
5. Läuft der Angriff zweimal hintereinander **denselben Weg**? (Punkt 4, 8a, 8b)

**Und die Regel über allen:** Was nicht im laufenden Spiel gesehen wurde, steht
als ungesehen im PR-Text. Der Abschnitt „Im laufenden Spiel gesehen" bleibt
leer und als leer erkennbar, bis ein Mensch gespielt hat.
