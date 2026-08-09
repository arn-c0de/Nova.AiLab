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
