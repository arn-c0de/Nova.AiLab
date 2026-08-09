# Zwei Seiten einer Grenze

Dieses Repository steht auf der einen Seite, `VibecodingGermany/Project_Nova`
auf der anderen. Diese Datei hält fest, was beim Überqueren passiert — und was
ausdrücklich **nicht** passiert.

Die kurze Fassung:

> Das Werkzeug bleibt meins. Was ich damit am Spiel ändere und ins Hauptrepo
> einreiche, richtet sich nach den Bedingungen des Hauptrepos — aber nur das,
> und nur die eingereichten Änderungen.

## 1. Was hier bleibt

Das Labor selbst: Quelltext, Berichtsgenerator, Tests, Skripte, `reports/`.
Rechteinhaber ist arn-c0de, es gilt [LICENSE](LICENSE).

Nichts davon wird per Pull Request an Project Nova eingereicht. Das ist keine
Formalie, sondern der Grund, warum das Labor überhaupt aus dem Fork
herausgelöst wurde: Ein Messwerkzeug, das im gemessenen Branch liegt, misst
nicht mehr den Branch, den man messen wollte.

## 2. Was hinübergeht

Änderungen am Spielcode innerhalb unseres Scopes — KI, Bewegung, Kampfwerte,
Fraktionsidentität, neue Tests, ein Changelog-Eintrag. Sie gehen als Pull
Request von `arn-c0de/Project_Nova` nach `VibecodingGermany/Project_Nova`.

Dort gelten die Bedingungen des Hauptrepos, nicht meine:

| | Was gilt |
|---|---|
| **Urheberrecht** | bleibt bei mir. Weder [`NOTICE`](../Project_Nova/NOTICE) noch das CLA sehen eine Übertragung vor: „Each contributor retains copyright in their contribution." |
| **An die Empfänger des Repos** | [PolyForm Noncommercial License 1.0.0](../Project_Nova/LICENSE) — wie der übrige Projektcode |
| **An den Project Owner** | die Rechteeinräumung aus [`CONTRIBUTOR_LICENSE_AGREEMENT.md`](../Project_Nova/CONTRIBUTOR_LICENSE_AGREEMENT.md) §2: unbefristet, weltweit, nicht ausschliesslich, unwiderruflich, unterlizenzierbar, auch kommerziell und unter anderen Lizenzbedingungen |
| **Wodurch** | die angekreuzte Checkbox im Pull Request. Pro PR, nicht rückwirkend, nicht pauschal fürs Konto |

„Übergehen" ist dabei das falsche Wort und zu meinen Gunsten falsch: Ich
**verliere nichts**, ich räume Rechte ein. Dieselbe Änderung könnte ich
theoretisch anderswo erneut verwenden — praktisch tue ich das nicht, weil sie
für dieses Spiel geschrieben ist.

## 3. Der Umfang, und wo er endet

Erfasst ist **der Diff des jeweiligen Pull Requests** — die geänderten Zeilen in
den Dateien unter `Project_Nova/`, sonst nichts.

Nicht erfasst, auch nicht sinngemäss:

- dieses Repository, in keiner Datei und zu keinem Zeitpunkt
- das Labor als Werkzeug, seine Architektur, sein Berichtsformat
- frühere Beiträge ohne angekreuzte Checkbox — das CLA gilt ausdrücklich nicht
  rückwirkend
- künftige Beiträge, solange deren Checkbox nicht angekreuzt ist

**Ein Grenzfall, der wirklich vorkommt:** Messwerte und Berichtsausschnitte aus
einem Laborlauf, die ich in einen PR-Text kopiere. Sie werden damit Teil dessen,
was ich einreiche. Das ist gewollt und unproblematisch — es sind Beobachtungen
über Project Nova, und [LICENSE](LICENSE) §2 gibt sie ohnehin frei. Der
*Generator* dieser Zahlen wandert dadurch nicht mit.

## 4. Was der Maintainer mit dem Labor darf

Benutzen, ausführen, lokal anpassen, um Branches zu vermessen — unentgeltlich
und ausdrücklich erwünscht ([LICENSE](LICENSE) §2). Nicht: weitergeben,
in Project Nova aufnehmen, kommerziell verwerten.

Wer ein Artefakt des Labors weitergibt, gibt Project-Nova-Code mit weiter und
hängt an dessen Bedingungen — siehe [LICENSE](LICENSE) §3.

## 5. Wenn etwas davon nicht passt

Dann wird gefragt, nicht entschieden. Eine abweichende Regelung — etwa das Labor
doch ins Hauptrepo zu holen, oder eine andere Lizenz dafür — ist eine Absprache
zwischen dem Project Owner und mir und steht schriftlich, bevor sie gilt.

| Version | Datum | Änderung |
|---|---|---|
| 1.0.0 | 2026-08-09 | Erstfassung |
