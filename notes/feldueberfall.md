# Feldüberfall — das Laborszenario zu Issue #101

**Gebaut am:** 2026-08-21 · **Modus:** `./lab.sh` kennt es nicht, es läuft eigenständig:
`dotnet run --project Nova.AiLab -c Release -- raid [--raid-delay n] [--field start|expansion]`
**Gemessener Stand:** `Project_Nova` auf `feat/movement-engaged-spacing` (`6232ce1`), Verhalten `r12`

## Die Frage, aus der es entstand

Issue #101 („KI verteidigt weder ihre Harvester noch den Sammelplatz") stellt eine Frage,
die vor dem Bau einer Regel beantwortet sein soll: war die nächste eigene Einheit
**ausser Reichweite** (dann braucht es Bewegung) oder hatte sie einen **stehenden
Angriffsbefehl** (dann genügt eine Zielregel)?

An der kanonischen Partie ist das nicht messbar. Der Fall tritt dort kaum auf, und wenn
er auftritt, ist die Sache schon entschieden: im Lauf, der ihn erzeugt hat
(`hq-weight-1` gegen `ms1-canonical`), lebte zum Zeitpunkt des Beschusses **eine
einzige** eigene Kampfeinheit. Eine Stichprobe von eins, aus einer Niederlage, ist keine
Antwort.

Also wird die Lage gebaut statt abgewartet, und der Abstand ist ein Regler statt eines
Zufalls: ein Angreifer am Harvester, N eigene Kampfeinheiten in einstellbarer Entfernung,
und die Messung fährt die Entfernung durch.

## Aufbau

- **Slot 1** spielt die KI (`ms1-canonical`), kanonische Eröffnung: HQ, Feld, Bauarbeiter.
- **Slot 0** ist *scripted* — ein Kampfpanzer, ein `AttackTarget`-Befehl, sonst nichts.
  Damit gehört jede Reaktion im Ergebnis der KI und nicht dem Gegenspieler.
- Der Harvester steht auf dem Feld und bekommt einen echten Erntebefehl, damit
  „erntet unter Beschuss weiter" eine Beobachtung ist und keine Annahme über eine
  Einheit, die ohnehin nur herumsteht.
- **`--field start`** überfällt das Startfeld, **`--field expansion`** ein registriertes
  Expansionsfeld auf (100,84).
- **`--raid-delay n`** lässt die KI erst n Ticks in Ruhe. Ohne Vorlauf ist ihre Armee
  untätig und der Angreifer das Einzige auf der Karte, worauf sich schiessen lohnt —
  dann läuft die Welle hin, und das Ergebnis hat mit Verteidigung nichts zu tun.

**Eine Falle, in die die erste Fassung gelaufen ist:** die Reichweitenprüfung war eine
Chebyshev-Zellzahl neben der Simulation statt der Prüfung, die `CombatSystem` selbst
macht. Fünf Zellen quer und fünf hoch sind sieben Zellen Abstand; ein Gewehr mit
Reichweite 6 feuert dann nicht. Die erste Fassung las das als „in Reichweite und
trotzdem still" und hätte damit einen Defekt erfunden. Jetzt steht `CombatSystem.IsInRange`
als bewusste Kopie im Szenario, einzeilig und mit Quellenangabe daneben.

## Was herauskommt

### Ohne Vorlauf — untätige Armee

Sie reagiert in **jeder** Zeile, bis hinunter zu 35 Zellen Abstand. Das ist aber nicht die
Verteidigungsregel, sondern das Angriffsziel: der Angreifer ist der einzige sichtbare
Gegner, also marschiert die Welle hin. `DefendHome` fällt am Expansionsfeld nie.

Und selbst dort, wo etwas kommt, kommt es zu spät: am Startfeld mit vier Zellen Abstand
stirbt der Harvester bei **T402**, das erste Rückfeuer fällt bei **T660**.

### Mit 1500 Ticks Vorlauf — die Armee ist unterwegs

| Feld | Abstand gespawnt | Abstand bei Überfall | Angreifer sichtbar | HomeThreatened | committed | DefendHome | Rückfeuer |
|---|---|---|---|---|---|---|---|
| Start | 2 | 4 | 1200/1200 | ja | 10 | T1520 | T1521 |
| Start | 4 | 5 | 1200/1200 | ja | 11 | T1520 | **nie** |
| Start | ≥ 6 | 110 | 1200/1200 | **ja** | **12** | **nie** | **nie** |
| Expansion | beliebig | 88 | 50/1200 | **nein** | 12 | nie | nie |

Drei Befunde, jeder für sich prüfbar:

1. **Am Startfeld liegt der Überfall im Heimradius.** Feld (119,119), HQ-Mitte (121,121),
   `DefendHomeCells` = 10 — Abstand 2. Die Begründung im Issue („weit ausserhalb von 10
   Zellen um das HQ") trifft auf das Startfeld nicht zu.
2. **Und trotzdem schweigt die Regel, sobald die Armee marschiert.** `HomeThreatened`
   liegt 1200 Ticks lang an, der Angreifer ist 1200 Ticks lang sichtbar, zwölf Einheiten
   sind in einer Welle gebunden — und `DefendHome` fällt kein einziges Mal. Das ist kein
   Fehler, sondern die zweite Bedingung in `ResolveGoal`:
   `if (posture.HomeThreatened && !committed)`. Wer angreift, verteidigt nicht.
   **Das ist der dritte Fall, den das Issue nicht nennt** — weder ausser Reichweite noch
   stehender Befehl, sondern eine Regel, die absichtlich still ist.
3. **Am Expansionsfeld greift der Anker gar nicht.** 37 Zellen vom HQ, `HomeThreatened`
   bleibt über den ganzen Lauf 0. Hier — und nur hier — stimmt die Diagnose des Issues
   vollständig. Dieser Fall existiert erst seit den endlichen Feldern (#80/#97).

## Was das Szenario nicht sagt

Es ist Diagnose. Es sagt, was die Simulation für eine Lage rechnet, die jemand gebaut hat;
es sagt nicht, dass diese Lage in einer gespielten Partie vorkommt, und es ersetzt nicht,
die Sache im laufenden Spiel gesehen zu haben.

Zwei Einschränkungen dazu:

- Die sechs Wachposten sind Legion-Infanterie (55 HP, 8 Schaden) gegen einen Kampfpanzer
  (1100 HP). Auch die Zeile mit Rückfeuer rettet den Harvester nicht. Ob eine Reaktion
  *reicht*, misst dieses Szenario nicht — nur, ob überhaupt eine kommt.
- Am Expansionsfeld ist der Angreifer nur 50 von 1200 Ticks sichtbar. Dass dort nichts
  passiert, ist damit **auch** eine Sichtfrage und nicht allein eine Regelfrage.

## Nebenbefund: die Eröffnung des Labors ist zwei Zellen aus dem Tritt

`CanonicalOpening` setzt Slot 1 auf HQ-Ursprung (120,120) mit Feld (119,119) und beruft
sich dabei auf `MatchBootstrap`. Das Spiel steht inzwischen woanders: D-107 hat den
Gegner-HQ auf (118,118) gelegt, das Feld liegt auf (117,117)
(`MatchBootstrap.cs:163` und `:1000`). Die *relative* Geometrie ist dieselbe — zwei Zellen
zwischen HQ und Feld, hier wie dort —, die absoluten Zellen sind es nicht.

Für die Aussagen oben ändert das nichts. Für einen Vergleich mit einem Spiel-Log schon.
Gehört nachgezogen, ist aber Laborarbeit und kein Beitrag ins Spiel.

## Tests

`Nova.AiLab.Tests/RaidScenarioTests.cs`, fünf Stück: Reproduzierbarkeit, kein abgelehnter
Befehl, die Geometrie beider Felder, und die beiden Lesarten selbst. Die letzten beiden
**halten einen Defekt fest** — sie werden rot, sobald die Verteidigungsregel sich ändert,
und genau das ist ihr Zweck.
