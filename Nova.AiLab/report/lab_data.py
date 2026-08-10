#!/usr/bin/env python3
"""Liest einen Laborlauf aus `out/lab/` und verdichtet ihn zu einem Datenblock.

Eine Quelle fuer beide Berichtsformen: `build_dashboard.py` bettet den Block in
die HTML-Seite ein, `markdown_report.py` rendert ihn nach Markdown. Was hier
nicht steht, steht in keinem der beiden Berichte.

Es wird bewusst nichts gerechnet, was die Laufarten nicht schon gemessen haben:
umsortieren, summieren, ausduennen — keine abgeleiteten Kennzahlen und vor allem
keine Gesamtnote (Plan 3.6: das Labor rankt nicht, es legt nebeneinander).
"""

import datetime
import hashlib
import json
import os

# Metriken je Slot, die die Berichte als Kurve zeichnen. Alles Ganzzahlen —
# aus der Simulation kommt kein Float, und hier entsteht auch keiner.
TRACE_KEYS = [
    'credits', 'powerProvided', 'powerRequired', 'harvesters', 'armySize',
    'armyHealthSum', 'unitsLost', 'visibleEnemyUnits', 'visibleEnemyBuildings',
    'intentsSubmitted', 'intentsRejected', 'queuedUnits', 'sitesOpen',
]

# Version des archivierten Datenblocks. Wird sie erhoeht, muessen die
# historischen Berichte neu gerendert werden — genau dafuer liegt neben jedem
# Bericht sein Datenblock (`build_reports.py --regenerate`).
#
# 2 — `duel.budget` (eine Zahl) wurde zu `duel.budgetRange` (Spanne ueber alle
#     Paarungen). Die eine Zahl war die einer beliebigen Paarung und stand als
#     "Budget je Seite" ueber der ganzen Tabelle.
REPORT_SCHEMA_VERSION = 2


def read_ndjson(path):
    with open(path, encoding='utf-8') as handle:
        return [json.loads(line) for line in handle if line.strip()]


def read_json(path):
    """Eine JSON-Datei, mit geschlossenem Griff.

    `json.load(open(...))` haelt sich daran auf, dass CPython die Datei beim
    Wegfallen der letzten Referenz schliesst — eine Zusage der Implementierung,
    nicht der Sprache. Ein `with` kostet nichts und gilt ueberall.
    """
    with open(path, encoding='utf-8') as handle:
        return json.load(handle)


def short(faction, role):
    """Fraktion + Rolle als ein Bezeichner. Jede Einheiten-Zahl ist
    fraktionsgebunden (Plan 3.9) — eine Zeile ohne Fraktion mittelt zwei
    verschiedene Waffen."""
    return faction[:3] + '.' + role


def collect_match(root):
    result = read_json(os.path.join(root, 'match', 'result.json'))
    samples = read_ndjson(os.path.join(root, 'match', 'trace.ndjson'))
    slots = []
    for index in range(result['slotCount']):
        series = {key: [s['slots'][index][key] for s in samples] for key in TRACE_KEYS}
        series['buildings'] = [sum(s['slots'][index]['buildingsByRole']) for s in samples]
        slots.append(series)
    return {'result': result, 'trace': {'ticks': [s['tick'] for s in samples], 'slots': slots}}


def collect_duels(root):
    duels = read_ndjson(os.path.join(root, 'duel', 'duels.ndjson'))

    units, cells = [], {}
    for duel in duels:
        if duel['siege']:
            continue
        attacker = short(duel['factionA'], duel['roleA'])
        defender = short(duel['factionB'], duel['roleB'])
        for name in (attacker, defender):
            if name not in units:
                units.append(name)
        cell = cells.setdefault((attacker, defender),
                                {'n': 0, 'w': 0, 'l': 0, 'u': 0, 'nc': 0, 'wob': 0, 'ranges': {}})
        cell['n'] += 1
        cell['w'] += duel['winner'] == 0
        cell['l'] += duel['winner'] == 1
        cell['u'] += duel['winner'] < 0
        cell['nc'] += duel['noContact']
        cell['wob'] += duel['parityWobbles']
        cell['ranges'][duel['range']] = {
            'winner': duel['winner'], 'tick': duel['decidedTick'],
            'survA': duel['survivorsA'], 'survB': duel['survivorsB'],
            'noContact': duel['noContact'],
        }
    units.sort()

    siege = [{
        'a': short(d['factionA'], d['roleA']), 'b': short(d['factionB'], d['roleB']),
        'range': d['range'], 'decided': d['decided'],
        'tick': d['decidedTick'] if d['decided'] else None,
        'countA': d['countA'], 'spentA': d['spentA'], 'survA': d['survivorsA'],
        'winner': d['winner'],
    } for d in duels if d['siege']]

    # DAS BUDGET IST PRO PAARUNG BEMESSEN, nicht global (DuelArena.DeriveBudget:
    # es sizt so, dass die TEURE Seite `unitsPerSide` Einheiten stellt). Hier
    # stand frueher `duels[0]['budgetAE']` — die Zahl einer beliebigen Paarung,
    # ueber der ganzen Tabelle als "Budget je Seite" ausgewiesen. Bei einem
    # echten Lauf sind das zwoelf verschiedene Werte und gedruckt wurde einer.
    budgets = sorted({d['budgetAE'] for d in duels})

    table = {
        'units': units,
        'cells': [dict(a=a, b=b, **v) for (a, b), v in cells.items()],
        'budgetRange': [budgets[0], budgets[-1]],
        'budgetValues': len(budgets),
        'counts': {
            'total': len(duels),
            'decided': sum(1 for d in duels if d['decided']),
            'timeout': sum(1 for d in duels if not d['decided']),
            'noContact': sum(1 for d in duels if d['noContact']),
            # Wackelnde Paritaet wird je Paarung gezaehlt, nicht je Duell:
            # sonst zaehlt dieselbe schiefe Paarung dreimal (ein Abstand je Lauf).
            'wobble': len({(d['factionA'], d['roleA'], d['factionB'], d['roleB'])
                           for d in duels if d['parityWobbles']}),
        },
    }
    return table, siege


def collect(root):
    """Alle vier Laufarten aus `root` zu einem Block. Reihenfolge der Schluessel
    ist fest — der Fingerabdruck haengt daran."""
    duel, siege = collect_duels(root)
    return {
        'match': collect_match(root),
        'compare': read_json(os.path.join(root, 'compare', 'resultset.json')),
        'movement': read_ndjson(os.path.join(root, 'movement', 'movement.ndjson')),
        'duel': duel,
        'siege': siege,
    }


def fingerprint(data):
    """Was diesen Lauf von jedem anderen unterscheidet.

    Ueber den gesamten Messblock, nicht ueber eine Auswahl: zwei Laeufe mit
    demselben Fingerabdruck haben dieselben Zahlen gemessen und brauchen keinen
    zweiten Eintrag in der Historie. Ein wiederholter Bericht ueberschreibt
    seinen Eintrag dann, statt die Liste zu verdoppeln."""
    payload = json.dumps(data, sort_keys=True, separators=(',', ':')).encode('utf-8')
    return hashlib.sha256(payload).hexdigest()[:16]


def run_identity(root, data):
    """Herkunft des Laufs: wann gemessen, an welchem Commit, gegen welche
    Definitionstabelle. Der Zeitstempel kommt aus `result.json` selbst — die
    Simulation kennt keine Wanduhr, die Datei schon."""
    stamp = os.path.getmtime(os.path.join(root, 'match', 'result.json'))
    when = datetime.datetime.fromtimestamp(stamp, datetime.timezone.utc)
    commit = data['compare'].get('commit', '')
    return {
        'reportSchemaVersion': REPORT_SCHEMA_VERSION,
        'id': when.strftime('%Y%m%d-%H%M') + '-' + (commit[:8] or 'nocommit'),
        'timestamp': when.strftime('%Y-%m-%dT%H:%M:%SZ'),
        'commit': commit,
        'commitShort': commit[:8],
        'definitionsHash64': data['compare'].get('definitionsHash64', ''),
        # WELCHE KI gespielt hat. Ohne diese Zeile laesst sich eine Messung
        # keinem Eintrag im Verhaltensjournal zuordnen.
        'aiBehaviorId': data['match']['result'].get('aiBehaviorId', ''),
        'finalStateHash': data['match']['result']['finalStateHash'],
        'fingerprint': fingerprint(data),
        'source': os.path.normpath(root),
    }


def archive_record(root):
    """Der Block, wie er unter `reports/data/<id>.json` liegt: Messwerte plus
    Herkunft. Er ist die Quelle jeder Neuerzeugung — die Markdown-Dateien sind
    Ableitung, nicht Ablage."""
    data = collect(root)
    return dict(run=run_identity(root, data), **data)
