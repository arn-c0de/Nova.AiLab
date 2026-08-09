# Wer hat geschossen — Herleitung heute, Vorschlag fürs Spiel

**Notiert am:** 2026-08-09 · **Status:** Herleitung gebaut, Spiel-Hook nur aufgeschrieben

Das Labor schreibt in `events.ndjson` bei jedem `damage` und `death` ein Feld
`by`. Es ist **hergeleitet**, nicht beobachtet, und das steht an jeder Stelle
dran, an der es auftaucht — im JSON als `bySure`, in der Seite kursiv als
*derived*. Diese Notiz hält fest, **wie** hergeleitet wird, **wo es versagt**,
und **wie ein sauberer Hook im Spiel aussähe**, falls wir ihn später bauen.

Kein Code in `Project_Nova` ist dafür geändert worden.

## 1 · Warum überhaupt hergeleitet wird

Die Simulation meldet keinen Verursacher. `CombatSystem` bringt den Treffer an
und geht weiter:

```csharp
// Assets/_Project/Scripts/Simulation/Combat/CombatSystem.cs:246-252
int damage = DamageMatrix.Resolve(
    weapon.AttackDamage, weapon.DamageType,
    WeaponProfiles.GetArmorClass(_factions.GetSlotFaction(target.PlayerId), target.Role));

target.CurrentHealth -= damage;
attacker.WeaponCooldownTicks = weapon.AttackCooldownTicks;
```

Ein Beobachter, der nach `StepTick()` liest, sieht danach nur: *diese Einheit
hat 26 Lebenspunkte weniger als vorher.* Der Schütze steht nicht im Zustand.

Für „KI, die reagiert statt nur baut" ist genau das die interessante Zeile.
Ohne Verursacher lässt sich nicht sagen, ob eine Einheit von einem Panzer, von
drei Infanteristen oder vom Verteidigungsturm zerlegt wurde — und damit auch
nicht, ob ein Rückzugsverhalten auf die richtige Bedrohung reagiert hat.

## 2 · Wie die Herleitung rechnet

Sie steht in `Metrics/DebugEventLog.cs`, Methode `AttributeAttackers`, und
läuft nur in Ticks, in denen überhaupt Schaden aufgetreten ist.

**Strenger Pfad.** Kandidat ist eine gegnerische Einheit, für die beides gilt:

1. Ihr Waffencooldown steht auf dem **Maximum ihres eigenen Profils**
   (`WeaponProfiles.Get(faction, role).AttackCooldownTicks`). Genau darauf setzt
   ihn `CombatSystem` in derselben Anweisung, die den Schaden anbringt — wer im
   beobachteten Tick geschossen hat, steht auf Maximum, und sonst niemand.
   Phase 1 desselben Ticks zählt Cooldowns herunter, bevor gefeuert wird
   (`CombatSystem.cs:156-164`), also kann kein Nachbar zufällig auf demselben
   Wert stehen, ohne selbst geschossen zu haben.
2. Sie nennt das Opfer als Angriffsziel — **jetzt oder im Tick davor**.

Der Tick davor ist nicht Bequemlichkeit, sondern nötig: bei einem Kill räumt
`KillUnit` in demselben Tick jeden Angriffsbefehl auf die tote ID weg
(`CombatSystem.cs:311-322`), damit niemand auf eine Leiche weiterschiesst. Der
Mörder hat also im Moment der Beobachtung **kein** Ziel mehr. Deshalb hält das
Ereignisprotokoll die Angriffsziele des Vortticks in einem eigenen Array.

**Weiter Pfad**, nur wenn der strenge niemanden nennt: jede gegnerische
Einheit mit vollem Cooldown, deren Waffenreichweite die letzte bekannte
Position des Opfers erreicht. Das fängt den Schuss einer Einheit, die im selben
Tick über die D-087-Selbstakquise ein Ziel bekommen **und** gefeuert hat. Er
wird nie als sicher markiert: der Reichweitentest hat bei einer despawnten
Einheit keinen Zielradius mehr und rechnet mit einer Zelle Zuschlag.

**Mehrdeutigkeit bleibt sichtbar.** Zwei Kandidaten werden als zwei
geschrieben, nie auf den ersten eingedampft. `bySure` ist genau dann `1`, wenn
der strenge Pfad **einen einzigen** Kandidaten nannte. Nur solche Treffer
gehen in `damageDealtDerived` und `killsDerived` in `units.json` ein — eine
Summe, die Vielleichts enthält, liest sich wie eine Messung und ist keine.

**Gemessen** an einem Lauf über 6000 Ticks (Seed `0xA17E57DE57`, zwei Slots):
811 Schadens- und Todesereignisse, davon **809 mit mindestens einem Kandidaten**
und **687 eindeutig** — 84 %. Die fehlenden 16 % sind fast durchweg der erste
Fall aus Abschnitt 3: mehrere Schützen auf dasselbe Opfer im selben Tick, und
der Anteil steigt mit der Gefechtsgrösse. Bei 3000 Ticks lag er bei 92 %.

`DebugEventTests` hält fest, dass mehr als die Hälfte einen Kandidaten bekommt.
Wird es weniger, passt die Begründung oben nicht mehr zum Kampfsystem — dann
gehört die Herleitung überprüft, nicht der Test gelockert.

## 3 · Wo sie versagt

- **Zwei Schützen, ein Tick, ein Opfer.** Beide stehen auf vollem Cooldown und
  nennen dasselbe Ziel. `by` hat zwei Einträge, `bySure` ist 0, und der Schaden
  landet bei keinem von beiden im Konto.
- **Der Schütze stirbt im selben Tick.** Dann ist er weg, bevor irgendjemand
  ihn zählen konnte — weder Pfad findet ihn.
- **Selbstakquise plus Schuss im selben Tick** trifft nur der weite Pfad, also
  immer unsicher.
- **Waffen mit `AttackCooldownTicks == 1`.** Der Cooldown steht nach dem
  Herunterzählen des Folgetticks wieder auf 0; innerhalb eines Ticks bleibt die
  Unterscheidung gültig, aber der Sicherheitsabstand ist weg.
- **Jede künftige Schadensart ohne Schützen** — Flächenschaden, Rückschlag,
  Gift, Schaden aus einem anderen System als `CombatSystem` — ist für diese
  Herleitung unsichtbar und würde stillschweigend dem nächstbesten Schützen
  zugeschlagen. Das ist der Punkt, an dem die Herleitung von „ungenau" zu
  „falsch" kippt.

Der letzte Punkt ist der Grund, warum diese Notiz existiert: Die Herleitung
ist an das heutige Kampfmodell gebunden. Sie altert mit ihm, und sie sagt es
nicht von selbst.

## 4 · Wie ein Hook im Spiel aussähe

Ein optionaler Beobachter an der Stelle, an der Schaden angebracht wird:

```csharp
// Simulation/Combat/ICombatObserver.cs
public interface ICombatObserver
{
    void OnDamage(EntityId attacker, EntityId victim, int amount, Tick tick);
    void OnKill(EntityId attacker, EntityId victim, Tick tick);
}
```

`CombatSystem` bekommt ein Feld `ICombatObserver _observer` (Vorgabe `null`)
und ruft es in den zwei Zeilen auf, die es ohnehin gibt:

```csharp
target.CurrentHealth -= damage;
attacker.WeaponCooldownTicks = weapon.AttackCooldownTicks;
_observer?.OnDamage(attacker.Id, targetId, damage, tick);

if (target.CurrentHealth <= 0)
{
    _observer?.OnKill(attacker.Id, targetId, tick);
    KillUnit(targetId);
}
```

**Bedingungen, unter denen das baseline-neutral bleibt** — und nur dann ist es
diesen PR wert:

- Der Beobachter hält **keinen Simulationszustand**. Er steht nicht im
  Zustands-Hash, nicht im Snapshot, nicht in `EntityManager.WriteState`.
- Er **zieht keine Zufallszahl** und liest keine Uhr.
- Er **ändert die Reihenfolge nicht**: der Aufruf steht hinter der
  Schadensanwendung, nicht davor, und verzweigt nichts.
- Er ist **nicht gesetzt**, wenn niemand ihn setzt — `MatchRunner` bindet
  keinen. Dann ist der einzige Unterschied ein Nullvergleich pro Treffer.

Unter diesen Bedingungen ändert sich kein Simulationsverhalten, und keine der
vier Baseline-Dateien wird rot. Das ist eine Behauptung, kein Beweis: der PR
muss `dotnet test tools/Nova.SimRunner.Tests/…` grün zeigen, **einschliesslich**
`Determinism10000Tests` und `SnapshotGoldenBytesTests`.

Das Labor bände den Hook dann in `MultiSlotAiHost` und `DebugEventLog` würde
`by` **beobachtet** statt hergeleitet schreiben — mit demselben Feldnamen, aber
`bySure` fiele weg, weil es nichts mehr zu bezweifeln gäbe.

## 5 · Scope-Lage

`Assets/_Project/Scripts/Simulation/Combat/` gehört uns (`CLAUDE.md` §1). Das
macht den Hook **erlaubt**, nicht **beschlossen**:

- Es bleibt ein PR nach `VibecodingGermany/Project_Nova` über den Fork, gemergt
  von der Gegenseite.
- Der PR-Text beschreibt die **gespielte Beobachtung**. Ein Hook, der nichts
  ändert, hat keine sichtbare Wirkung im Spiel — genau das gehört hingeschrieben,
  statt eine zu erfinden.
- Die Baseline-Regel `CLAUDE.md` §3 gilt unverändert. Falls wider Erwarten eine
  Baseline rot wird, ist das ein **Befund** und keine Zeile zum Nachziehen: dann
  ist der Hook nicht der beschriebene reine Beobachter.
- `ICommandTransport`, Match-Fingerprint und Tick-Reihenfolge werden nicht
  berührt.

Solange dieser PR nicht existiert, bleibt es bei der Herleitung aus Abschnitt 2
— gekennzeichnet, mit sichtbarer Mehrdeutigkeit, und mit dieser Notiz daneben.
