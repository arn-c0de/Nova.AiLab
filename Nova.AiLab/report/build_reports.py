#!/usr/bin/env python3
"""Schreibt alle Berichtsformen eines Laborlaufs — ein Kommando, vier Ausgaben.

    python3 Nova.AiLab/report/build_reports.py out

erzeugt aus den Artefakten von `match`, `duel`, `movement` und `compare`:

    tools/Nova.AiLab/out/dashboard.html    interaktiv, lokal, kein Server
    reports/latest.md     immer der zuletzt vermessene Lauf
    reports/runs/<id>.md  die Historie, ein Bericht je Lauf
    reports/README.md     die Gesamtuebersicht ueber alle Laeufe

Der verdichtete Messblock jedes Laufs bleibt unter `reports/data/<id>.json`
liegen. **Er ist die Quelle, die Markdown-Dateien sind Ableitung.** Aendert sich
das Berichtsformat, werden alle historischen Berichte daraus neu erzeugt, ohne
dass irgendetwas nachgemessen werden muss:

    python3 Nova.AiLab/report/build_reports.py --regenerate

Ein Lauf wird an seinem Fingerabdruck erkannt. Zweimal derselbe Lauf ergibt
denselben Eintrag, keinen zweiten — der Ordner waechst mit den Messungen, nicht
mit den Berichtslaeufen.

WERKZEUG, KEIN BEITRAG. Die Seiten sind Diagnose; was nicht im laufenden Spiel
gesehen wurde, steht als ungesehen im PR-Text.
"""

import argparse
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import build_dashboard          # noqa: E402
import lab_data                 # noqa: E402
import markdown_report as md    # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
# Zwei Ebenen hoch, nicht eine: seit dem Ausbau aus dem Spiel-Repo liegt das
# Labor als eigenes Projekt da, mit dem Code unter Nova.AiLab/ und den
# Ergebnissen daneben — reports/, findings/ und out/ gehoeren dem Labor als
# Ganzem, nicht dem csproj darin.
LAB = os.path.normpath(os.path.join(HERE, '..', '..'))
DEFAULT_REPORTS = os.path.join(LAB, 'reports')
DEFAULT_RUN = os.path.join(LAB, 'out')


def load_records(reports_dir):
    """Alle archivierten Laeufe, aeltester zuerst."""
    records = []
    for path in sorted(glob.glob(os.path.join(reports_dir, 'data', '*.json'))):
        with open(path, encoding='utf-8') as handle:
            records.append(json.load(handle))
    return sorted(records, key=lambda r: r['run']['timestamp'])


def archive(reports_dir, record):
    """Legt den Lauf ab — oder erkennt ihn als bereits abgelegt wieder.

    Der Fingerabdruck deckt den gesamten Messblock ab. Faellt er mit einem
    vorhandenen Eintrag zusammen, sind es dieselben Zahlen: dann behaelt der
    aeltere Eintrag seine Kennung, damit ein zweiter Berichtslauf die Historie
    nicht verdoppelt."""
    for existing in load_records(reports_dir):
        if existing['run']['fingerprint'] == record['run']['fingerprint']:
            return existing, False

    path = os.path.join(reports_dir, 'data', record['run']['id'] + '.json')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as handle:
        json.dump(record, handle, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    return record, True


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as handle:
        handle.write(text if text.endswith('\n') else text + '\n')
    return path


def render_all(reports_dir):
    """Erzeugt `runs/*.md`, `latest.md` und `README.md` neu aus `data/*.json`.

    Berichte ohne Messblock werden entfernt: die Historie zeigt, was gemessen
    wurde, nicht was einmal gerendert worden ist."""
    records = load_records(reports_dir)
    if not records:
        raise SystemExit(f'{reports_dir}/data: kein archivierter Lauf — zuerst mit einem Laufpfad '
                         'aufrufen, z. B. `build_reports.py out`')

    written = []
    keep = set()
    for record in records:
        name = record['run']['id'] + '.md'
        keep.add(name)
        written.append(write(os.path.join(reports_dir, 'runs', name),
                             md.report_markdown(record, dashboard_link='../../out/dashboard.html')))

    for stale in glob.glob(os.path.join(reports_dir, 'runs', '*.md')):
        if os.path.basename(stale) not in keep:
            os.remove(stale)
            print(f'entfernt (kein Messblock mehr): {stale}')

    written.append(write(os.path.join(reports_dir, 'latest.md'),
                         md.report_markdown(records[-1], dashboard_link='../out/dashboard.html')))
    written.append(write(os.path.join(reports_dir, 'README.md'),
                         md.index_markdown([md.run_summary(r) for r in records])))
    return records, written


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('run', nargs='?', default=None,
                        help=f'Verzeichnis eines Laborlaufs (Vorgabe: {os.path.relpath(DEFAULT_RUN)})')
    parser.add_argument('--reports', default=DEFAULT_REPORTS,
                        help=f'Berichtsordner (Vorgabe: {os.path.relpath(DEFAULT_REPORTS)})')
    parser.add_argument('--regenerate', action='store_true',
                        help='nur neu rendern, nichts einlesen — braucht keinen Laufpfad')
    parser.add_argument('--no-dashboard', action='store_true',
                        help='dashboard.html nicht mitschreiben')
    args = parser.parse_args(argv)

    if not args.regenerate:
        root = args.run or DEFAULT_RUN
        record = lab_data.archive_record(root)
        stored, is_new = archive(args.reports, record)
        print(('archiviert' if is_new else 'schon archiviert, unveraendert') +
              f": {stored['run']['id']} · Fingerabdruck {stored['run']['fingerprint']}")
        if not args.no_dashboard:
            page, size = build_dashboard.build(root)
            print(f'{page} — {size // 1024} KiB')

    records, written = render_all(args.reports)
    for path in written[-2:]:
        print(os.path.relpath(path))
    print(f"{len(records)} Lauf/Laeufe in der Historie, "
          f"{os.path.relpath(os.path.join(args.reports, 'runs'))} je einer")
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
