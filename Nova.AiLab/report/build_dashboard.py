#!/usr/bin/env python3
"""Baut die Gesamtauswertung eines Laborlaufs: out/dashboard.html.

Liest die Artefakte, die `match`, `duel`, `movement` und `compare` geschrieben
haben, verdichtet sie zu einem kompakten JSON-Block (`lab_data.collect`) und
bettet den in `dashboard.tpl.html` ein. Ergebnis ist eine selbststaendige Seite
ohne Build, ohne Server und ohne Netzzugriff.

    python3 Nova.AiLab/report/build_dashboard.py out

Die Markdown-Fassung, die Historie und die Gesamtuebersicht schreibt
`build_reports.py` — es baut diese Seite mit und ist der uebliche Einstieg.

WERKZEUG, KEIN BEITRAG. Die Seite ist Diagnose; was nicht im laufenden Spiel
gesehen wurde, steht als ungesehen im PR-Text. Der Leser wird oben auf der
Seite genau daran erinnert.

Es wird bewusst nichts gerechnet, was die Laufarten nicht schon gemessen haben:
umsortieren, summieren, ausduennen — keine abgeleiteten Kennzahlen und vor allem
keine Gesamtnote (Plan 3.6: das Labor rankt nicht, es legt nebeneinander).
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import lab_data  # noqa: E402

# Das Sammeln liegt in `lab_data`, damit HTML- und Markdown-Bericht dieselben
# Zahlen sehen. Die Namen bleiben hier stehen: sie sind der bekannte Einstieg.
TRACE_KEYS = lab_data.TRACE_KEYS
read_ndjson = lab_data.read_ndjson
short = lab_data.short
collect_match = lab_data.collect_match
collect_duels = lab_data.collect_duels


def build(root, out_path=None):
    data = lab_data.collect(root)

    template_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'dashboard.tpl.html')
    with open(template_path, encoding='utf-8') as handle:
        template = handle.read()
    if '__DATA__' not in template:
        raise SystemExit(f'{template_path}: Platzhalter __DATA__ fehlt')

    # `</script>` im Datenblock wuerde den umgebenden Script-Tag schliessen.
    payload = json.dumps(data, separators=(',', ':')).replace('</script>', r'<\/script>')
    page = template.replace('__DATA__', payload)

    out_path = out_path or os.path.join(root, 'dashboard.html')
    with open(out_path, 'w', encoding='utf-8') as handle:
        handle.write(page)
    return out_path, len(page)


if __name__ == '__main__':
    directory = sys.argv[1] if len(sys.argv) > 1 else 'out'
    written, size = build(directory)
    print(f'{written} — {size // 1024} KiB')
