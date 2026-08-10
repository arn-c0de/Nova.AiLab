/* Nova.AiLab — ein Symbolsatz fuer DOM und Leinwand.
 *
 * WARUM UEBERHAUPT. Auf der Karte war eine Einheit ein Dreieck, ein Kreis, ein
 * Kreuz oder ein Quadrat: fuenf Formen fuer achtzehn Rollen. Kampfpanzer,
 * Artillerie und Rekrut sahen gleich aus, und genau die Unterscheidung ist der
 * Grund, warum man einer Partie ueberhaupt zusieht.
 *
 * EINE QUELLE FUER BEIDE ZEICHENWEGE. Dieselben Pfaddaten werden zu <svg> im
 * Panel und zu Path2D auf der Leinwand. Ein zweiter Satz nur fuer die Karte
 * waere ein Satz, der irgendwann anders aussieht als die Legende daneben.
 *
 * ZWEI FAMILIEN.
 *   Rollen  — gefuellte Silhouetten. Auf der Karte misst eine Marke 12 bis
 *             20 Pixel; bei der Groesse traegt nur die Silhouette, eine
 *             Strichzeichnung wird zu Grau.
 *   Bedienung — Striche, 2 px, runde Enden. Die sitzen auf Knoepfen mit
 *             16 px und duerfen leicht sein.
 *
 * Alles liegt in einem 24x24-Feld. Wer etwas hinzufuegt: unten aufsetzen
 * (Gebaeude stehen auf dem Boden), Rand von 2 px lassen, keine Haarlinien —
 * was bei 12 px verschwindet, ist bei 12 px nicht da.
 */

/** name -> { d: Pfaddaten, s: 1 wenn gestrichen statt gefuellt, e: 1 fuer evenodd } */
const ICONS = {

  // ---------------------------------------------------------- Rollen: Bauten
  // Blockig, auf dem Boden stehend. Wer die Karte von oben ueberfliegt, soll
  // Gebaeude von Einheiten trennen koennen, ohne hinzusehen.

  'role.hq': { d: 'M3 12h18v8H3zM4.4 7.6h3.4V12H4.4zM10.3 7.6h3.4V12h-3.4zM16.2 7.6h3.4V12h-3.4z'
                + 'M11.3 2h1.5v6h-1.5zM12.8 2.4l5 1.8-5 1.8z' },
  'role.refinery': { d: 'M4 20V10a3 3 0 0 1 6 0v10zM14 20v-7a2.5 2.5 0 0 1 5 0v7zM10 14.5h4v2.2h-4z' },
  'role.power': { d: 'M13.8 2 5 13.6h5.4L9.4 22 19 10.2h-5.9z' },
  'role.storage': { d: 'M3 6h18v14H3zM5.4 8.4v9.2h13.2V8.4zM11 8.4h2v9.2h-2z', e: 1 },
  'role.barracks': { d: 'M2 11.4 12 5l10 6.4V20h-6.4v-5.6H8.4V20H2z' },
  'role.vehicleFactory': { d: 'M2 20v-8.2l4-3v3l4-3v3l4-3V20zM16.8 4.6h3.2V20h-3.2z' },
  'role.researchLab': { d: 'M9.6 2.6h4.8v2h-1.2v4.6l5.4 9.6a1.7 1.7 0 0 1-1.5 2.6H7a1.7 1.7 0 0 1-1.5-2.6'
                         + 'l5.3-9.6V4.6H9.6z' },
  'role.radar': { d: 'M3.4 10.9A8.6 8.6 0 0 1 16.3 3.4zM10.9 11.4h2.4V19h-2.4zM6.6 19h11v2.4h-11z' },
  'role.defensePlatform': { d: 'M3.6 15.4h16.8V21H3.6zM7.6 9h8.2v6.4H7.6zM14.6 10.9H23v2.6h-8.4z' },

  // ------------------------------------------------- Rollen: Fussvolk
  // Die Dreiecksfamilie — dieselbe Grundform, die die Karte bisher fuer
  // "Kampfeinheit" benutzt hat. Wer sie kennt, liest sie weiter.

  'role.basicInfantry': { d: 'M12 2.6 22.4 21H1.6z' },
  'role.antiArmorInfantry': { d: 'M12 2.6 22.4 21H1.6zM7.4 14.2h9.2v2.8H7.4z', e: 1 },

  // ------------------------------------------------------ Rollen: Fahrzeuge
  // Seitenansicht mit Raedern oder Ketten. Der Lauf zeigt nach rechts, immer.

  // Der Spaeher traegt die Antennenkugel, damit er nicht als kleiner Sammler
  // gelesen wird; die beiden Panzer trennen sich am Turm — rund gegen eckig —
  // und nicht an der Groesse. Groessenunterschiede verschwinden bei 13 px,
  // Formunterschiede nicht.
  'role.scoutVehicle': { d: 'M3.6 13.4h10.8l2.6 2.6v1.6H3.6zM6.6 16.4a2.4 2.4 0 1 0 0 4.8 2.4 2.4 0 0 0 0-4.8z'
                          + 'M14.4 16.4a2.4 2.4 0 1 0 0 4.8 2.4 2.4 0 0 0 0-4.8zM17.2 6.4h1.7v7.2h-1.7z'
                          + 'M18.05 1.8a2.4 2.4 0 1 0 0 4.8 2.4 2.4 0 0 0 0-4.8z' },
  'role.lightTank': { d: 'M2.2 16.6h19.6v4.2H2.2zM4.6 13h14.8v3.6H4.6z'
                       + 'M12 8.2a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zM15 10.6h7.4v1.8H15z' },
  'role.battleTank': { d: 'M1.8 15h20.4v6H1.8zM3.4 9.8h17.2v5.2H3.4zM7.8 4.4h8.6v5.4H7.8z'
                        + 'M15.6 5.8h7.6v2.9h-7.6z' },
  'role.artillery': { d: 'M3.2 15h11v4.2h-11zM6 18a2.6 2.6 0 1 0 0 5.2 2.6 2.6 0 0 0 0-5.2z'
                       + 'M12.4 18a2.6 2.6 0 1 0 0 5.2 2.6 2.6 0 0 0 0-5.2zM9.4 15.2 20.8 3.8l2.6 2.4-11.4 11.4z' },

  // ------------------------------------------------------- Rollen: Zivilisten
  'role.builder': { d: 'M4.6 3.6h14.8V9h-5.2v11.4h-4.4V9H4.6z' },
  'role.harvester': { d: 'M2 7h12.2v8.4H2zM14.8 9.6h3.2l3 4.2v1.6h-6.2zM5.6 15.2a2.7 2.7 0 1 0 0 5.4 '
                       + '2.7 2.7 0 0 0 0-5.4zM17 15.2a2.7 2.7 0 1 0 0 5.4 2.7 2.7 0 0 0 0-5.4z' },
  'role.unit': { d: 'M12 5.4a6.6 6.6 0 1 0 0 13.2 6.6 6.6 0 0 0 0-13.2z' },

  // Eine Baustelle ist noch nichts — vier Ecken, mehr behauptet sie nicht.
  'role.site': { d: 'M3 3h7.2v2.6H5.6V10H3zM13.8 3H21v7h-2.6V5.6h-4.6zM3 14h2.6v4.4h4.6V21H3z'
                  + 'M21 14v7h-7.2v-2.6h4.6V14z' },

  // Eine Rolle, die das Spiel kennt und dieses Kit nicht: sichtbar unbekannt
  // statt still nichts. Wer das sieht, weiss, dass hier ein Symbol fehlt.
  'role.unknown': { d: 'M12 1.8 22.2 12 12 22.2 1.8 12zM12 5.4 5.4 12l6.6 6.6 6.6-6.6z'
                     + 'M10.9 10.2h2.2v2.2h-2.2z', e: 1 },

  // ------------------------------------------------------------- Transport
  // Gefuellt: Abspielknoepfe sind ueberall auf der Welt gefuellte Dreiecke.
  'play':      { d: 'M7 4.5 20 12 7 19.5z' },
  'pause':     { d: 'M7 4.5h4.2v15H7zM12.8 4.5H17v15h-4.2z' },
  'first':     { d: 'M4.6 4.5h2.6v15H4.6zM20.4 4.5 8.4 12l12 7.5z' },
  'last':      { d: 'M16.8 4.5h2.6v15h-2.6zM3.6 4.5 15.6 12l-12 7.5z' },
  'stepBack':  { d: 'M15.6 4.5 5.6 12l10 7.5z' },
  'stepFwd':   { d: 'M8.4 4.5 18.4 12l-10 7.5z' },
  'jumpBack':  { d: 'M11.6 4.5 3 12l8.6 7.5zM21 4.5 12.4 12l8.6 7.5z' },
  'jumpFwd':   { d: 'M12.4 4.5 21 12l-8.6 7.5zM3 4.5 11.6 12 3 19.5z' },

  // --------------------------------------------------------------- Bedienung
  'zoomIn':  { d: 'M10.8 3.6a7.2 7.2 0 1 0 0 14.4 7.2 7.2 0 0 0 0-14.4zM16 16l4.6 4.6M10.8 7.9v5.8M7.9 10.8h5.8', s: 1 },
  'zoomOut': { d: 'M10.8 3.6a7.2 7.2 0 1 0 0 14.4 7.2 7.2 0 0 0 0-14.4zM16 16l4.6 4.6M7.9 10.8h5.8', s: 1 },
  'fit':     { d: 'M4 9.4V4h5.4M20 9.4V4h-5.4M4 14.6V20h5.4M20 14.6V20h-5.4', s: 1 },
  'focus':   { d: 'M12 2.6v3.6M12 17.8v3.6M2.6 12h3.6M17.8 12h3.6M12 7.6a4.4 4.4 0 1 0 0 8.8 4.4 4.4 0 0 0 0-8.8z', s: 1 },
  'panel':   { d: 'M3.4 4.4h17.2v15.2H3.4zM14.6 4.4v15.2', s: 1 },
  'file':    { d: 'M3.6 5.4h6l2 2.2h8.8v11H3.6z', s: 1 },
  'grip':    { d: 'M10 5v14M14 5v14', s: 1 },
  'expand':  { d: 'M6 10l6 6 6-6', s: 1 },
  'collapse':{ d: 'M6 14l6-6 6 6', s: 1 },
  'chevron': { d: 'M9 5l7 7-7 7', s: 1 },

  // ------------------------------------------------------------- Navigation
  'map':     { d: 'M3 6.4 9 4.2l6 2.2 6-2.2v13.4l-6 2.2-6-2.2-6 2.2zM9 4.2v13.4M15 6.4v13.4', s: 1 },
  'plus':    { d: 'M12 4.6v14.8M4.6 12h14.8', s: 1 },
  'list':    { d: 'M4 6.4h16M4 12h16M4 17.6h16', s: 1 },
  'history': { d: 'M12 3.6a8.4 8.4 0 1 1-8.4 8.4M3.6 12H1M12 7.4V12l3.4 2.2M3.6 8.4 3.6 12l3.6 0', s: 1 },
  'compare': { d: 'M12 3v18M4.4 7.4h6M13.6 7.4h6M7.4 7.4 4 14.4h6.8zM16.6 7.4 13.2 14.4H20z', s: 1 },
  'layers':  { d: 'M12 3.2 3 8l9 4.8L21 8zM3 13.2l9 4.8 9-4.8', s: 1 },
  'legend':  { d: 'M3.6 5h6a2.4 2.4 0 0 1 2.4 2.4v12a2.4 2.4 0 0 0-2.4-2.4h-6zM20.4 5h-6A2.4 2.4 0 0 0 12 7.4'
                + 'v12a2.4 2.4 0 0 1 2.4-2.4h6z', s: 1 },
  'gauge':   { d: 'M3.6 17.4a9 9 0 1 1 16.8 0M12 12.6l4.4-4', s: 1 },

  // ----------------------------------------------------------------- Zustand
  'ok':      { d: 'M4.4 12.6 9.6 18 19.6 6.4', s: 1 },
  'fail':    { d: 'M6 6l12 12M18 6 6 18', s: 1 },
  'warn':    { d: 'M12 3.4 22 20.6H2zM12 9.4v4.8M12 16.6v1.8', s: 1 },
  'spinner': { d: 'M12 3.4v4M12 16.6v4M3.4 12h4M16.6 12h4M6 6l2.8 2.8M15.2 15.2 18 18M18 6l-2.8 2.8M8.8 15.2 6 18', s: 1 },

  // --------------------------------------------------- Was eine Einheit tut
  // Kein Fadenkreuz: das ist schon "focus". Ein Einschlag ist ein Stern.
  'act.attack':  { d: 'M12 2.4 14.3 9.7 21.6 12 14.3 14.3 12 21.6 9.7 14.3 2.4 12 9.7 9.7z' },
  'act.harvest': { d: 'M12 3.2c4.2 3.4 6.2 6.4 6.2 9.2a6.2 6.2 0 0 1-12.4 0c0-2.8 2-5.8 6.2-9.2z' },
  'act.move':    { d: 'M4 12h13M12.4 7.2 17.6 12l-5.2 4.8', s: 1 },
  'act.stuck':   { d: 'M12 3.4 22 20.6H2zM12 9.4v4.8M12 16.6v1.8', s: 1 }
};

/** Welche Rolle welches Symbol traegt — Reihenfolge wie ROLE_NAME im Player. */
const ROLE_ICON = ['role.unit', 'role.builder', 'role.harvester', 'role.hq', 'role.refinery',
                   'role.power', 'role.storage', 'role.barracks', 'role.vehicleFactory',
                   'role.researchLab', 'role.radar', 'role.defensePlatform', 'role.basicInfantry',
                   'role.antiArmorInfantry', 'role.scoutVehicle', 'role.lightTank',
                   'role.battleTank', 'role.artillery'];

/** Symbolname einer Rolle; eine unbekannte Rolle wird sichtbar unbekannt. */
function roleIconName(role, isSite) {
  if (isSite) return 'role.site';
  return ROLE_ICON[role] || 'role.unknown';
}

/**
 * Das Symbol als SVG-Zeichenkette. `currentColor` ueberall, damit die Farbe
 * dort entschieden wird, wo das Symbol steht, und nicht hier.
 */
function iconSvg(name, size, extra) {
  const icon = ICONS[name] || ICONS['role.unknown'];
  const px = size || 16;
  const paint = icon.s
    ? 'fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"'
    : 'fill="currentColor"' + (icon.e ? ' fill-rule="evenodd"' : '');
  return '<svg class="i" viewBox="0 0 24 24" width="' + px + '" height="' + px +
         '" aria-hidden="true"' + (extra ? ' ' + extra : '') + '><path d="' + icon.d + '" ' + paint + '/></svg>';
}

/** Dasselbe fuer die Leinwand, einmal gebaut und behalten. */
const ICON_PATHS = new Map();
function iconPath2D(name) {
  if (typeof Path2D === 'undefined') return null;
  let path = ICON_PATHS.get(name);
  if (path === undefined) {
    const icon = ICONS[name] || ICONS['role.unknown'];
    path = new Path2D(icon.d);
    ICON_PATHS.set(name, path);
  }
  return path;
}

/** Ob das Symbol mit gerader/ungerader Regel gefuellt werden muss. */
function iconEvenOdd(name) {
  const icon = ICONS[name] || ICONS['role.unknown'];
  return !!icon.e;
}

/** Die acht Sitzfarben, wie tokens.css sie setzt — fuer Leinwand und Inline-Stil.
 *
 * EINMAL AUFGELOEST, DANN BEHALTEN. `getComputedStyle` loest erst den Stil auf
 * und antwortet dann; der Player fragt die Farbe eines Sitzes fuer jede
 * Einheit, jede Listenzeile und jede Protokollzeile — beim Abspielen
 * sechzehnmal in der Sekunde. Die Seiten setzen ihr `data-theme` fest und
 * bieten keinen Umschalter, die Antwort kann sich also nicht aendern.
 *
 * Der Modulo faengt auch negative Slots: `-1 % 8` ist in JS `-1`, und ein
 * Zugriff daneben waere `undefined` statt einer Farbe.
 */
const SLOT_COLOURS = new Map();
function slotColour(slot) {
  const index = (((slot % 8) + 8) % 8);
  let colour = SLOT_COLOURS.get(index);
  if (colour === undefined) {
    colour = getComputedStyle(document.documentElement)
      .getPropertyValue('--slot-' + index).trim() || '#8b949e';
    SLOT_COLOURS.set(index, colour);
  }
  return colour;
}
