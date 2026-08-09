#!/usr/bin/env python3
"""Rendert einen Laborlauf nach Markdown — dieselben Zahlen wie `dashboard.html`,
nur in einer Form, die GitHub ohne Download anzeigt.

Zwei Ausgaben entstehen hier:

* `report_markdown(record)` — ein vollstaendiger Lauf (`latest.md`, `runs/<id>.md`)
* `index_markdown(summaries)` — die Gesamtuebersicht ueber alle archivierten Laeufe

Die Kurven sind Mermaid-Bloecke: GitHub rendert sie, jeder andere Betrachter
sieht den Zahlenblock im Klartext. Beides ist lesbar, keines braucht einen
Server, kein Bild wird erzeugt, kein Netzzugriff findet statt.

Was hier NICHT entsteht: eine Gesamtnote, eine Rangfolge, eine Sortierung nach
Guete. Die Zeilenreihenfolge folgt der Kandidatenliste bzw. dem Alphabet, nie
einem Wert (Entscheidung 11 — eine einzelne Zahl belohnt zuverlaessig das
Falsche). `assert_no_ranking()` haelt das maschinell fest.

Kommentare hier bleiben ASCII wie in `build_dashboard.py`; der *ausgegebene*
Text ist deutsch geschrieben wie das Dashboard, mit Umlauten und ss statt ß.
"""

BANNER = (
    '> [!IMPORTANT]\n'
    '> **DIAGNOSE, kein Nachweis.** Nichts in diesem Bericht wurde im laufenden Spiel gesehen.\n'
    '> Alle Zahlen stammen aus headless-Läufen derselben Quelldateien, die Unity lädt — das\n'
    '> macht sie vergleichbar, nicht wahr. Es gibt bewusst **keine Rangfolge**: die Werte stehen\n'
    '> nebeneinander, die Auswahl trifft ein Mensch.'
)

REPRO = """```bash
export DOTNET_ROOT="$PWD/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project tools/Nova.AiLab -c Release -- match --trace-every 50 --hash-every 500 --view-every 25 --fog --out tools/Nova.AiLab/out/match
dotnet run --project tools/Nova.AiLab -c Release -- duel     --out tools/Nova.AiLab/out/duel
dotnet run --project tools/Nova.AiLab -c Release -- movement --out tools/Nova.AiLab/out/movement
dotnet run --project tools/Nova.AiLab -c Release -- compare  --out tools/Nova.AiLab/out/compare
python3 tools/Nova.AiLab/report/build_reports.py tools/Nova.AiLab/out
```"""

# Mehr Stuetzstellen macht den Mermaid-Block breiter, nicht lesbarer. Die
# Endwerte stehen ohnehin exakt in der Kennzahlentabelle darueber.
CHART_POINTS = 48


# ── Kleinkram ────────────────────────────────────────────────────────────────

def fmt(value):
    """Tausenderpunkt wie im Dashboard. Ganzzahlen bleiben Ganzzahlen."""
    return f'{value:,}'.replace(',', '.') if isinstance(value, int) else str(value)


def row(cells):
    return '| ' + ' | '.join(str(c) for c in cells) + ' |'


def table(header, rows, align=None):
    """Eine Markdown-Tabelle. `align` ist eine Zeichenkette aus l/r je Spalte."""
    align = align or 'l' * len(header)
    rule = ['---' if a == 'l' else '--:' for a in align]
    return '\n'.join([row(header), row(rule)] + [row(r) for r in rows])


def thin(values, count=CHART_POINTS):
    """Gleichmaessig ausduennen, letzten Wert immer behalten."""
    if len(values) <= count:
        return list(values)
    step = (len(values) - 1) / (count - 1)
    picked = [values[min(len(values) - 1, round(i * step))] for i in range(count)]
    picked[-1] = values[-1]
    return picked


def axis_top(series):
    """Obere Achsengrenze: naechster runder Wert ueber dem Maximum, nie 0."""
    top = max((max(s) for s in series if s), default=0)
    if top <= 0:
        return 1
    magnitude = 10 ** (len(str(int(top))) - 1)
    return int(-(-top // magnitude) * magnitude)


def xychart(title, x_label, x_max, y_label, series):
    """Ein Mermaid-Liniendiagramm ueber die Tick-Achse."""
    lines = [
        '```mermaid',
        'xychart-beta',
        f'    title "{title}"',
        f'    x-axis "{x_label}" 0 --> {x_max}',
        f'    y-axis "{y_label}" 0 --> {axis_top(series)}',
    ]
    for values in series:
        lines.append('    line [' + ', '.join(str(v) for v in thin(values)) + ']')
    lines.append('```')
    return '\n'.join(lines)


def assert_no_ranking(markdown):
    """Kein `score`, kein `rank` in einer Tabellenzeile — dieselbe Grenze, die
    `ComparisonTests` fuer den HTML-Bericht zieht. Eine Note in einer Spalte
    waere eine Rangliste im Tabellenkleid."""
    for line in markdown.splitlines():
        if not line.startswith('|'):
            continue
        lowered = line.lower()
        for word in ('score', 'rank'):
            if word in lowered:
                raise SystemExit(f'Rangfolge im Bericht: "{word}" in Tabellenzeile — {line.strip()}')
    return markdown


# ── Ein Lauf ─────────────────────────────────────────────────────────────────

def _match_section(record):
    result = record['match']['result']
    trace = record['match']['trace']
    slots, ticks = trace['slots'], trace['ticks']
    names = [f"Slot {s['slot']} · {s['faction']}" for s in result['slots']]
    ticks_per_second = round(result['finalTick'] / result['elapsedMilliseconds'] * 1000)

    out = ['## Laufart 1 · `match` — die Partie: KI gegen KI', '',
           'Eine kanonische Partie über beide Slots, Metriken alle '
           f"{result['traceIntervalTicks']} Ticks, reine Beobachtung — ein Lauf mit und ohne "
           'Trace liefert dieselbe Hash-Kette.', '',
           table(['Kennzahl', 'Wert', 'Kontext'], [
               ['Ausgang', result['outcome'],
                f"Slot {result['winnerSlot']} · {result['slots'][result['winnerSlot']]['faction']}"
                if result['winnerSlot'] >= 0 else 'kein Sieger'],
               ['Entschieden bei Tick', fmt(result['decidedTick']),
                f"von {fmt(result['tickBudget'])} Budget"],
               ['Rechenzeit', f"{fmt(result['elapsedMilliseconds'])} ms",
                f'{fmt(ticks_per_second)} Ticks/s'],
               ['Metrikproben', fmt(result['traceSamples']),
                f"alle {result['traceIntervalTicks']} Ticks"],
               ['Hash-Kette', fmt(result['hashChainEntries']),
                f"alle {result['hashIntervalTicks']} Ticks"],
               ['Endzustands-Hash', f"`{result['finalStateHash']}`",
                f"bei Tick {fmt(result['finalTick'])}"],
           ], 'lrl'), '']

    curves = [
        ('Credits', 'credits', 'Kassenstand je Slot'),
        ('Armeegrösse', 'armySize', 'lebende Kampfeinheiten'),
        ('Verluste, kumuliert', 'unitsLost', 'verlorene Einheiten seit Tick 0'),
        ('Harvester', 'harvesters', 'Erntefahrzeuge im Feld'),
        ('Gebäude', 'buildings', 'fertiggestellt, alle Rollen'),
        ('Sichtbare Feindeinheiten', 'visibleEnemyUnits', 'in der committed Team-Sicht'),
    ]
    rows = [[label] + [fmt(slots[i][key][-1]) for i in range(len(slots))]
            for label, key, _ in curves]
    order = ' · '.join(f'{i + 1}. Linie **{name}**' for i, name in enumerate(names))
    out += [table(['Endwert je Slot'] + names, rows, 'l' + 'r' * len(slots)), '',
            f'{order}. `xychart-beta` kennt keine Legende, deshalb steht die Zuordnung hier. '
            f'x-Achse Tick 0 bis {fmt(ticks[-1])}, alle Werte ganzzahlig — kein Float verlässt '
            'die Simulation.', '']

    # Vier Kurven, nicht sechs: Gebaeude und Feindsicht sind Stufenkurven mit
    # zwei bis zehn Stufen — die Endwertetabelle darueber sagt darueber mehr als
    # ein Liniendiagramm.
    for label, key, sub in curves[:4]:
        out += [f'**{label}** — {sub}', '',
                xychart(label, 'Tick', ticks[-1], label, [slot[key] for slot in slots]), '']

    submitted = [s['intentsSubmitted'][-1] for s in slots]
    rejected = [s['intentsRejected'][-1] for s in slots]
    out += ['**Verworfene Intents:** ' + ' und '.join(
        f'{fmt(rejected[i])} von {fmt(submitted[i])} (Slot {i})' for i in range(len(slots))) +
        '. Diese Spalte ist die unterschätzte — sie zeigt, wo die KI gegen Executor-Regeln '
        'anrennt, und ist überall sonst stumm, weil `Submit()` das Verdikt nicht auswertet.', '',
        '**Der Seed ändert die Partie nicht.** Kein Simulationssystem zieht aus dem Kernel-PRNG; '
        'der Seed geht in Zustands-Hash und Snapshot, sonst nirgendwohin. Ein Sweep über 24 Seeds '
        'ist *eine* Beobachtung.', '']
    return '\n'.join(out)


def _compare_section(record):
    compare = record['compare']
    candidates = compare['candidates']
    ref = candidates[0]

    def delta(value, reference, is_ref):
        if is_ref or not reference or value == reference:
            return ''
        percent = round((value - reference) / reference * 100)
        return f" ({'+' if percent > 0 else ''}{percent} %)"

    rows = []
    for c in candidates:
        is_ref = c is ref
        rows.append([
            f"**{c['profileId']}**" + (' _Referenz_' if is_ref else ''),
            c['changes'] or '—',
            f"{c['winPercent']} %",
            f"{c['wins']}/{c['losses']}/{c['draws']}",
            fmt(c['averageDecidedTick']) + delta(c['averageDecidedTick'], ref['averageDecidedTick'], is_ref),
            fmt(c['averageCredits']) + delta(c['averageCredits'], ref['averageCredits'], is_ref),
            fmt(c['averageArmySize']) + delta(c['averageArmySize'], ref['averageArmySize'], is_ref),
            fmt(c['averageUnitsLost']) + delta(c['averageUnitsLost'], ref['averageUnitsLost'], is_ref),
            fmt(c['intentsSubmitted']),
            fmt(c['intentsRejected']),
        ])

    seeds = compare.get('seeds', [])
    return '\n'.join([
        '## Laufart 4 · `compare` — Kandidatenprofile gegen die eingefrorene Referenz', '',
        f"Jeder Kandidat spielt gegen `{ref['profileId']}`, in beiden Fraktionsrollen — das hebt "
        'die Spawnreihenfolge auf. Die Zeilenreihenfolge ist die Kandidatenliste, nicht die Güte.', '',
        table(['Kandidat', 'geändert gegen Referenz', 'Sieg %', 'S/N/U', 'Entsch. Tick',
               'Credits', 'Armee', 'verloren', 'Intents', 'verworfen'],
              rows, 'llrrrrrrrr'), '',
        '**Was hier _nicht_ steht.** Keine Spalte ist zu einer Note verrechnet und nichts ist nach '
        'Güte sortiert. Eine einzelne Zahl belohnt zuverlässig das Falsche — eine KI, die 5 % '
        'häufiger gewinnt, weil sie den Gegner mit Bauarbeitern zumüllt, ist keine bessere KI.', '',
        f"**{len(seeds)} Seed{'s' if len(seeds) != 1 else ''} je Kandidat** — die Seed-Achse ist "
        f"heute leer, also sind das {ref['matches']} Partien je Kandidat, nicht "
        f"{ref['matches']} unabhängige Stichproben.", '',
    ])


def _duel_section(record):
    duel = record['duel']
    units = duel['units']
    cell_of = {(c['a'], c['b']): c for c in duel['cells']}

    rows = []
    for a in units:
        line = [f'**{a}**']
        for b in units:
            c = cell_of.get((a, b))
            if not c:
                line.append('')
                continue
            balance = c['w'] - c['l']
            if c['nc'] == 3:
                line.append('·')
            else:
                line.append(f"{'+' if balance > 0 else ''}{balance}" +
                            ('&nbsp;⚠' if 0 < c['nc'] < 3 else ''))
        rows.append(line)

    # Eine Paarung widerspricht sich, wenn ihre beiden Richtungen nicht
    # spiegelbildlich ausgehen. Selbstpaarungen (dieselbe Rolle gegen sich)
    # bleiben draussen: ihr Spiegel IST sie selbst, dort entscheidet die
    # dokumentierte Duell-Asymmetrie, und eine Zeile, die sich mit sich selbst
    # vergleicht, kann nie einig sein.
    disagreements = sorted(
        f'{a} ↔ {b}' for (a, b), c in cell_of.items()
        if a < b and (b, a) in cell_of
        and (c['w'] - c['l']) != (cell_of[(b, a)]['l'] - cell_of[(b, a)]['w']))

    counts = duel['counts']
    out = [
        '## Laufart 2 · `duel` — die Gegentabelle, gemessen statt abgelesen', '',
        'AE-Parität statt Stückzahlparität, drei Startabstände, jede Paarung in beide Richtungen. '
        'Zeile = die zuerst gespawnte Seite. Der Wert ist ihre Bilanz über die drei Abstände '
        '(Siege − Niederlagen, Bereich −3…+3); die Abstände einzeln stehen im Dashboard.', '',
        table(['Zeile gewinnt ↓ / Spalte →'] + units, rows, 'l' + 'r' * len(units)), '',
        '`·` — in keinem Abstand Kontakt · `⚠` — kein Kontakt in mindestens einem Abstand', '',
        table(['Duelle', 'entschieden', 'ins Tickbudget', 'ohne Kontakt', 'wackelnde Parität',
               'Budget je Seite'],
              [[fmt(counts['total']), fmt(counts['decided']), fmt(counts['timeout']),
                fmt(counts['noContact']), fmt(counts['wobble']), f"{fmt(duel['budget'])} AE"]],
              'rrrrrr'), '',
        f"**{fmt(counts['noContact'])} Duelle ohne einen einzigen Schuss.** Wo die Waffenreichweite "
        'über der Sichtweite liegt, kann sie ohne Aufklärung nicht benutzt werden — `CombatSystem` '
        'verlangt das Ziel als sichtbar in der committed Team-Sicht. Das ist ein Balance-Befund, '
        'kein Messfehler.', '',
        f"**{fmt(counts['wobble'])} Paarungen** liessen über 10 % eines Budgets ungenutzt — dort "
        'wackelt die AE-Parität selbst, ihr Ausgang ist schwaches Material.', '',
    ]
    if disagreements:
        out += [f'**{len(disagreements)} Richtungsabweichungen** — Rollenpaarungen, bei denen A '
                'gegen B anders ausgeht als B gegen A. Sie sind so knapp, dass die Spawnreihenfolge '
                'sie kippt; das gehört in den Bericht, nicht wegkalibriert. Spiegelpaarungen '
                '(dieselbe Rolle gegen sich selbst) zählen nicht mit: dort entscheidet die '
                'dokumentierte Duell-Asymmetrie.', '',
                '<details><summary>Die betroffenen Paarungen</summary>', '',
                '\n'.join(f'- {d}' for d in disagreements), '', '</details>', '']
    return '\n'.join(out)


def _siege_section(record):
    contact = [r for r in record['siege'] if r['range'] == 'Contact']
    weapon = [r for r in record['siege'] if r['range'] == 'LongestWeapon']
    own = sorted((r for r in contact if r['a'][:3] == r['b'][:3]), key=lambda r: (r['a'], r['b']))

    rows = [[f"**{r['a']}**", r['b'],
             fmt(r['tick']) if r['decided'] else 'ins Tickbudget',
             fmt(r['countA']), fmt(r['spentA']), fmt(r['countA'] - r['survA'])]
            for r in own]

    return '\n'.join([
        '## Laufart 2 · Belagerungsstaffel — womit reisst man eine Basis ein', '',
        'Gebäude schiessen nicht zurück — ausser der `DefensePlatform`. Gemessen wird deshalb die '
        'Zeit bis zum Abriss, gegen das eigene Fraktionsgebäude, Staffel *Berührung*. Sortierung '
        'alphabetisch, nicht nach Zeit.', '',
        '> [!WARNING]',
        '> **Diese Staffel läuft nicht auf AE-Parität**, anders als die Einheitenduelle. Jeder',
        '> Angreifer stellt sechs Einheiten *seiner* Kosten, die Tickzahlen sind untereinander',
        '> deshalb nicht direkt vergleichbar — die AE-Spalte gehört zur Tickspalte dazu. Wer',
        '> Waffenwirkung vergleichen will, rechnet `Ticks × AE`.', '',
        table(['Angreifer', 'Gebäude', 'Ticks bis Abriss', 'Einheiten', 'AE', 'eigene Verluste'],
              rows, 'llrrrr'), '',
        f"**Nur {sum(1 for r in weapon if r['decided'])} von {len(weapon)} Belagerungen auf "
        f"Waffenreichweite wurden entschieden**, gegen {sum(1 for r in contact if r['decided'])} "
        f"von {len(contact)} auf Berührung. Dieselbe Ursache wie in der Gegentabelle: ohne Sicht "
        'kein Feuer.', '',
    ])


def _movement_section(record):
    moves = record['movement']
    standoff = [r for r in moves if r['scenario'] == 'Standoff']

    standoff_rows = [[f"{r['faction']} {r['role']}",
                      f"{r['usableRangeOvershootCells']} von {usable_range(r)}",
                      fmt(r['attackRangeCells']), fmt(r['sightRadiusCells']),
                      fmt(usable_range(r)), fmt(r['closestApproachCells']),
                      f"{r['arrived']}/{r['groupSize']}"] for r in standoff]

    def block(r):
        return (f"{r['blockedUnits']} Einh. · {r['longestSingleBlockTicks']} Ticks"
                if r['scenario'] == 'Blocking' else '—')

    def gap(r):
        # Der Durchlass wird aus dem CostField zurueckgemessen, nicht angenommen:
        # eine Mauer mit einem zweiten, unbemerkten Loch macht "niemand blockiert"
        # zu einer Eigenschaft des Aufbaus statt des Bewegungscodes.
        return f"{r['wallGapCells']} Zellen @ y={r['wallGapStartCell']}" if r['wallGapCells'] else '—'

    def zero_dash(value):
        """0 ist hier keine Messung, sondern "gibt es in diesem Szenario nicht" —
        dieselbe Unterscheidung, die das Dashboard trifft."""
        return '—' if value == 0 else fmt(value)

    rows = [[r['scenario'], r['faction'], r['role'], fmt(r['groupSize']), fmt(r['arrived']),
             f"{r['ticksToFirstArrival']}/{r['ticksToLastArrival']}" if r['arrived'] else '—',
             fmt(r['spreadCells']), zero_dash(r['travelledCells']),
             zero_dash(r['straightLineCells']), block(r), gap(r),
             zero_dash(r['usableRangeOvershootCells'])]
            for r in moves]

    return '\n'.join([
        '## Laufart 3 · `movement` — Bewegung: vier Szenarien', '',
        'Hindernisse sind Daten, nicht Code: eine Fussabdruckliste, eine Gruppe, ein Befehl.', '',
        '**Überlauf im Szenario `standoff`** — Zellen, die Fernkämpfer mit Angriffsbefehl über die '
        'Entfernung hinaus vorrücken, auf der sie zum ersten Mal Schaden angerichtet haben.', '',
        table(['Fraktion / Rolle', 'Überlauf (nutzbar)', 'Reichweite', 'Sicht', 'Feuer ab',
               'nächster Abstand', 'angekommen'], standoff_rows, 'lrrrrrr'), '',
        '**Nur die erste Spalte ist Issue 03.** Der Abstand zwischen nominaler Reichweite und '
        '„Feuer ab" gehört der Aufklärung, nicht der Bewegung: `CombatSystem` verlangt das Ziel '
        'als sichtbar in der committed Team-Sicht. Ein Kontrolllauf, der die Gruppe auf voller '
        'Reichweite stehen liess, richtete über 2.000 Ticks null Schaden an — „auf Reichweite '
        'stehenbleiben" wäre also keine Verbesserung, sondern eine wirkungslose Waffe. Die Werte '
        'sind absichtlich fraktionsasymmetrisch (Allianz 20, Legion 18 Tiles). Im `standoff` ist '
        '„angekommen 0" kein Befund, sondern der Auftrag: die Gruppe greift an, sie reist nicht.', '',
        '**Alle Szenarien, gemessene Rohwerte je Fraktion**', '',
        table(['Szenario', 'Fraktion', 'Rolle', 'Gruppe', 'angekommen', 'erster/letzter',
               'Streuung', 'Weg', 'Luftlinie', 'blockiert', 'Durchlass', 'Überlauf (nutzbar)'],
              rows, 'lllrrrrrrllr'), '',
    ])


def report_markdown(record, dashboard_link='../out/dashboard.html'):
    """Ein vollstaendiger Laborlauf als Markdown.

    `dashboard_link` zeigt relativ zur geschriebenen Datei auf die interaktive
    Fassung — aus `reports/` eine Ebene hoch, aus `reports/runs/` zwei."""
    run, result = record['run'], record['match']['result']
    head = '\n'.join([
        f"# Laborlauf {run['id']}", '',
        BANNER, '',
        table(['Herkunft', 'Wert'], [
            ['gemessen am', run['timestamp']],
            ['Commit', f"`{run['commit'] or '—'}`"],
            ['Definitionstabelle', f"`{run['definitionsHash64']}`"],
            ['KI-Verhalten', f"`{run.get('aiBehaviorId') or '—'}`"],
            ['Seed', f"`{result['seed']}`"],
            ['Tickbudget', fmt(result['tickBudget'])],
            ['Slots', fmt(result['slotCount'])],
            ['specVersion', fmt(result['specVersion'])],
            ['Fingerabdruck', f"`{run['fingerprint']}`"],
        ], 'lr'), '',
        f'Interaktive Fassung derselben Zahlen: [`dashboard.html`]({dashboard_link}) — Kurven mit '
        'Fadenkreuz, Heatmap mit Abstandsdetail, Scrubber. Lokal öffnen, kein Server nötig; '
        'GitHub zeigt HTML nicht an, dafür ist dieser Bericht da.', '', '',
    ])

    body = '\n'.join([
        _match_section(record),
        _compare_section(record),
        _duel_section(record),
        _siege_section(record),
        _movement_section(record),
        '## Reproduktion', '',
        'Alle Zahlen dieses Berichts stammen aus vier Kommandos und einem Berichtslauf:', '',
        REPRO, '',
        '---', '',
        'Nova.AiLab ist lokales Werkzeug, kein Beitrag — es gerät in keinen `feat/`-Branch und '
        f"wird nie gemergt. Diese Ergebnismenge ist an Commit `{run['commitShort'] or '—'}` und "
        f"Definitionstabelle `{run['definitionsHash64']}` gebunden. Nach dem nächsten "
        'Merge-Fenster des Maintainers sind die Zahlen nicht mehr vergleichbar und werden neu '
        'vermessen, nicht über die Grenze hinweg verglichen.', '',
        f"*{record['compare'].get('evidence', '')}*", '',
    ])
    return assert_no_ranking(head + body)


# ── Alle Laeufe ──────────────────────────────────────────────────────────────

def usable_range(movement_row):
    """Die Entfernung, auf der das erste Mal Schaden fiel — die einzige
    Reichweite, gegen die ein Ueberlauf etwas bedeutet."""
    return max(0, movement_row['firstContactDistanceCells'])


def run_summary(record):
    """Die Zeile, mit der ein Lauf in der Gesamtuebersicht steht."""
    run, result, counts = record['run'], record['match']['result'], record['duel']['counts']
    standoff = [r for r in record['movement'] if r['scenario'] == 'Standoff']
    # Im `standoff` ist "angekommen 0" kein Befund, sondern der Auftrag: die
    # Gruppe greift an, sie reist nicht. Die Ankunftsquote zaehlt deshalb ohne
    # dieses Szenario — sonst stuende in der Uebersicht dauerhaft eine Warnung,
    # die keine ist.
    travelling = [r for r in record['movement'] if r['scenario'] != 'Standoff']
    return {
        'id': run['id'],
        'timestamp': run['timestamp'],
        'commitShort': run['commitShort'] or '—',
        'definitionsHash64': run['definitionsHash64'],
        'aiBehaviorId': run.get('aiBehaviorId', ''),
        'winnerSlot': result['winnerSlot'],
        'decidedTick': result['decidedTick'],
        'finalStateHash': result['finalStateHash'],
        'duelDecided': counts['decided'],
        'duelTotal': counts['total'],
        'duelNoContact': counts['noContact'],
        'duelWobble': counts['wobble'],
        'standoffOvershoot': sum(r['usableRangeOvershootCells'] for r in standoff),
        'standoffUsable': sum(usable_range(r) for r in standoff),
        'movementArrived': sum(r['arrived'] for r in travelling),
        'movementGroup': sum(r['groupSize'] for r in travelling),
    }


def index_markdown(summaries):
    """Die Gesamtuebersicht: jeder archivierte Lauf in einer Zeile, neuester
    zuerst, dazu der Verlauf ueber alle Laeufe."""
    ordered = sorted(summaries, key=lambda s: s['timestamp'])
    newest = ordered[-1] if ordered else None
    tables = sorted({s['definitionsHash64'] for s in ordered})

    rows = []
    for s in reversed(ordered):
        # Wechselt die Definitionstabelle, ist die Historie zweigeteilt. Die
        # Ziffer sagt, welcher Haelfte eine Zeile angehoert — verglichen wird
        # ueber die Grenze hinweg nicht.
        marker = '' if len(tables) < 2 else f" ({tables.index(s['definitionsHash64']) + 1})"
        rows.append([
            f"[`{s['id']}`](runs/{s['id']}.md)",
            s['timestamp'][:16].replace('T', ' '),
            f"`{s['commitShort']}`{marker}",
            f"Slot {s['winnerSlot']}" if s['winnerSlot'] >= 0 else '—',
            fmt(s['decidedTick']),
            f"{s['duelDecided']}/{s['duelTotal']}",
            fmt(s['duelNoContact']),
            fmt(s['duelWobble']),
            f"{s['standoffOvershoot']}/{s['standoffUsable']}",
            f"{s['movementArrived']}/{s['movementGroup']}",
            f"`{s['finalStateHash']}`",
        ])

    out = [
        '# Nova.AiLab — Berichte', '',
        BANNER, '',
        '↩ zurück zum [Labor](../README.md) · [Handreichung für Agenten](../AGENTS.md)', '',
        'Dieser Ordner ist die **lesbare Fassung** der Laborläufe: [`latest.md`](latest.md) ist '
        'immer der zuletzt vermessene Lauf, `runs/` die Historie, `data/` die verdichteten '
        'Messwerte, aus denen beides jederzeit neu entsteht. Die interaktive Fassung mit Kurven, '
        'Heatmap und Scrubber bleibt [`../out/dashboard.html`](../out/dashboard.html) — sie '
        'braucht einen Browser, dieser Ordner nicht.', '',
        '> [!IMPORTANT]',
        '> **Was hier NICHT generiert wird: [`behavior-log.md`](behavior-log.md).** Die Berichte '
        'sagen, wo die Zahlen stehen — das Journal sagt, *warum* sie sich bewegt haben: je '
        'Verhaltensänderung die genauen Werte, die Folgen in beide Richtungen und ein Abschnitt '
        '„Widerlegt". Vor einer neuen Idee zuerst dort nachsehen; eine Sackgasse, die niemand '
        'aufgeschrieben hat, wird ein zweites Mal gelaufen.', '',
        '```bash',
        '# messen, Bericht schreiben, Historie fortschreiben — ein Kommando',
        'python3 tools/Nova.AiLab/report/build_reports.py tools/Nova.AiLab/out',
        '',
        '# nur neu rendern, ohne zu messen (nach einer Formatänderung)',
        'python3 tools/Nova.AiLab/report/build_reports.py --regenerate',
        '```', '',
    ]

    if newest:
        out += [
            f"## Zuletzt vermessen — [`{newest['id']}`](latest.md)", '',
            table(['Was', 'Wert'], [
                ['gemessen am', newest['timestamp']],
                ['Commit', f"`{newest['commitShort']}`"],
                ['Definitionstabelle', f"`{newest['definitionsHash64']}`"],
                ['KI-Verhalten', f"`{newest.get('aiBehaviorId') or '—'}`"],
                ['Partie entschieden bei Tick',
                 f"{fmt(newest['decidedTick'])} — Slot {newest['winnerSlot']}"
                 if newest['winnerSlot'] >= 0 else fmt(newest['decidedTick'])],
                ['Duelle entschieden', f"{fmt(newest['duelDecided'])} von "
                                       f"{fmt(newest['duelTotal'])}, "
                                       f"{fmt(newest['duelNoContact'])} ohne Kontakt"],
                ['Überlauf `standoff`', f"{newest['standoffOvershoot']} von "
                                        f"{newest['standoffUsable']} nutzbaren Zellen"],
                ['Endzustands-Hash', f"`{newest['finalStateHash']}`"],
            ], 'lr'), '',
        ]

    out += [
        f"## Historie — {len(ordered)} {'Lauf' if len(ordered) == 1 else 'Läufe'}", '',
        table(['Lauf', 'gemessen (UTC)', 'Commit', 'Sieger', 'entsch. Tick', 'Duelle entsch.',
               'ohne Kontakt', 'wackelnd', 'Überlauf standoff', 'angekommen', 'Endzustands-Hash'],
              rows, 'llllrrrrrrl'), '',
    ]

    if len(tables) > 1:
        out += ['> [!WARNING]',
                '> **Über Merge-Fenster hinweg wird nicht verglichen.** Diese Historie enthält '
                'Läufe gegen verschiedene Definitionstabellen; die Ziffer hinter dem Commit sagt, '
                'gegen welche. Zahlen aus verschiedenen Gruppen stehen nebeneinander, nicht '
                'gegeneinander.', '',
                table(['Gruppe', 'Definitionstabelle', 'Läufe'],
                      [[i + 1, f'`{h}`', sum(1 for s in ordered if s['definitionsHash64'] == h)]
                       for i, h in enumerate(tables)], 'llr'), '']

    # Der Verlauf zeichnet NUR die Laeufe gegen die zuletzt gemessene
    # Definitionstabelle. Eine Linie ueber den Tabellenwechsel hinweg waere
    # genau der Vergleich, den die Warnung darueber verbietet — sie saehe nur
    # deshalb aus wie eine Entwicklung, weil zwei verschiedene Spiele
    # untereinander stehen.
    current = [s for s in ordered if not newest or
               s['definitionsHash64'] == newest['definitionsHash64']]
    if len(current) > 1:
        older = len(ordered) - len(current)
        if older:
            out += [f'Der Verlauf zeigt die {len(current)} Läufe gegen die aktuelle '
                    f"Definitionstabelle `{newest['definitionsHash64']}`; "
                    + (f'{older} älterer Lauf steht' if older == 1
                       else f'{older} ältere Läufe stehen')
                    + ' nur in der Tabelle darüber.', '']
        labels = ', '.join(f'"{s["id"][:13]}"' for s in current)
        for title, key, y_label in [
            ('Entscheidungstick der Partie', 'decidedTick', 'Tick'),
            ('Duelle ohne Kontakt', 'duelNoContact', 'Duelle'),
            ('Überlauf im Szenario standoff', 'standoffOvershoot', 'Zellen'),
        ]:
            values = [s[key] for s in current]
            out += [f'**{title}** — je Lauf, ältester links', '',
                    '```mermaid', 'xychart-beta', f'    title "{title}"',
                    f'    x-axis [{labels}]',
                    f'    y-axis "{y_label}" 0 --> {axis_top([values])}',
                    '    line [' + ', '.join(str(v) for v in values) + ']',
                    '```', '']

    out += [
        '---', '',
        'Ein grüner Laborlauf ist Diagnose, kein Nachweis. Was nicht im laufenden Spiel gesehen '
        'wurde, steht als ungesehen im PR-Text — diese Seite ersetzt keine gespielte Beobachtung.', '',
    ]
    return assert_no_ranking('\n'.join(out))
