# PR-Serie Einheitenstrang — vier Stufen, gestapelt

Die vier Texte hier sind zum Einfügen in die PR-Beschreibung gedacht. Sie liegen
im Labor und nicht im Spiel-Repo, weil sie Arbeitsmaterial sind und kein
Spielcode.

## Warum vier statt einem

Der Strang trägt 2.095 Zeilen über vier logische Änderungen. Als ein PR wäre er
gegen zwei Regeln zugleich: **eine Verhaltensänderung pro PR**, und die
ausdrückliche Bitte des Maintainers um einen kleinen ersten. Der Schnitt liegt
nicht im Zeilenzähler, sondern an den Stellen, an denen sich der
**Verhaltensbezeichner** ändert — das ist genau die Grenze, an der ein Reviewer
etwas anderes prüfen muss.

| # | Branch | Verhalten | Zeilen | Tests |
|---|---|---|---:|---|
| 1 | `refactor/ai-profile-data-layer` | **unverändert** | +512 / −32 | 561/561 |
| 2 | `feat/ai-score-targeting` | `r1` → `r2` | +417 / −30 | 562/562 |
| 3 | `feat/ai-attack-waves` | `r2` → `r3` | +882 / −84 | 564/564 |
| 4 | `feat/ai-retreat` | `r3` → `r4` | +351 / −22 | 565/565 |

**Gestapelt, nicht parallel:** jeder Branch baut auf dem vorigen auf. Wird 1
gemergt, rebased sich 2 von selbst. Jeder Stand ist für sich grün und für sich
lauffähig.

## Reihenfolge und Abhängigkeit

- **1 vor 2**, weil die Zielgewichte Profilfelder sind.
- **2 vor 3**, weil der Bezeichner in 2 entsteht und 3 ihn bumpt.
- **3 vor 4**, weil der Rückzug ein Ziel braucht, zu dem er zurückläuft — den
  Sammelpunkt aus 3.

## Was danach kam — und wo die Reihenfolge steht

Der Einheitenstrang oben ist gemergt, das Stärketor auch. **Die verbindliche
Reihenfolge aller weiteren PRs steht in [`../ROADMAP.md`](../ROADMAP.md)** —
hier stand vorher eine eigene Nummerierung, die zweimal die 8 vergab und mit
den Tabellen in `NEXT-STEPS.md` und `KAMPFSTAERKE.md` nicht übereinstimmte.

| Branch | Verhalten | Stand |
|---|---|---|
| `feat/ai-strength-wave-gate-de` | `r5` → `r6` — die Welle **kann** auf Kampfstärke messen statt auf Kopfzahl. Bei Obergrenze 12 nachweislich verhaltensneutral: eine Entkopplung, keine Verbesserung | ✅ **gemergt als #72**, PR-Text: [`6-staerketor.md`](6-staerketor.md) |
| `feat/ai-abort-on-threat` | Die Welle bricht das Sammeln ab, wenn die eigene Basis angegriffen wird. Grund ist eine Beobachtung am Player (B003), nicht ein Plan-Punkt | **der nächste PR** — [ROADMAP](../ROADMAP.md) 1, Plan: [`../VERTEIDIGUNG.md`](../VERTEIDIGUNG.md) |
| `fix/ai-army-cap` | Armeeobergrenze. Sie liegt in `MatchRunner`, also in fremdem Terrain — **eine Rückfrage, kein PR von uns** | [ROADMAP §4](../ROADMAP.md) · Journal V007. Ohne sie bleibt `r6` im Spiel wirkungslos |
| `feat/ai-reinforce-doctrine` | nachschicken, solange die Welle intakt ist; sammeln, wenn sie gebrochen ist | [ROADMAP](../ROADMAP.md) 3 |
| `feat/ai-vehicle-buildout` | Bauliste bis Fahrzeugwerk, Kaufregel nach Stärke je AE | [ROADMAP](../ROADMAP.md) 6 |

**Ziel-Repo ist jetzt [`VibecodingGermany/HashKrieg`](https://github.com/VibecodingGermany/HashKrieg)**
— dasselbe Repository unter neuem Namen (vorher `Project_Nova`). Der Fork heisst
weiterhin `arn-c0de/Project_Nova`, der Weg bleibt Fork → PR.

## Was in allen vier gilt

- **Keine der vier Determinismus-Baselines ist angefasst.** Eine reine
  KI-Änderung macht sie ohnehin nicht rot (sie fahren kein KI-System), die
  Trennungsregel gilt trotzdem.
- **Kein Float, kein `System.Random`, keine Wanduhr, keine Hash-Iteration** unter
  `Simulation/` und `AI*`.
- **`ICommandTransport` und `ICommandSubmissionReadiness` werden benutzt, nicht
  geändert.** Kein neues System registriert, `MatchRunner` nicht angefasst —
  alles Reaktionsverhalten liegt in `SkirmishAiSystem`, das zwischen Combat und
  Victory bereits eingeordnet ist.
- **Im laufenden Spiel ungesehen.** Steht in jedem PR-Text, nicht als Formalie.
