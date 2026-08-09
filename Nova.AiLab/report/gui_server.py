#!/usr/bin/env python3
"""Die Steuerseite des Labors: messen, ansehen, vergleichen — alles im Browser.

    ./lab-gui.sh                 startet den Server und oeffnet die Seite

WOZU EIN SERVER, WO DER REST OHNE AUSKOMMT. `player.html` und `dashboard.html`
sind Ansichten fertiger Artefakte; eine Seite reicht dafuer. Diese hier soll
einen Lauf STARTEN, einen Branch auswaehlen und zwei Laeufe nebeneinanderlegen —
und eine Seite ohne Gegenstelle kann kein `dotnet` aufrufen. Der Server ist die
kleinstmoegliche Gegenstelle: nur Standardbibliothek, nur an 127.0.0.1, keine
Abhaengigkeit ausser dem `python3`, das die Berichte ohnehin brauchen.

WAS ER AM SPIEL-CHECKOUT AENDERT: nichts. Ein anderer Branch wird nie im
Arbeitscheckout ausgecheckt — dafuer legt er ein `git worktree` unter
`.worktrees/` an und misst dort. Der Checkout, in dem gearbeitet wird, bleibt
stehen, wo er steht. Gepusht, gemergt oder committet wird von hier aus nichts.

WERKZEUG, KEIN BEITRAG. Ein gruener Laborlauf ist Diagnose, kein Nachweis: was
nicht im laufenden Spiel gesehen wurde, steht als ungesehen im PR-Text.
"""

import glob
import http.server
import json
import os
import re
import shutil
import socketserver
import subprocess
import threading
import time
import urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
LAB = os.path.normpath(os.path.join(HERE, '..', '..'))
GUI_RUNS = os.path.join(LAB, 'out', 'gui')
WORKTREES = os.path.join(LAB, '.worktrees')
HISTORY = os.path.join(LAB, 'reports', 'data')

# Der gemessene Checkout. Wird von lab-gui.sh gesetzt, sonst der Nachbarordner.
REPO = os.environ.get('NovaRepo') or os.path.normpath(os.path.join(LAB, '..', 'Project_Nova'))

# Ein Branchname, den git akzeptiert und der als Ordnername taugt. Alles andere
# wird abgewiesen, bevor es in eine Kommandozeile geraet — hier laeuft nichts
# ueber eine Shell, aber eine Pruefung vor dem Aufruf ist billiger als die
# Frage, ob subprocess wirklich nirgends eine Shell benutzt.
SAFE_REF = re.compile(r'^[A-Za-z0-9._/-]{1,120}$')

# Eine Laufkennung oder ein Blockname: derselbe Zeichenvorrat ohne Schraegstrich.
SAFE_NAME = re.compile(r'^[A-Za-z0-9._-]{1,120}$')


def safe_name(value, pattern, what):
    """Ein Name aus einer Anfrage, oder eine Ausnahme. Drei Dinge, nicht eines:

    1. der Zeichenvorrat — `pattern`,
    2. kein `.`- oder `..`-Bestandteil und kein leerer, damit aus einem Namen
       nie eine Bewegung im Dateibaum wird. `..` allein passte durch beide
       Muster und zeigte auf den Elternordner,
    3. kein fuehrender Bindestrich. Der Name geht als Argument an `git`, und
       ein Argument, das mit `-` beginnt, ist dort eine Option und kein Wert.
    """
    text = (value or '').strip()
    parts = text.split('/')
    if not pattern.match(text) or text.startswith('-') or any(p in ('', '.', '..') for p in parts):
        raise ValueError(f'{what} nicht erlaubt: {value!r}')
    return text


def inside(base, *parts):
    """Ein Pfad UNTER `base`, oder gar keiner.

    JEDER Pfad, in den ein Stueck Anfrage eingeht, laeuft durch hier — und
    zwar aufgeloest, nicht nur zusammengesetzt: `realpath` zieht `..` und
    Symlinks heraus, bevor verglichen wird, sonst prueft man den Text und
    oeffnet den Pfad. Ein Treffer ausserhalb ist kein 404, sondern ein Fehler:
    die Steuerseite hat dort nichts zu suchen, auch nicht lesend.
    """
    root = os.path.realpath(base)
    target = os.path.realpath(os.path.join(root, *parts))
    if target != root and not target.startswith(root + os.sep):
        raise ValueError(f'Pfad ausserhalb von {base}: {os.path.join(*parts)!r}')
    return target

# Der Dateiname, den RunArtifacts fuer die Seite vergibt.
HtmlPlayerName = 'player.html'

_jobs = {}
_job_lock = threading.Lock()


# ----------------------------------------------------------------- git

def git(*args, cwd=None):
    """git ohne Shell, mit Ausgabe als Text. Fehler werfen."""
    return subprocess.run(['git', '-C', cwd or REPO, *args],
                          capture_output=True, text=True, check=True).stdout


def git_quiet(*args, cwd=None):
    """Wie `git`, aber ein Fehlschlag ist eine leere Antwort statt einer Ausnahme."""
    try:
        return git(*args, cwd=cwd)
    except (subprocess.CalledProcessError, FileNotFoundError):
        return ''


def branches():
    """Lokale Branches und Remote-Zweige, in einer Liste, ohne Dubletten."""
    found, seen = [], set()
    for line in git_quiet('for-each-ref', '--format=%(refname:short)',
                          'refs/heads', 'refs/remotes').splitlines():
        ref = line.strip()
        if not ref or ref.endswith('/HEAD') or ref in seen:
            continue
        seen.add(ref)
        found.append(ref)
    return found


def head_of(ref):
    return git_quiet('rev-parse', '--short', ref).strip()


def checkout_for(ref):
    """Der Pfad, in dem `ref` gemessen wird — der Arbeitscheckout oder ein worktree.

    DER ARBEITSCHECKOUT WIRD NIE UMGESCHALTET. Wer gerade an etwas sitzt, will
    nicht, dass eine Messung im Hintergrund seinen Branch wechselt. Fuer alles,
    was nicht der aktuelle HEAD ist, entsteht ein losgeloester worktree unter
    `.worktrees/` — dieselbe Loesung, die README und lab.sh mit `--repo` von Hand
    beschreiben, nur ohne Handarbeit.
    """
    current = git_quiet('rev-parse', '--abbrev-ref', 'HEAD').strip()
    if ref in ('', 'current', current):
        return REPO, current or 'HEAD'

    ref = safe_name(ref, SAFE_REF, 'Branchname')
    path = inside(WORKTREES, ref.replace('/', '_'))
    if not os.path.isdir(os.path.join(path, '.git')) and not os.path.isfile(os.path.join(path, '.git')):
        os.makedirs(WORKTREES, exist_ok=True)
        shutil.rmtree(path, ignore_errors=True)
        git('worktree', 'add', '--detach', path, ref)
    else:
        # Vorhandenen worktree auf den heutigen Stand des Refs ziehen.
        git('fetch', '--all', '--quiet')
        git('checkout', '--detach', ref, cwd=path)
    return path, ref


# ------------------------------------------------------------ die Laeufe

def run_meta(directory):
    """Ein GUI-Lauf als Zeile fuer die Liste. Fehlt etwas, fehlt die Zeile nicht."""
    meta_path = os.path.join(directory, 'meta.json')
    meta = json.load(open(meta_path, encoding='utf-8')) if os.path.exists(meta_path) else {}
    result_path = os.path.join(directory, 'result.json')
    result = json.load(open(result_path, encoding='utf-8')) if os.path.exists(result_path) else {}
    return {
        'id': os.path.basename(directory),
        'branch': meta.get('branch', '?'),
        'commit': meta.get('commit', '?'),
        'label': meta.get('label', ''),
        'started': meta.get('started', ''),
        'args': meta.get('args', []),
        'outcome': result.get('outcome'),
        'winnerSlot': result.get('winnerSlot'),
        'decidedTick': result.get('decidedTick'),
        'finalTick': result.get('finalTick'),
        'seed': result.get('seed'),
        'finalStateHash': result.get('finalStateHash'),
        'definitionsHash64': result.get('definitionsHash64'),
        'aiBehaviorId': result.get('aiBehaviorId'),
        'hasPlayer': os.path.exists(os.path.join(directory, 'player.html')),
        # Ein Lauf ohne result.json ist entweder noch unterwegs oder er ist
        # gescheitert. Beides als "laeuft…" zu zeigen waere die eine Anzeige,
        # die man nicht haben will.
        'exitCode': meta.get('exitCode'),
        'state': ('fertig' if result else
                  'fehlgeschlagen' if meta.get('exitCode') not in (None, 0) else 'läuft…'),
    }


def newest_player():
    """Der zuletzt geschriebene `player.html` unter `out/gui/`, oder nichts.

    Nach Aenderungszeit, nicht nach Lauf: die Seite ist ein Werkzeug und der
    Lauf sind die Daten. `player --out out/gui` schreibt alle Seiten neu, ohne
    eine einzige Zahl anzufassen — danach gewinnt die neue hier automatisch.
    """
    candidates = glob.glob(os.path.join(GUI_RUNS, '*', HtmlPlayerName))
    root = os.path.join(GUI_RUNS, HtmlPlayerName)
    if os.path.exists(root):
        candidates.append(root)
    return max(candidates, key=os.path.getmtime) if candidates else None


def gui_runs():
    if not os.path.isdir(GUI_RUNS):
        return []
    rows = [run_meta(d) for d in sorted(glob.glob(os.path.join(GUI_RUNS, '*'))) if os.path.isdir(d)]
    rows.reverse()
    return rows


def history():
    """Die archivierten Messbloecke — die Historie, die das Repo mittraegt.

    Sie enthaelt keine Rohartefakte (out/ ist nicht versioniert, mit Absicht),
    also gibt es dazu keinen Player. Was sie enthaelt, ist der verdichtete
    Block, aus dem jeder Bericht neu entsteht.
    """
    rows = []
    for path in sorted(glob.glob(os.path.join(HISTORY, '*.json'))):
        try:
            block = json.load(open(path, encoding='utf-8'))
        except (OSError, ValueError):
            continue
        run = block.get('run', {})
        result = block.get('match', {}).get('result', {})
        rows.append({
            'id': run.get('id', os.path.basename(path)[:-5]),
            'timestamp': run.get('timestamp', ''),
            'commit': run.get('commitShort', ''),
            'aiBehaviorId': run.get('aiBehaviorId', ''),
            'definitionsHash64': run.get('definitionsHash64', ''),
            'finalStateHash': run.get('finalStateHash', ''),
            'outcome': result.get('outcome'),
            'decidedTick': result.get('decidedTick'),
            'winnerSlot': result.get('winnerSlot'),
        })
    rows.reverse()
    return rows


def unit_summary(directory):
    """Was `units.json` ueber einen ganzen Lauf sagt, in vier Zahlen.

    Bewusst keine Note und kein Mittel ueber Ungleiches: der Umwegfaktor wird
    ueber die Einheiten gemittelt, die ueberhaupt einen hatten, und die Zahl
    dieser Einheiten steht daneben. Ein Mittelwert ohne seine Stichprobe ist
    genau die Sorte Zahl, gegen die dieses Labor gebaut ist.
    """
    path = os.path.join(directory, 'units.json')
    if not os.path.exists(path):
        return None
    try:
        units = json.load(open(path, encoding='utf-8'))['units']
    except (OSError, ValueError, KeyError):
        return None

    detours = [u['detourPercent'] for u in units if u['detourPercent'] >= 0]
    moving = sum(u['movingTicks'] for u in units)
    blocked = sum(u['blockedTicks'] for u in units)
    return {
        'units': len(units),
        'died': sum(1 for u in units if u['died']),
        'pathLengthCells': sum(u['pathLengthCells'] for u in units),
        'movingTicks': moving,
        'blockedTicks': blocked,
        'blockedPercent': (blocked * 100 // moving) if moving else -1,
        'detourMean': (sum(detours) // len(detours)) if detours else -1,
        'detourSamples': len(detours),
        'stuckUnits': sum(1 for u in units if u['blockedTicks'] > 0),
    }


def compare(a_id, b_id):
    rows, warnings = [], []
    a_dir, b_dir = run_dir(a_id), run_dir(b_id)
    a = run_meta(a_dir)
    b = run_meta(b_dir)

    if a['definitionsHash64'] and a['definitionsHash64'] != b['definitionsHash64']:
        warnings.append('Die Definitionstabellen unterscheiden sich — die beiden Laeufe messen '
                        'verschiedene Einheiten. Alles darunter ist nebeneinandergelegt, nicht vergleichbar.')
    if a['seed'] != b['seed']:
        warnings.append('Verschiedene Seeds. Der Seed aendert die Partie zwar nicht (kein System zieht '
                        'aus dem Kernel-PRNG), aber er geht in den Zustands-Hash — ein Hash-Unterschied '
                        'sagt hier nichts.')

    for key, name in [('outcome', 'Ausgang'), ('winnerSlot', 'Sieger-Slot'),
                      ('decidedTick', 'Entscheidungstick'), ('finalTick', 'Endtick'),
                      ('finalStateHash', 'Zustands-Hash'), ('aiBehaviorId', 'Verhaltens-ID'),
                      ('branch', 'Branch'), ('commit', 'Commit')]:
        rows.append({'name': name, 'a': a.get(key), 'b': b.get(key),
                     'same': a.get(key) == b.get(key)})

    ua, ub = unit_summary(a_dir), unit_summary(b_dir)
    if ua and ub:
        for key, name in [('units', 'Einheiten insgesamt'), ('died', 'davon gestorben'),
                          ('pathLengthCells', 'gelaufene Zellen'), ('movingTicks', 'Bewegungsticks'),
                          ('blockedTicks', 'davon blockiert'), ('blockedPercent', 'blockiert in %'),
                          ('detourMean', 'Umweg im Mittel %'), ('detourSamples', 'Umweg-Stichprobe'),
                          ('stuckUnits', 'Einheiten mit Blockade')]:
            rows.append({'name': name, 'a': ua[key], 'b': ub[key], 'same': ua[key] == ub[key],
                         'delta': ub[key] - ua[key] if isinstance(ua[key], int) else None})
    else:
        warnings.append('Mindestens einem der beiden Laeufe fehlt units.json — er wurde ohne '
                        '--view-every gemessen und traegt keine Laufrouten.')

    return {'a': a, 'b': b, 'rows': rows, 'warnings': warnings}


def run_dir(run_id):
    path = inside(GUI_RUNS, safe_name(run_id, SAFE_NAME, 'Laufkennung'))
    if not os.path.isdir(path):
        raise ValueError(f'Lauf unbekannt: {run_id}')
    return path


# ------------------------------------------------------------ ein Lauf

def start_run(options):
    """Startet einen Messlauf im Hintergrund und liefert die Auftragskennung."""
    checkout, ref = checkout_for(options.get('branch', 'current'))
    commit = head_of('HEAD') if checkout == REPO else git_quiet('rev-parse', '--short', 'HEAD', cwd=checkout).strip()

    stamp = time.strftime('%Y%m%d-%H%M%S')
    run_id = f"{stamp}-{ref.replace('/', '_')}-{commit or 'unknown'}"
    directory = inside(GUI_RUNS, safe_name(run_id, SAFE_NAME, 'Laufkennung'))
    os.makedirs(directory, exist_ok=True)

    args = ['match',
            '--seed', str(options.get('seed', '0xA17E57DE57')),
            '--ticks', str(int(options.get('ticks', 27000))),
            '--view-every', str(int(options.get('viewEvery', 25))),
            '--trace-every', str(int(options.get('traceEvery', 50))),
            '--hash-every', str(int(options.get('hashEvery', 500))),
            '--track-every', str(int(options.get('trackEvery', 1)))]
    if options.get('fog'):
        args.append('--fog')
    for flag in ('profile0', 'profile1'):
        value = (options.get(flag) or '').strip()
        if value:
            args += [f'--{flag}', value]
    args += ['--out', directory]

    json.dump({'branch': ref, 'commit': commit, 'checkout': checkout,
               'label': (options.get('label') or '').strip(),
               'started': time.strftime('%Y-%m-%dT%H:%M:%S'), 'args': args},
              open(os.path.join(directory, 'meta.json'), 'w', encoding='utf-8'), indent=2)

    job_id = run_id
    with _job_lock:
        _jobs[job_id] = {'running': True, 'log': [f'misst {ref} @ {commit} in {checkout}', ''],
                         'exitCode': None, 'runId': run_id}

    threading.Thread(target=_execute, args=(job_id, checkout, args), daemon=True).start()
    return job_id


def _execute(job_id, checkout, args):
    environment = dict(os.environ, NovaRepo=checkout)

    # DAS SDK LIEGT NUR IM ARBEITSCHECKOUT. `.dotnet/` ist im Spiel-Repo nicht
    # versioniert, ein frischer worktree hat es also nicht — und dann scheitert
    # der Lauf an einem 'dotnet', das es im PATH nicht gibt. Der Arbeitscheckout
    # ist der Rueckfall: es ist dasselbe SDK, es uebersetzt nur andere Quellen.
    for candidate in (os.path.join(checkout, '.dotnet'), os.path.join(REPO, '.dotnet')):
        if not os.path.isdir(candidate):
            continue
        environment['DOTNET_ROOT'] = candidate
        environment['PATH'] = candidate + os.pathsep + environment.get('PATH', '')
        break

    command = ['dotnet', 'run', '--project', os.path.join(LAB, 'Nova.AiLab'), '-c', 'Release', '--', *args]
    try:
        process = subprocess.Popen(command, cwd=LAB, env=environment, text=True,
                                   stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        for line in process.stdout:
            with _job_lock:
                _jobs[job_id]['log'].append(line.rstrip())
        process.wait()
        code = process.returncode
    except FileNotFoundError:
        with _job_lock:
            _jobs[job_id]['log'].append(
                "FEHLER: 'dotnet' nicht gefunden. Das SDK liegt unter <checkout>/.dotnet und "
                'gehoert in den PATH — lab-gui.sh setzt das; wer gui_server.py von Hand startet, '
                'muss es selbst tun.')
        code = -1
    except Exception as error:                                   # noqa: BLE001
        with _job_lock:
            _jobs[job_id]['log'].append(f'FEHLER: {error}')
        code = -1

    with _job_lock:
        job = _jobs[job_id]
        job['running'] = False
        job['exitCode'] = code
        # Exit-Code 2 heisst NON-DETERMINISTIC. Dann ist jede Zahl aus diesem
        # Lauf wertlos, auch die gruenen — das gehoert in die Seite, nicht in
        # eine Zeile, die nur im Terminal steht.
        if code == 2:
            job['log'].append('')
            job['log'].append('EXIT 2 — NICHT DETERMINISTISCH. Kein Wert aus diesem Lauf zaehlt.')
        elif code != 0 and any('error CS' in line for line in job['log']):
            # Der haeufigste Fehlschlag bei einem alten Branch, und er sieht wie
            # ein Defekt aus, obwohl er die Wahrheit sagt: das Labor ist gegen
            # die HEUTIGE KI-Schnittstelle geschrieben. Fehlt drueben ein Feld,
            # das es benutzt, laesst sich dieser Branch mit diesem Labor nicht
            # messen — und ein Messwerkzeug, das sich daran vorbeimogelt, misst
            # etwas anderes als den Branch.
            job['log'].append('')
            job['log'].append(
                'UEBERSETZUNG FEHLGESCHLAGEN. Dieser Branch kennt eine Schnittstelle noch nicht, '
                'die das Labor benutzt — die Fehlerzeilen oben sagen welche. Das Labor misst den '
                'Branch also nicht; ein aelterer Stand braucht einen aelteren Laborstand.')

    meta_path = os.path.join(GUI_RUNS, job_id, 'meta.json')
    try:
        meta = json.load(open(meta_path, encoding='utf-8'))
        meta['exitCode'] = code
        json.dump(meta, open(meta_path, 'w', encoding='utf-8'), indent=2)
    except (OSError, ValueError):
        pass

    # Der frisch geschriebene Player wird der, den die Steuerseite fuer JEDEN
    # Lauf ausliefert — auch fuer die aelteren. Siehe serve_artifact.
    fresh = os.path.join(GUI_RUNS, job_id, HtmlPlayerName)
    if code == 0 and os.path.exists(fresh):
        shutil.copyfile(fresh, os.path.join(GUI_RUNS, HtmlPlayerName))


# ------------------------------------------------------------ der Server

class Handler(http.server.BaseHTTPRequestHandler):
    server_version = 'NovaAiLabGui'

    def log_message(self, fmt, *args):                            # noqa: A003
        pass                                                      # kein Zugriffslog im Terminal

    # -- Antworten -------------------------------------------------
    def send_json(self, payload, code=200):
        body = json.dumps(payload).encode('utf-8')
        self.send_response(code)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def send_file(self, path, content_type):
        try:
            with open(path, 'rb') as handle:
                body = handle.read()
        except OSError:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    # -- Routen ----------------------------------------------------
    def do_GET(self):                                             # noqa: N802
        url = urllib.parse.urlparse(self.path)
        query = urllib.parse.parse_qs(url.query)

        try:
            if url.path in ('/', '/index.html'):
                self.send_file(os.path.join(HERE, 'gui.tpl.html'), 'text/html; charset=utf-8')

            elif url.path == '/api/state':
                current = git_quiet('rev-parse', '--abbrev-ref', 'HEAD').strip()
                self.send_json({
                    'repo': REPO, 'currentBranch': current, 'currentCommit': head_of('HEAD'),
                    'branches': branches(), 'runs': gui_runs(), 'history': history(),
                })

            elif url.path == '/api/job':
                with _job_lock:
                    job = _jobs.get(query.get('id', [''])[0])
                    self.send_json(dict(job) if job else {'error': 'unbekannter Auftrag'})

            elif url.path == '/api/compare':
                self.send_json(compare(query.get('a', [''])[0], query.get('b', [''])[0]))

            elif url.path == '/api/history':
                name = safe_name(query.get('id', [''])[0], SAFE_NAME, 'Kennung')
                self.send_file(inside(HISTORY, name + '.json'), 'application/json; charset=utf-8')

            elif url.path.startswith('/runs/'):
                self.serve_artifact(url.path[len('/runs/'):])

            else:
                self.send_error(404)
        except Exception as error:                                # noqa: BLE001
            self.send_json({'error': str(error)}, code=400)

    def do_POST(self):                                            # noqa: N802
        url = urllib.parse.urlparse(self.path)
        length = int(self.headers.get('Content-Length') or 0)
        try:
            body = json.loads(self.rfile.read(length) or b'{}')
            if url.path == '/api/run':
                self.send_json({'jobId': start_run(body)})
            else:
                self.send_error(404)
        except Exception as error:                                # noqa: BLE001
            self.send_json({'error': str(error)}, code=400)

    def serve_artifact(self, relative):
        """Artefakte eines GUI-Laufs, damit `player.html` dort aufgeht wo er liegt.

        EINE AUSNAHME: die Seite selbst. Jeder Lauf legt beim Messen seinen
        eigenen `player.html` ab, damit ein Artefaktverzeichnis am Stueck
        kopierbar bleibt und per Doppelklick aufgeht — das ist Absicht und
        bleibt so. Fuer die Steuerseite ist es aber falsch: ein Lauf von
        letzter Woche wuerde mit dem Player von letzter Woche angesehen, und
        jede Verbesserung an der Ansicht waere fuer alles Aeltere unsichtbar.
        Deshalb gewinnt hier der NEUESTE Player, den das Labor geschrieben hat
        — nicht die Kopie, die nach dem letzten Lauf liegen blieb. Wer die
        Ansicht verbessert und `player --out out/gui` laufen laesst, sieht sie
        hier sofort; frueher brauchte es dafuer einen neuen Messlauf. Die Daten
        holt die Seite weiter relativ zu ihrer URL, also aus dem Lauf, den man
        angeklickt hat.
        """
        parts = urllib.parse.unquote(relative).split('/')
        current = newest_player()
        if len(parts) == 2 and parts[1] == HtmlPlayerName and current:
            self.send_file(current, 'text/html; charset=utf-8')
            return

        # Der Pfad muss UNTER dem Laufordner bleiben. Ohne diese Pruefung ist
        # ein ../../ in der URL ein Lesezugriff auf alles, was der Benutzer
        # lesen darf.
        try:
            target = inside(GUI_RUNS, urllib.parse.unquote(relative))
        except ValueError:
            self.send_error(403)
            return
        types = {'.html': 'text/html; charset=utf-8', '.json': 'application/json; charset=utf-8',
                 '.ndjson': 'text/plain; charset=utf-8', '.md': 'text/plain; charset=utf-8'}
        self.send_file(target, types.get(os.path.splitext(target)[1], 'application/octet-stream'))


class Server(socketserver.ThreadingTCPServer):
    daemon_threads = True
    allow_reuse_address = True


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Steuerseite des Labors')
    parser.add_argument('--port', type=int, default=8730)
    parser.add_argument('--repo', help='Spiel-Checkout, der gemessen wird')
    options = parser.parse_args()

    global REPO                                                   # noqa: PLW0603
    if options.repo:
        REPO = os.path.abspath(options.repo)
    if not os.path.isdir(os.path.join(REPO, 'Assets', '_Project', 'Scripts')):
        raise SystemExit(f"'{REPO}' ist kein Project-Nova-Checkout (kein Assets/_Project/Scripts).")

    os.makedirs(GUI_RUNS, exist_ok=True)
    # Nur an die Loopback-Adresse. Diese Gegenstelle startet Prozesse; sie hat
    # im Netz nichts zu suchen, auch nicht im eigenen.
    with Server(('127.0.0.1', options.port), Handler) as httpd:
        print(f'Labor-Steuerseite: http://127.0.0.1:{options.port}/')
        print(f'misst: {REPO}')
        print('Beenden mit Strg-C')
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print()


if __name__ == '__main__':
    main()
