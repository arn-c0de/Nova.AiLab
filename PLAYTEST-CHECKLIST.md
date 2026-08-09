# Was in der gespielten Partie zu prüfen ist — KI-Verhalten `r4.779A1B5B`

**Wofür das hier ist.** Das Labor misst Stärke, Tempo und Rhythmus. Ob sich eine
Partie **gut anfühlt**, misst es nicht und soll es nicht (Entscheidung 11). Das
entscheidet die gespielte Partie — und die ist im Sinne dieses Repos der einzige
Nachweis, den es gibt. Alles unten ist mit blossem Auge zu sehen; keine Zahl,
kein Overlay, keine Logdatei nötig.

> [!IMPORTANT]
> **Kein Haken hier ist gesetzt.** Der Linux-Build steht aus, gespielt wurde
> zuletzt der Stand `r2.A037B84D` ([Journal B001](reports/behavior-log.md)).
> Alles, was seither dazukam — Wellen (V004) und Rückzug (V005) —, ist
> **ungesehen**. Genau so gehört es in den PR-Text.

**Wie ausfüllen.** Beobachtung wörtlich notieren, dann einordnen. Ein „ja, wie
erwartet" ist weniger wert als ein „nein, und zwar so". Ergebnisse wandern als
`B`-Eintrag ins [Verhaltensjournal](reports/behavior-log.md) — `B` wie
Beobachtung, nicht `V` wie Verhaltensmessung.

---

## 0 · Vor dem Start

- [ ] Bezeichner im F3-HUD steht auf **`r4.779A1B5B`**. Steht dort etwas
      anderes, ist der Build älter als diese Liste und die Punkte unten
      beschreiben eine andere KI.
- [ ] Zwei Partien einplanen, nicht eine. Die halbe Liste fragt „passiert das
      **zweimal** gleich".
- [ ] Mitschreiben, während gespielt wird. Nach der Partie erinnert man das
      Ergebnis, nicht das Gefühl.

---

## 1 · Rhythmus — kommt die Armee als Welle oder als Kette?

*Die sichtbarste Änderung. Die KI greift erst mit voller Armee an und sammelt
Nachschub an einem Punkt zwischen ihrer Basis und dir.*

- [ ] **Kommt der erste Angriff als geschlossene Gruppe?** Oder tröpfeln
      einzelne Soldaten nacheinander an?
- [ ] **Gibt es zwischen zwei Angriffen eine Ruhephase?** Aufbau, Angriff,
      Ruhe, Aufbau — oder ein Dauerrinnsal ohne Struktur?
- [ ] **Kündigt sich die zweite Welle an?** Sammelt sichtbar etwas, bevor es
      losläuft? (Das ist ein Feature, kein Fehler: eine lesbare KI schlägt eine
      kluge.)
- [ ] **Wartet sie zu lange?** Steht die Armee herum, während du in Ruhe
      ausbaust — und wird das langweilig statt spannend?
- [ ] **Fühlt sich die Pause zwischen zwei Wellen wie Absicht an** oder wie ein
      Aussetzer?

## 2 · Der frühe Konter — die bekannte Schwachstelle

*Die KI greift erst mit zwölf Einheiten an. Wer früh kommt, trifft eine
wartende Armee. Das ist im Labor nicht messbar, weil dort beide Seiten gleich
eröffnen.*

- [ ] **Mit drei bis vier Einheiten früh angreifen.** Was tut sie? Verteidigt
      sie sich überhaupt, oder steht sie und lässt sich abräumen?
- [ ] **Verteidigt sie ihre Harvester?** Beschiesse sie und zähle mit, wie
      lange nichts passiert.
- [ ] **Ist der frühe Angriff eine sichere Gewinnstrategie?** Wenn ja, ist die
      Wellenregel in dieser Form zu starr und `DefendBase` wird dringend.
- [ ] **Wirkt sie wehrlos oder gelassen?** Der Unterschied ist Spielgefühl:
      „sie ignoriert mich" liest sich anders als „sie kann nicht".

## 3 · Rückzug — merkt sie, dass sie beschossen wird?

*Angeschlagene Einheiten (unter 60 % Leben) drehen ab und laufen nach Hause,
wenn ein bewaffneter Feind in der Nähe ist.*

- [ ] **Drehen angeschlagene Einheiten sichtbar ab?** Das ist die erste
      Reaktion überhaupt, die diese KI zeigt — man muss sie sehen können.
- [ ] **Sieht das nach Absicht aus oder nach Fehler?** „Der zieht sich zurück"
      gegen „der bleibt hängen und zappelt".
- [ ] **Schiesst die abdrehende Einheit weiter zurück**, während sie läuft?
      (Sie soll — das übernimmt die Automatik.)
- [ ] **Ködern:** Mit einer einzelnen billigen Einheit die Welle beschiessen.
      Lässt sich die halbe Armee nach Hause schicken? Das wäre der teure Fall.
- [ ] **Kommt die zurückgezogene Einheit wieder?** Sie soll mit der nächsten
      Welle wieder losziehen — verwundet, denn heilen kann sie nicht.
- [ ] **Sammeln sich Verwundete zu Hause an**, bis nichts mehr angreift?

## 4 · Vorhersagbarkeit — kann man sich hinstellen?

*Unverändert seit `r2` und bekannt: die KI läuft aufs Headquarter, auf der
geraden Linie. Die Punkte prüfen, ob die Wellen das verschlimmert haben.*

- [ ] **Läuft der Angriff zweimal hintereinander denselben Weg?**
- [ ] **Kann man sich an eine Stelle stellen und dort alles abräumen?** Mit
      Wellen kommen jetzt zwölf statt einer — ist die Stelle immer noch sicher?
- [ ] **Greift sie je etwas anderes an als das HQ?** Harvester, Refinery,
      Kaserne — oder immer nur das eine Ziel?
- [ ] **Wird die zweite Partie langweilig,** weil man weiss, was kommt?

## 5 · Nahkampf und Abstand — die alten Punkte

- [ ] **Läuft Artillerie bis auf Tuchfühlung heran** und stirbt an Infanterie?
- [ ] **Läuft die Armee ohne Zögern in die Reichweite** deiner Fernkämpfer?
- [ ] **Blockieren sich ihre Einheiten gegenseitig** an Engstellen oder an
      Gebäudeecken?
- [ ] **Bleibt jemand hängen?** Eine Einheit, die im Nichts stehenbleibt, ist
      der sichtbarste Defekt überhaupt.

## 6 · Fairness — sieht es nach Schummeln aus?

*Die KI nimmt keine Abkürzung: gleicher Befehlsweg, gleiche Startmittel, nur
die committed Team-Sicht. Der Spieler sieht das nur, wenn nichts dagegen
spricht.*

- [ ] **Reagiert sie je auf etwas, das sie nicht sehen kann?** Ein einziger
      solcher Moment zerstört den Eindruck.
- [ ] **Wirkt ihre Handlungsdichte menschlich?** Gemessen: rund 15 Aktionen pro
      Minute, weit unter menschlichem RTS-Niveau. Wirkt das träge oder ruhig?
- [ ] **Sucht sie sichtbar?** Oder weiss sie sofort, wo alles steht?

## 7 · Die drei Fragen, wenn nur fünf Minuten Zeit sind

Wenn von dieser Liste nur drei Zeilen beantwortet werden können, dann diese:

1. **Kommt die Armee als Welle oder als Kette?**
2. **Drehen angeschlagene Einheiten ab?**
3. **Läuft der Angriff zweimal hintereinander denselben Weg aufs HQ?**

Alle drei sieht man, ohne eine Zahl zu lesen. Die ersten beiden prüfen, was
seit `r2` neu ist; die dritte prüft, was bekannt kaputt ist.

---

## Was danach passiert

- Beobachtungen wörtlich in einen **`B`-Eintrag** im
  [Verhaltensjournal](reports/behavior-log.md), mit der KI-Revision in der
  Kopfzeile.
- **Ein Fall, in dem die Reaktion falsch war, gehört ausdrücklich dazu.** Ein
  Bericht ohne Gegenbeispiel ist verdächtig, nicht sauber — dieselbe Regel wie
  beim Abschnitt „Schlechter".
- Was hier bestätigt oder widerlegt wurde, verschiebt die Reihenfolge in
  [`NEXT-STEPS.md`](NEXT-STEPS.md). Eine gespielte Beobachtung sticht jede
  Laborzahl.
- Erst danach darf in einem PR-Text „im laufenden Spiel gesehen" stehen — und
  auch dann nur für genau das, was tatsächlich gesehen wurde.
