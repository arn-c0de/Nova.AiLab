using System.Globalization;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// Writes the recorded half of the view window (plan section 3.4): one
    /// self-contained HTML file with a canvas that loads the run's artifacts
    /// from beside it. Scrubber, single tick, switchable layers — and the
    /// per-unit half: pick one entity and follow where it walked, what it
    /// attacked, when it stood still and how it died.
    /// <para>
    /// A real window (Avalonia, SDL) was weighed and dropped: a foreign
    /// dependency and platform upkeep for no advantage over this file.
    /// </para>
    /// <para>
    /// Because browsers refuse <c>fetch</c> on <c>file://</c>, the page also
    /// accepts the files dropped onto it or picked from a dialog. That is not
    /// a workaround bolted on — it is the difference between a tool that opens
    /// with a double-click and one that needs a local web server first.
    /// </para>
    /// <para>
    /// FOUR SURFACES, and each answers a different question — the shape the
    /// Unreal Visual Logger settled on for the same job: the map says WHERE,
    /// the unit list says WHO, the detail panel says WHAT IT IS DOING, and the
    /// event band under the scrubber says WHEN IT CHANGED. A picture alone
    /// cannot answer the last one, which is why watching a match at
    /// <c>--view-every 25</c> never explained a bad wave.
    /// </para>
    /// <para>
    /// THE ROUTE IS FINER THAN THE PICTURE. Frames arrive every n ticks;
    /// <c>tracks.ndjson</c> arrives every tick. The trail is drawn from the
    /// track, so it shows the way that was walked and not the straight line
    /// between two pictures.
    /// </para>
    /// </summary>
    public static class HtmlPlayer
    {
        public const string FileName = "player.html";

        public static string Build(int mapWidth, int mapHeight, int slotCount, ulong seed)
        {
            var html = new StringBuilder(48 * 1024);
            html.Append(Template
                .Replace("__MAP_WIDTH__", mapWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("__MAP_HEIGHT__", mapHeight.ToString(CultureInfo.InvariantCulture))
                .Replace("__SLOT_COUNT__", slotCount.ToString(CultureInfo.InvariantCulture))
                .Replace("__SEED__", "0x" + seed.ToString("X", CultureInfo.InvariantCulture))
                .Replace("__VIEW_FILE__", RunArtifacts.ViewFileName)
                .Replace("__TRACKS_FILE__", RunArtifacts.TracksFileName)
                .Replace("__EVENTS_FILE__", RunArtifacts.EventsFileName)
                .Replace("__UNITS_FILE__", RunArtifacts.UnitsFileName));
            return html.ToString();
        }

        // The page is one string on purpose: an artifact directory should be
        // copyable as a unit, and a player split across files is not.
        private const string Template = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Nova AI Lab — view player (seed __SEED__)</title>
<style>
  :root { color-scheme: dark; }
  body { margin:0; background:#0d1117; color:#c9d1d9;
         font:13px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace; }
  header { padding:10px 14px; border-bottom:1px solid #21262d; }
  h1 { font-size:14px; margin:0 0 2px; font-weight:600; }
  .sub { color:#8b949e; font-size:12px; }
  .warn { color:#d29922; }
  .derived { color:#d29922; font-style:italic; }
  main { display:flex; gap:14px; padding:14px; align-items:flex-start; flex-wrap:wrap; }
  canvas#map { background:#010409; border:1px solid #21262d; border-radius:4px;
           image-rendering:pixelated; max-width:100%; cursor:crosshair; }
  aside { min-width:320px; flex:1; max-width:520px; }
  .bar { display:flex; gap:8px; align-items:center; padding:0 14px 6px; flex-wrap:wrap; }
  input[type=range] { flex:1; min-width:220px; }
  button, label.file, select { background:#21262d; color:#c9d1d9; border:1px solid #30363d;
           border-radius:4px; padding:4px 10px; cursor:pointer; font:inherit; }
  button:hover, label.file:hover { background:#30363d; }
  table { border-collapse:collapse; width:100%; font-size:12px; }
  th,td { text-align:right; padding:3px 7px; border-bottom:1px solid #21262d; }
  th:first-child, td:first-child { text-align:left; }
  .layers { margin-top:12px; display:flex; flex-direction:column; gap:4px; font-size:12px; }
  .legend { margin-top:12px; color:#8b949e; font-size:12px; line-height:1.7; }
  .sw { display:inline-block; width:10px; height:10px; border-radius:2px; vertical-align:-1px; }
  #drop { padding:14px 14px 6px; display:flex; gap:10px; align-items:center; flex-wrap:wrap; }
  #bandWrap { padding:0 14px 10px; }
  canvas#band { width:100%; height:26px; display:block; background:#010409;
                border:1px solid #21262d; border-radius:4px; cursor:pointer; }
  #unitList { max-height:260px; overflow-y:auto; border:1px solid #21262d; border-radius:4px; }
  #unitList table { font-size:12px; }
  #unitList tr { cursor:pointer; }
  #unitList tr:hover td { background:#161b22; }
  #unitList tr.sel td { background:#1f6feb33; }
  #unitList tr.dead td { opacity:0.45; }
  #detail { margin-top:10px; border:1px solid #21262d; border-radius:4px; padding:8px 10px;
            font-size:12px; min-height:96px; }
  #detail h2 { font-size:12px; margin:0 0 6px; font-weight:600; }
  #detail dl { display:grid; grid-template-columns:auto 1fr; gap:1px 10px; margin:0; }
  #detail dt { color:#8b949e; }
  #detail dd { margin:0; }
  #log { margin-top:8px; max-height:150px; overflow-y:auto; font-size:11px; color:#8b949e; }
  #log div { white-space:nowrap; }
  #log div.now { color:#c9d1d9; }
  .filters { display:flex; gap:6px; align-items:center; margin-bottom:6px; flex-wrap:wrap; font-size:12px; }
</style>
</head>
<body>
<header>
  <h1>Nova AI Lab — view player</h1>
  <div class=""sub"">seed __SEED__ · map __MAP_WIDTH__×__MAP_HEIGHT__ · __SLOT_COUNT__ slots ·
    <span class=""warn"">diagnosis, never proof — what was not seen in the running game is unseen</span></div>
</header>

<div id=""drop"">
  <label class=""file"">open the run's files<input type=""file"" id=""file"" multiple
    accept="".ndjson,.json,.txt"" hidden></label>
  <span class=""sub"" id=""status"">loading from beside this page…</span>
</div>

<div class=""bar"">
  <button id=""play"">play</button>
  <button id=""prev"">◀ tick</button>
  <button id=""next"">tick ▶</button>
  <input type=""range"" id=""scrub"" min=""0"" max=""0"" value=""0"">
  <span id=""tickLabel"" class=""sub"">—</span>
</div>

<div id=""bandWrap"">
  <canvas id=""band"" width=""2000"" height=""26""></canvas>
  <div class=""sub"" id=""bandLabel"">event band — pick a unit to see its life on the time axis
    (<b>n</b> / <b>p</b> jump to the next / previous event)</div>
</div>

<main>
  <canvas id=""map"" width=""768"" height=""768""></canvas>
  <aside>
    <table id=""headers""><thead><tr>
      <th>slot</th><th>credits</th><th>power</th><th>army</th><th>sees</th>
    </tr></thead><tbody></tbody></table>

    <div class=""layers"">
      <label><input type=""checkbox"" id=""layerLines"" checked> order lines</label>
      <label><input type=""checkbox"" id=""layerHealth"" checked> health as brightness</label>
      <label><input type=""checkbox"" id=""layerTrail"" checked> trail of the selected unit</label>
      <label><input type=""checkbox"" id=""layerAllTrails""> trails of every unit of slot
        <select id=""trailSlot""></select></label>
      <label><input type=""checkbox"" id=""layerFog""> fog of war of slot
        <select id=""fogSlot""></select></label>
      <label>trail length
        <select id=""trailSpan"">
          <option value=""200"">200 ticks</option>
          <option value=""600"" selected>600 ticks</option>
          <option value=""2000"">2000 ticks</option>
          <option value=""0"">the whole run</option>
        </select></label>
    </div>

    <div style=""margin-top:12px"">
      <div class=""filters"">
        <b>units</b>
        <select id=""filterSlot""><option value=""-1"">every slot</option></select>
        <select id=""filterShape"">
          <option value=""-1"">every shape</option>
          <option value=""4"">combat</option>
          <option value=""3"">harvester</option>
          <option value=""2"">builder</option>
          <option value=""0"">building</option>
          <option value=""1"">site</option>
        </select>
        <label><input type=""checkbox"" id=""filterDead""> show the dead</label>
      </div>
      <div id=""unitList""><table><tbody></tbody></table></div>
      <div id=""detail""><span class=""sub"">no unit selected — click one in the list or on the map</span></div>
      <div id=""log""></div>
    </div>

    <div class=""legend"">
      <b>shape</b> ▣ building · ▢ site · ✚ builder · ● harvester · ▲ combat<br>
      <b>line</b> <span class=""sw"" style=""background:#f85149""></span> attack ·
      <span class=""sw"" style=""background:#3fb950""></span> harvest ·
      <span class=""sw"" style=""background:#58a6ff""></span> move<br>
      <b>event</b> <span class=""sw"" style=""background:#f85149""></span> damage/death ·
      <span class=""sw"" style=""background:#ff9e64""></span> attack ·
      <span class=""sw"" style=""background:#58a6ff""></span> order/goal ·
      <span class=""sw"" style=""background:#d29922""></span> stuck ·
      <span class=""sw"" style=""background:#3fb950""></span> harvest/cargo ·
      <span class=""sw"" style=""background:#bc8cff""></span> spawn/site<br>
      <b>hollow</b> returning cargo · <b>white rim</b> below retreat threshold<br>
      <span class=""warn"">fog is the most common reason an AI ""did not react"" — check it before blaming the logic.</span><br>
      <span class=""derived"">who fired is DERIVED from state, never reported by the simulation — see notes/schadensquelle.md.</span>
    </div>
  </aside>
</main>

<script>
const MAP_W = __MAP_WIDTH__, MAP_H = __MAP_HEIGHT__;
const ONE = 65536;                       // Q16.16: positions arrive as raw integers
const SLOT_COLOURS = ['#58a6ff','#f85149','#3fb950','#d29922','#bc8cff','#39c5cf','#ff9e64','#8b949e'];
const LINE_COLOURS = [null,'#f85149','#3fb950','#58a6ff'];
const SHAPE_GLYPH = ['▣','▢','✚','●','▲'];
const SHAPE_NAME  = ['building','site','builder','harvester','combat'];
const ROLE_NAME = ['unit','builder','harvester','HQ','refinery','power','storage','barracks',
                   'vehicleFactory','researchLab','radar','defensePlatform','basicInfantry',
                   'antiArmorInfantry','scoutVehicle','lightTank','battleTank','artillery'];

// Few, discrete colours: an event band with twenty hues is a colour chart,
// not a reading aid.
const EVENT_COLOUR = {
  damage:'#f85149', death:'#f85149',
  attackStart:'#ff9e64', attackSwitch:'#ff9e64', attackStop:'#ff9e64',
  order:'#58a6ff', goal:'#58a6ff', moveStart:'#8b949e', moveStop:'#8b949e',
  stuck:'#d29922', unstuck:'#d29922',
  harvestStart:'#3fb950', harvestStop:'#3fb950', cargoFull:'#3fb950', cargoDelivered:'#3fb950',
  spawn:'#bc8cff', siteOpen:'#bc8cff', siteDone:'#bc8cff',
  heal:'#3fb950', retreatBelow:'#e6edf3', retreatAbove:'#e6edf3'
};

const canvas = document.getElementById('map'), ctx = canvas.getContext('2d');
const band = document.getElementById('band'), bctx = band.getContext('2d');
const scrub = document.getElementById('scrub'), tickLabel = document.getElementById('tickLabel');
const status = document.getElementById('status');
const fogSlot = document.getElementById('fogSlot'), trailSlot = document.getElementById('trailSlot');
const filterSlot = document.getElementById('filterSlot');

let frames = [], index = 0, playing = false, timer = null;
let tracks = new Map();        // id -> {t:[], x:[], y:[]}
let events = [], eventsById = new Map();
let units = new Map();         // id -> row of units.json
let selected = null, selectedEventIndex = -1;
let haveIds = false;           // a view.ndjson written before the id column exists

const loaded = { view:false, tracks:false, events:false, units:false };

// ---------------------------------------------------------------- loading

function parseNdjson(text) {
  return text.split('\n').filter(l => l.trim()).map(JSON.parse);
}

function loadView(text) {
  frames = parseNdjson(text);
  if (!frames.length) { status.textContent = 'no frames in the view file'; return; }
  haveIds = frames.some(f => f.e.some(e => e.length > 9));
  index = 0;
  scrub.max = frames.length - 1;
  scrub.value = 0;
  const slots = frames[0].h.map(h => h[0]);
  fogSlot.innerHTML = slots.map(s => '<option value=""' + s + '"">' + s + '</option>').join('');
  trailSlot.innerHTML = slots.map(s => '<option value=""' + s + '"">' + s + '</option>').join('');
  filterSlot.innerHTML = '<option value=""-1"">every slot</option>' +
    slots.map(s => '<option value=""' + s + '"">slot ' + s + '</option>').join('');
  loaded.view = true;
  const wanted = readHash();
  note();
  if (wanted !== null) gotoTick(wanted); else draw();
}

// The track is the whole point of the trail: it carries every tick, while a
// frame carries every n-th. Rebuilt into one array per id so drawing a route
// is a slice, not a search.
function loadTracks(text) {
  const lines = parseNdjson(text);
  tracks = new Map();
  const pos = new Map();
  for (const f of lines) {
    if (f.a) for (const s of f.a) { pos.set(s[0], [s[1], s[2]]); push(s[0], f.t, s[1], s[2]); }
    if (f.d) for (const s of f.d) {
      const p = pos.get(s[0]);
      if (!p) continue;
      p[0] += s[1]; p[1] += s[2];
      push(s[0], f.t, p[0], p[1]);
    }
    if (f.x) for (const id of f.x) pos.delete(id);
  }
  loaded.tracks = true;
  note();
  draw();

  function push(id, t, x, y) {
    let tr = tracks.get(id);
    if (!tr) { tr = { t:[], x:[], y:[] }; tracks.set(id, tr); }
    const n = tr.t.length;
    if (n && tr.t[n - 1] === t) { tr.x[n - 1] = x; tr.y[n - 1] = y; return; }
    tr.t.push(t); tr.x.push(x); tr.y.push(y);
  }
}

function loadEvents(text) {
  events = parseNdjson(text);
  eventsById = new Map();
  for (const e of events) {
    let list = eventsById.get(e.id);
    if (!list) { list = []; eventsById.set(e.id, list); }
    list.push(e);
  }
  loaded.events = true;
  note();
  draw();
}

function loadUnits(text) {
  const parsed = JSON.parse(text);
  units = new Map((parsed.units || []).map(u => [u.id, u]));
  loaded.units = true;
  note();
  draw();
}

function note() {
  const missing = Object.keys(loaded).filter(k => !loaded[k]);
  let text = frames.length
    ? frames.length + ' frames, ticks ' + frames[0].t + '…' + frames[frames.length - 1].t
    : 'no frames yet';
  if (loaded.tracks) text += ' · ' + tracks.size + ' tracked units';
  if (loaded.events) text += ' · ' + events.length + ' events';
  if (missing.length) text += ' · missing: ' + missing.join(', ');
  if (frames.length && !haveIds) {
    text += ' · this view file predates the id column — no unit can be followed in it';
  }
  status.textContent = text;
}

const FILES = [
  ['__VIEW_FILE__', loadView], ['__TRACKS_FILE__', loadTracks],
  ['__EVENTS_FILE__', loadEvents], ['__UNITS_FILE__', loadUnits]
];

// file:// blocks fetch in most browsers, so a failure here is expected and
// the file picker is the normal path, not the fallback.
for (const [name, load] of FILES) {
  fetch(name).then(r => r.ok ? r.text() : Promise.reject()).then(load).catch(() => {
    if (!loaded.view) {
      status.textContent = 'open the run files with the button (browsers block file:// reads) — ' +
        '__VIEW_FILE__, __TRACKS_FILE__, __EVENTS_FILE__, __UNITS_FILE__';
    }
  });
}

document.getElementById('file').addEventListener('change', e => {
  for (const f of e.target.files) {
    const entry = FILES.find(([name]) => f.name === name);
    if (entry) f.text().then(entry[1]);
  }
});

// ------------------------------------------------------------- geometry

const px = raw => (raw / ONE) * (canvas.width / MAP_W);
const py = raw => canvas.height - (raw / ONE) * (canvas.height / MAP_H);

function currentTick() { return frames[index] ? frames[index].t : 0; }

/** First index whose tick is >= t. */
function lowerBound(list, t) {
  let lo = 0, hi = list.length;
  while (lo < hi) { const mid = (lo + hi) >> 1; if (list[mid] < t) lo = mid + 1; else hi = mid; }
  return lo;
}

function trailRange(id, tick, span) {
  const tr = tracks.get(id);
  if (!tr || !tr.t.length) return null;
  const to = lowerBound(tr.t, tick + 1);
  const from = span > 0 ? lowerBound(tr.t, tick - span) : 0;
  return to - from < 2 ? null : { tr, from, to };
}

// ---------------------------------------------------------------- drawing

function drawFog(frame) {
  if (!frame.fog) return;
  const runs = frame.fog[+fogSlot.value];
  if (!runs) return;
  const cw = canvas.width / MAP_W, ch = canvas.height / MAP_H;
  let cell = 0;
  for (let i = 0; i < runs.length; i += 2) {
    const count = runs[i], state = runs[i + 1];
    if (state !== 2) {
      ctx.fillStyle = state === 0 ? 'rgba(0,0,0,0.82)' : 'rgba(0,0,0,0.45)';
      for (let k = 0; k < count; k++) {
        const c = cell + k, x = c % MAP_W, y = (c / MAP_W) | 0;
        ctx.fillRect(x * cw, canvas.height - (y + 1) * ch, cw + 0.5, ch + 0.5);
      }
    }
    cell += count;
  }
}

// A trail pool with a hard budget: hundreds of polylines per frame is the
// known cost of this kind of view, and a page that stutters does not get used.
const TRAIL_BUDGET = 6000;

function drawTrail(id, tick, span, colour, width, fade, budget) {
  const range = trailRange(id, tick, span);
  if (!range) return 0;
  const { tr, from, to } = range;
  const count = Math.min(to - from, budget);
  const start = to - count;

  ctx.lineWidth = width;
  ctx.lineJoin = 'round';
  ctx.strokeStyle = colour;
  if (!fade) {
    ctx.globalAlpha = 0.55;
    ctx.beginPath();
    ctx.moveTo(px(tr.x[start]), py(tr.y[start]));
    for (let i = start + 1; i < to; i++) ctx.lineTo(px(tr.x[i]), py(tr.y[i]));
    ctx.stroke();
    ctx.globalAlpha = 1;
    return count;
  }

  // Fading: the segment nearest the current tick is the brightest, so the
  // direction of travel is readable without an arrowhead.
  for (let i = start + 1; i < to; i++) {
    ctx.globalAlpha = 0.12 + 0.78 * ((i - start) / count);
    ctx.beginPath();
    ctx.moveTo(px(tr.x[i - 1]), py(tr.y[i - 1]));
    ctx.lineTo(px(tr.x[i]), py(tr.y[i]));
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
  return count;
}

function drawTrails(frame) {
  const span = +document.getElementById('trailSpan').value;
  const tick = frame.t;

  if (document.getElementById('layerAllTrails').checked) {
    const slot = +trailSlot.value;
    let budget = TRAIL_BUDGET;
    for (const e of frame.e) {
      if (e.length < 10 || e[0] !== slot || budget <= 0) continue;
      budget -= drawTrail(e[9], tick, span, SLOT_COLOURS[slot % SLOT_COLOURS.length], 1, false, budget);
    }
  }

  if (selected !== null && document.getElementById('layerTrail').checked) {
    drawTrail(selected, tick, span, '#ffffff', 1.8, true, TRAIL_BUDGET);
  }
}

function draw() {
  const frame = frames[index];
  if (!frame) return;
  ctx.fillStyle = '#010409';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  const showLines = document.getElementById('layerLines').checked;
  const showHealth = document.getElementById('layerHealth').checked;

  drawTrails(frame);

  if (showLines) {
    ctx.lineWidth = 1;
    for (const e of frame.e) {
      const line = e[6];
      if (!line) continue;
      ctx.strokeStyle = LINE_COLOURS[line];
      ctx.globalAlpha = 0.45;
      ctx.beginPath(); ctx.moveTo(px(e[2]), py(e[3])); ctx.lineTo(px(e[7]), py(e[8])); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  for (const e of frame.e) {
    const slot = e[0], shape = e[1], hp = e[4], flags = e[5];
    const cx = px(e[2]), cy = py(e[3]);
    const base = SLOT_COLOURS[slot % SLOT_COLOURS.length];
    ctx.globalAlpha = showHealth ? Math.max(0.3, hp / 100) : 1;
    ctx.fillStyle = base; ctx.strokeStyle = base; ctx.lineWidth = 1.4;

    const hollow = (flags & 1) !== 0;   // returning cargo
    const weak   = (flags & 2) !== 0;   // below retreat threshold
    const r = shape === 0 ? 5 : shape === 1 ? 4.5 : 3.2;

    ctx.beginPath();
    if (shape === 0 || shape === 1) {              // building / site
      ctx.rect(cx - r, cy - r, r * 2, r * 2);
    } else if (shape === 2) {                      // builder: cross
      ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
      ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
      ctx.stroke(); ctx.globalAlpha = 1;
      if (e.length > 9 && e[9] === selected) ring(cx, cy, r);
      continue;
    } else if (shape === 3) {                      // harvester: disc
      ctx.arc(cx, cy, r, 0, Math.PI * 2);
    } else {                                       // combat: triangle
      ctx.moveTo(cx, cy - r); ctx.lineTo(cx + r, cy + r); ctx.lineTo(cx - r, cy + r); ctx.closePath();
    }
    if (hollow || shape === 1) ctx.stroke(); else ctx.fill();

    if (weak) {
      ctx.globalAlpha = 1; ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.arc(cx, cy, r + 2.5, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.globalAlpha = 1;
    if (e.length > 9 && e[9] === selected) ring(cx, cy, r);
  }

  if (document.getElementById('layerFog').checked) drawFog(frame);

  markDeath(frame);

  const body = document.querySelector('#headers tbody');
  body.innerHTML = frame.h.map(h =>
    '<tr><td style=""color:' + SLOT_COLOURS[h[0] % SLOT_COLOURS.length] + '"">slot ' + h[0] +
    '</td><td>' + h[1] + '</td><td>' + h[2] + '</td><td>' + h[3] + '</td><td>' + h[4] + '</td></tr>').join('');

  tickLabel.textContent = 'tick ' + frame.t + '  (' + (index + 1) + '/' + frames.length + ')';
  scrub.value = index;

  renderUnits(frame);
  renderDetail(frame);
  drawBand();
}

function ring(cx, cy, r) {
  ctx.save();
  ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1.5; ctx.globalAlpha = 0.9;
  ctx.beginPath(); ctx.arc(cx, cy, r + 5, 0, Math.PI * 2); ctx.stroke();
  ctx.restore();
}

// A unit that dies does not simply vanish from the page: the spot it died on
// stays marked, because ""where did it die"" is half the question.
function markDeath(frame) {
  if (selected === null) return;
  const list = eventsById.get(selected);
  if (!list) return;
  const death = list.find(e => e.k === 'death');
  if (!death || death.t > frame.t || death.x === undefined) return;

  const cx = px(death.x), cy = py(death.y);
  ctx.save();
  ctx.strokeStyle = '#f85149'; ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(cx - 5, cy - 5); ctx.lineTo(cx + 5, cy + 5);
  ctx.moveTo(cx + 5, cy - 5); ctx.lineTo(cx - 5, cy + 5);
  ctx.stroke();
  ctx.restore();
}

// -------------------------------------------------------------- the band

function drawBand() {
  bctx.clearRect(0, 0, band.width, band.height);
  bctx.fillStyle = '#010409';
  bctx.fillRect(0, 0, band.width, band.height);
  if (!frames.length) return;

  const t0 = frames[0].t, t1 = frames[frames.length - 1].t || 1;
  const at = t => ((t - t0) / Math.max(1, t1 - t0)) * (band.width - 2) + 1;

  const list = selected === null ? null : eventsById.get(selected);
  if (list) {
    for (const e of list) {
      bctx.fillStyle = EVENT_COLOUR[e.k] || '#8b949e';
      bctx.fillRect(at(e.t) - 1, 3, 2, band.height - 10);
    }
  }

  bctx.fillStyle = '#ffffff';
  bctx.fillRect(at(currentTick()) - 1, 0, 2, band.height);

  const label = document.getElementById('bandLabel');
  label.innerHTML = selected === null
    ? 'event band — pick a unit to see its life on the time axis (<b>n</b> / <b>p</b> jump between events)'
    : '#' + selected + ': ' + (list ? list.length : 0) + ' events, ticks ' + t0 + '…' + t1 +
      ' (<b>n</b> / <b>p</b> jump between events)';
}

band.addEventListener('click', ev => {
  if (!frames.length) return;
  const rect = band.getBoundingClientRect();
  const t0 = frames[0].t, t1 = frames[frames.length - 1].t;
  const tick = t0 + ((ev.clientX - rect.left) / rect.width) * (t1 - t0);
  gotoTick(tick);
});

function gotoTick(tick) {
  let best = 0, bestDistance = Infinity;
  for (let i = 0; i < frames.length; i++) {
    const d = Math.abs(frames[i].t - tick);
    if (d < bestDistance) { bestDistance = d; best = i; }
  }
  index = best;
  draw();
}

// --------------------------------------------------------- list + detail

function renderUnits(frame) {
  const body = document.querySelector('#unitList tbody');
  if (!haveIds) {
    body.innerHTML = '<tr><td class=""sub"">this view file carries no ids — rerun the match to follow units</td></tr>';
    return;
  }

  const slotFilter = +filterSlot.value, shapeFilter = +document.getElementById('filterShape').value;
  const rows = [];
  const alive = new Set();

  for (const e of frame.e) {
    if (e.length < 10) continue;
    alive.add(e[9]);
    if (slotFilter >= 0 && e[0] !== slotFilter) continue;
    if (shapeFilter >= 0 && e[1] !== shapeFilter) continue;
    rows.push({ id:e[9], slot:e[0], shape:e[1], hp:e[4], line:e[6], dead:false });
  }

  if (document.getElementById('filterDead').checked) {
    for (const [id, u] of units) {
      if (alive.has(id) || !u.died) continue;
      if (slotFilter >= 0 && u.slot !== slotFilter) continue;
      rows.push({ id, slot:u.slot, shape:-1, hp:0, line:0, dead:true, role:u.role });
    }
  }

  rows.sort((a, b) => a.slot - b.slot || a.id - b.id);

  body.innerHTML = rows.map(r => {
    const colour = SLOT_COLOURS[r.slot % SLOT_COLOURS.length];
    const glyph = r.shape >= 0 ? SHAPE_GLYPH[r.shape] : '✖';
    const unit = units.get(r.id);
    const name = r.shape >= 0 ? SHAPE_NAME[r.shape] : (ROLE_NAME[r.role] || 'unit');
    // Short words, not pictograms: the monospace stacks this page runs in
    // fall back to a replacement box for half the symbol block, and a box
    // says nothing at all.
    const mark = r.line === 1 ? 'atk' : r.line === 2 ? 'hrv' : r.line === 3 ? 'mov' : '';
    const blocked = unit && unit.blockedTicks > 0
      ? ' <span class=""warn"">blk ' + unit.blockedTicks + '</span>' : '';
    return '<tr data-id=""' + r.id + '"" class=""' + (r.id === selected ? 'sel ' : '') + (r.dead ? 'dead' : '') + '"">' +
      '<td style=""color:' + colour + '"">' + glyph + ' #' + r.id + ' ' + name + '</td>' +
      '<td>' + (r.dead ? '†' : r.hp + '%') + '</td>' +
      '<td>' + mark + blocked + '</td></tr>';
  }).join('') || '<tr><td class=""sub"">nothing matches the filter</td></tr>';

  for (const tr of body.querySelectorAll('tr[data-id]')) {
    tr.addEventListener('click', () => select(+tr.dataset.id));
  }
}

// The selection lives in the URL fragment, so ""look at #1043 around tick
// 2200"" is a link and not a set of instructions.
function select(id) {
  selected = selected === id ? null : id;
  selectedEventIndex = -1;
  try { history.replaceState(null, '', selected === null ? '#' : '#u=' + selected); } catch (ignored) {}
  draw();
}

/** Reads ""#u=1043&t=2200"" and returns the wanted tick, or null. */
function readHash() {
  const params = new URLSearchParams((location.hash || '').replace(/^#/, ''));
  if (params.has('u')) { selected = +params.get('u'); selectedEventIndex = -1; }
  return params.has('t') ? +params.get('t') : null;
}

function describe(e) {
  switch (e.k) {
    case 'damage': return 'damage ' + e.from + '→' + e.to + by(e);
    case 'death': return 'died' + by(e);
    case 'heal': return 'healed ' + e.from + '→' + e.to;
    case 'order': return 'order ' + cell(e.fx, e.fy) + ' → ' + cell(e.tx, e.ty);
    case 'goal': return 'goal ' + cell(e.fx, e.fy) + ' → ' + cell(e.tx, e.ty);
    case 'moveStart': return 'started moving';
    case 'moveStop': return 'stopped';
    case 'attackStart': return 'attacks #' + e.target;
    case 'attackSwitch': return 'switches to #' + e.target;
    case 'attackStop': return 'stops attacking #' + e.target;
    case 'harvestStart': return 'harvests field ' + e.field;
    case 'harvestStop': return 'leaves field ' + e.field;
    case 'cargoFull': return 'cargo full (' + e.cargo + ' AE)';
    case 'cargoDelivered': return 'cargo delivered (' + e.cargo + ' AE)';
    case 'spawn': return 'spawned, ' + e.hp + ' hp';
    case 'siteOpen': return 'construction site of def ' + e.def;
    case 'siteDone': return 'finished building def ' + e.def;
    case 'stuck': return 'STUCK — moving, not moving';
    case 'unstuck': return 'moving again after ' + e.ticks + ' ticks';
    case 'retreatBelow': return 'fell under the retreat mark (' + e.hp + '%)';
    case 'retreatAbove': return 'back over the retreat mark (' + e.hp + '%)';
    default: return e.k;
  }
}

function by(e) {
  if (!e.by || !e.by.length) return '';
  const who = e.by.map(id => '#' + id).join(', ');
  return e.bySure
    ? ' <span class=""derived"">by ' + who + ' (derived)</span>'
    : ' <span class=""derived"">by one of ' + who + ' (derived, ambiguous)</span>';
}

function cell(x, y) { return x < 0 || y < 0 ? '—' : x + ',' + y; }

function renderDetail(frame) {
  const detail = document.getElementById('detail'), log = document.getElementById('log');
  if (selected === null) {
    detail.innerHTML = '<span class=""sub"">no unit selected — click one in the list or on the map</span>';
    log.innerHTML = '';
    return;
  }

  const live = frame.e.find(e => e.length > 9 && e[9] === selected);
  const unit = units.get(selected);
  const list = eventsById.get(selected) || [];
  const rows = [];

  rows.push(['id', '#' + selected]);
  if (unit) {
    rows.push(['slot', unit.slot + ' · ' + (ROLE_NAME[unit.role] || 'unit')]);
    rows.push(['life', 'tick ' + unit.firstTick + '…' + unit.lastTick + (unit.died ? ' · died' : '')]);
  }
  if (live) {
    rows.push(['health', live[4] + '%']);
    rows.push(['state', (live[5] & 4 ? 'moving ' : 'standing ') +
      (live[5] & 1 ? '· cargo ' : '') + (live[5] & 2 ? '· below retreat mark' : '')]);
    rows.push(['order line', live[6] === 1 ? 'attack' : live[6] === 2 ? 'harvest' :
      live[6] === 3 ? 'move to ' + cell(live[7] >> 16, live[8] >> 16) : 'none']);
  } else {
    rows.push(['at this tick', 'not on the map']);
  }
  if (unit) {
    rows.push(['walked', unit.pathLengthCells + ' cells']);
    rows.push(['detour', unit.detourPercent < 0
      ? '— (never walked towards a goal)'
      : unit.detourPercent + '% over ' + unit.segments + ' segments']);
    rows.push(['blocked', unit.blockedTicks + ' of ' + unit.movingTicks + ' moving ticks']);
    rows.push(['orders / goals', unit.orderChanges + ' / ' + unit.goalChanges]);
    rows.push(['damage taken', String(unit.damageTaken)]);
    rows.push(['dealt / kills', '<span class=""derived"">' + unit.damageDealtDerived + ' / ' +
      unit.killsDerived + ' (derived)</span>']);
  }

  detail.innerHTML = '<h2>#' + selected + '</h2><dl>' +
    rows.map(r => '<dt>' + r[0] + '</dt><dd>' + r[1] + '</dd>').join('') + '</dl>';

  const tick = frame.t;
  log.innerHTML = list.map((e, i) =>
    '<div class=""' + (e.t <= tick && (i + 1 >= list.length || list[i + 1].t > tick) ? 'now' : '') + '"" ' +
    'data-i=""' + i + '"" style=""cursor:pointer"">t' + e.t + '  ' +
    '<span style=""color:' + (EVENT_COLOUR[e.k] || '#8b949e') + '"">' + e.k + '</span>  ' +
    describe(e) + '</div>').join('');

  for (const div of log.querySelectorAll('div[data-i]')) {
    div.addEventListener('click', () => { selectedEventIndex = +div.dataset.i; gotoTick(list[selectedEventIndex].t); });
  }
  const now = log.querySelector('.now');
  if (now) now.scrollIntoView({ block:'nearest' });
}

// ------------------------------------------------------------ navigation

function step(delta) {
  index = Math.min(frames.length - 1, Math.max(0, index + delta));
  draw();
}

function jumpEvent(direction) {
  if (selected === null) return;
  const list = eventsById.get(selected);
  if (!list || !list.length) return;
  const tick = currentTick();
  const next = direction > 0
    ? list.find(e => e.t > tick)
    : [...list].reverse().find(e => e.t < tick);
  if (next) gotoTick(next.t);
}

canvas.addEventListener('click', ev => {
  const frame = frames[index];
  if (!frame || !haveIds) return;
  const rect = canvas.getBoundingClientRect();
  const x = (ev.clientX - rect.left) * (canvas.width / rect.width);
  const y = (ev.clientY - rect.top) * (canvas.height / rect.height);

  let best = null, bestDistance = 18 * 18;
  for (const e of frame.e) {
    if (e.length < 10) continue;
    const dx = px(e[2]) - x, dy = py(e[3]) - y;
    const d = dx * dx + dy * dy;
    if (d < bestDistance) { bestDistance = d; best = e[9]; }
  }
  if (best !== null) select(best); else { selected = null; draw(); }
});

scrub.addEventListener('input', () => { index = +scrub.value; draw(); });
document.getElementById('prev').addEventListener('click', () => step(-1));
document.getElementById('next').addEventListener('click', () => step(1));
for (const id of ['layerLines','layerHealth','layerFog','layerTrail','layerAllTrails',
                  'trailSpan','filterShape','filterDead']) {
  document.getElementById(id).addEventListener('change', draw);
}
fogSlot.addEventListener('change', draw);
trailSlot.addEventListener('change', draw);
filterSlot.addEventListener('change', draw);

document.getElementById('play').addEventListener('click', e => {
  playing = !playing;
  e.target.textContent = playing ? 'pause' : 'play';
  clearInterval(timer);
  if (playing) timer = setInterval(() => {
    if (index >= frames.length - 1) { index = 0; } else { index++; }
    draw();
  }, 60);
});

addEventListener('keydown', e => {
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
  if (e.key === 'ArrowRight') step(1);
  if (e.key === 'ArrowLeft') step(-1);
  if (e.key === 'n') jumpEvent(1);
  if (e.key === 'p') jumpEvent(-1);
  if (e.key === 'Escape') { selected = null; draw(); }
});
</script>
</body>
</html>
";
    }
}
