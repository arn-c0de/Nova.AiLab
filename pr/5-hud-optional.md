> **Stufe 5 von 5 des Einheitenstrangs — optional, bitte ohne schlechtes
> Gewissen ablehnen.**
> Reihenfolge: 1 Datenschicht → 2 Zielwahl → 3 Wellen → 4 Rückzug → **5 HUD**.
> Er hängt am Ende des Stapels: **wird er abgelehnt, ändert das an 1–4 nichts.**

> [!IMPORTANT]
> **Dieser PR fasst Dateien ausserhalb meiner Schreibhoheit an.** Das ist keine
> Unachtsamkeit, sondern der Grund, warum er getrennt und optional ist:
>
> | Datei | Wem sie gehört |
> |---|---|
> | `Gameplay/Match/MatchRunner.cs` | **Netzstrang** — eine Zeile im Tick-Akkumulator |
> | `Gameplay/Match/FogRevealDebug.cs`, `MatchSpeedDebug.cs` | neu, in fremdem Ordner |
> | `Gameplay/Match/UnitViewManager.cs` | nicht zugeteilt |
> | `Presentation/UI/FogOfWarOverlayView.cs`, `HealthBarHud.cs`, `MinimapHud.cs` | nicht zugeteilt |
> | `Presentation/UI/DebugHud.cs` | mir zugeteilt (Tabelle 1.2.0) |
>
> Nimm oder lass — beides ist in Ordnung. Wenn nur ein Teil interessiert: die
> **Simulations-Kennung** ist der einzige, der nichts Fremdes anfasst, und
> steckt in einem eigenen Commit.

## Warum

Ein Gegner, den man nur durch den eigenen Sichtradius beobachten kann, lässt sich
nicht beurteilen: Anmarschweg, Sammeln und der Moment, in dem eine angeschlagene
Einheit abdreht, passieren dort, wo niemand hinsieht. Und eine Partie entscheidet
um Tick 9.000 — fünfzehn Minuten Zusehen, mit Minuten zwischen den Stellen, die
etwas aussagen.

Das Repo verlangt eine gespielte Beobachtung als Nachweis. Diese drei Werkzeuge
machen sie überhaupt erst leistbar.

## Die drei

- **F4 — Karte aufdecken.** `FogOfWarSystem` berechnet und schreibt dieselben
  Team-Sichten fest, die KI liest weiter ihre eigene über `GetVisibleEntities`.
  Nur vier Präsentationsverbraucher zeichnen aus dem Entity-Store statt aus der
  Sicht.
- **F5 — Zeitraffer 1x → 2x → 4x → 10x.** Skaliert ausschliesslich die
  Wall-Clock-Zeit, die `MatchRunner` seinem Fixed-Tick-Akkumulator gibt. Die
  Simulation läuft weiterhin mit 10 Hz, Tick für Tick, in derselben Reihenfolge:
  **eine bei 10x zugesehene Partie endet auf demselben Tick mit demselben
  Zustands-Hash.** In einer Relay-Partie wirkungslos — zwei Peers mit
  verschiedenen Raten warten nur in der Lockstep-Barriere aufeinander.
- **Kennung der Simulation** im F3-Panel: Hash der Definitionstabelle plus die
  fünf Schemaversionen. Genau die Werte, an denen ungleiche Testbuilds
  auseinandergehen — ablesbar vom Screenshot statt erfragt. Einmal berechnet,
  nicht je Frame.

**Keines der drei rechnet etwas anders.** Das ist die Bedingung, unter der eine
Debug-Ansicht überhaupt durch den Nebel oder an die Uhr darf: eine Beobachtung
ist wertlos, wenn das Beobachten das Beobachtete ändert.

Beide Zustände stehen sichtbar in der Statuszeile (`FOG REVEALED`, `4x SPEED`),
weil ein Urteil über eine aufgedeckte oder gespulte Partie ohne dieses Etikett
nichts wert ist.

## Tests

**565/565 grün**, die vier Determinismus-Baselines **nicht angefasst**. Die
.NET-Testlane übersetzt keine MonoBehaviour — die drei Werkzeuge sind im Editor
gelaufen, nicht von der Suite abgedeckt.

## Im laufenden Spiel gesehen

Der Editor kompiliert und startet mit diesen Änderungen; F3, F4 und F5 tun, was
oben steht. **Eine Partie zur Beurteilung des KI-Verhaltens ist damit noch nicht
gespielt** — das steht in den Stufen 3 und 4 offen und wird dort auch nicht
behauptet.
