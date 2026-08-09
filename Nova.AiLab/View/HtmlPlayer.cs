using System.Globalization;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// Writes the recorded half of the view window (plan section 3.4): one
    /// self-contained HTML file with a canvas that loads <c>view.ndjson</c>
    /// from beside it. Scrubber, single tick, switchable layers. No build, no
    /// server, no dependency.
    /// <para>
    /// A real window (Avalonia, SDL) was weighed and dropped: a foreign
    /// dependency and platform upkeep for no advantage over this file.
    /// </para>
    /// <para>
    /// Because browsers refuse <c>fetch</c> on <c>file://</c>, the page also
    /// accepts the ndjson dropped onto it or picked from a dialog. That is not
    /// a workaround bolted on — it is the difference between a tool that opens
    /// with a double-click and one that needs a local web server first.
    /// </para>
    /// </summary>
    public static class HtmlPlayer
    {
        public const string FileName = "player.html";

        public static string Build(int mapWidth, int mapHeight, int slotCount, ulong seed)
        {
            var html = new StringBuilder(16 * 1024);
            html.Append(Template
                .Replace("__MAP_WIDTH__", mapWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("__MAP_HEIGHT__", mapHeight.ToString(CultureInfo.InvariantCulture))
                .Replace("__SLOT_COUNT__", slotCount.ToString(CultureInfo.InvariantCulture))
                .Replace("__SEED__", "0x" + seed.ToString("X", CultureInfo.InvariantCulture))
                .Replace("__VIEW_FILE__", RunArtifacts.ViewFileName));
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
  main { display:flex; gap:14px; padding:14px; align-items:flex-start; flex-wrap:wrap; }
  canvas { background:#010409; border:1px solid #21262d; border-radius:4px;
           image-rendering:pixelated; max-width:100%; }
  aside { min-width:280px; flex:1; }
  .bar { display:flex; gap:8px; align-items:center; padding:0 14px 10px; flex-wrap:wrap; }
  input[type=range] { flex:1; min-width:220px; }
  button, label.file { background:#21262d; color:#c9d1d9; border:1px solid #30363d;
           border-radius:4px; padding:4px 10px; cursor:pointer; font:inherit; }
  button:hover, label.file:hover { background:#30363d; }
  table { border-collapse:collapse; width:100%; font-size:12px; }
  th,td { text-align:right; padding:3px 7px; border-bottom:1px solid #21262d; }
  th:first-child, td:first-child { text-align:left; }
  .layers { margin-top:12px; display:flex; flex-direction:column; gap:4px; font-size:12px; }
  .legend { margin-top:12px; color:#8b949e; font-size:12px; line-height:1.7; }
  .sw { display:inline-block; width:10px; height:10px; border-radius:2px; vertical-align:-1px; }
  #drop { padding:14px; }
</style>
</head>
<body>
<header>
  <h1>Nova AI Lab — view player</h1>
  <div class=""sub"">seed __SEED__ · map __MAP_WIDTH__×__MAP_HEIGHT__ · __SLOT_COUNT__ slots ·
    <span class=""warn"">diagnosis, never proof — what was not seen in the running game is unseen</span></div>
</header>

<div id=""drop"">
  <label class=""file"">open __VIEW_FILE__<input type=""file"" id=""file"" accept="".ndjson,.json,.txt"" hidden></label>
  <span class=""sub"" id=""status"">loading __VIEW_FILE__ from beside this page…</span>
</div>

<div class=""bar"">
  <button id=""play"">play</button>
  <button id=""prev"">◀ tick</button>
  <button id=""next"">tick ▶</button>
  <input type=""range"" id=""scrub"" min=""0"" max=""0"" value=""0"">
  <span id=""tickLabel"" class=""sub"">—</span>
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
      <label><input type=""checkbox"" id=""layerFog""> fog of war of slot
        <select id=""fogSlot""></select></label>
    </div>

    <div class=""legend"">
      <b>shape</b> ▣ building · ▢ site · ✚ builder · ● harvester · ▲ combat<br>
      <b>line</b> <span class=""sw"" style=""background:#f85149""></span> attack ·
      <span class=""sw"" style=""background:#3fb950""></span> harvest ·
      <span class=""sw"" style=""background:#58a6ff""></span> move<br>
      <b>hollow</b> returning cargo · <b>white rim</b> below retreat threshold<br>
      <span class=""warn"">fog is the most common reason an AI ""did not react"" — check it before blaming the logic.</span>
    </div>
  </aside>
</main>

<script>
const MAP_W = __MAP_WIDTH__, MAP_H = __MAP_HEIGHT__;
const ONE = 65536;                       // Q16.16: positions arrive as raw integers
const SLOT_COLOURS = ['#58a6ff','#f85149','#3fb950','#d29922','#bc8cff','#39c5cf','#ff9e64','#8b949e'];
const LINE_COLOURS = [null,'#f85149','#3fb950','#58a6ff'];

const canvas = document.getElementById('map'), ctx = canvas.getContext('2d');
const scrub = document.getElementById('scrub'), tickLabel = document.getElementById('tickLabel');
const status = document.getElementById('status'), fogSlot = document.getElementById('fogSlot');
let frames = [], index = 0, playing = false, timer = null;

function parse(text) {
  return text.split('\n').filter(l => l.trim()).map(JSON.parse);
}

function load(text) {
  frames = parse(text);
  if (!frames.length) { status.textContent = 'no frames in file'; return; }
  index = 0;
  scrub.max = frames.length - 1;
  scrub.value = 0;
  status.textContent = frames.length + ' frames, ticks ' +
    frames[0].t + '…' + frames[frames.length - 1].t;
  fogSlot.innerHTML = frames[0].h.map(h => '<option value=""' + h[0] + '"">' + h[0] + '</option>').join('');
  draw();
}

// file:// blocks fetch in most browsers, so a failure here is expected and
// the file picker is the normal path, not the fallback.
fetch('__VIEW_FILE__').then(r => r.ok ? r.text() : Promise.reject())
  .then(load)
  .catch(() => { status.textContent = 'open __VIEW_FILE__ with the button (browsers block file:// reads)'; });

document.getElementById('file').addEventListener('change', e => {
  const f = e.target.files[0];
  if (f) f.text().then(load);
});

const px = raw => (raw / ONE) * (canvas.width / MAP_W);
const py = raw => canvas.height - (raw / ONE) * (canvas.height / MAP_H);

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

function draw() {
  const frame = frames[index];
  if (!frame) return;
  ctx.fillStyle = '#010409';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  const showLines = document.getElementById('layerLines').checked;
  const showHealth = document.getElementById('layerHealth').checked;

  if (showLines) {
    ctx.lineWidth = 1;
    for (const e of frame.e) {
      const [slot, shape, x, y, hp, flags, line, lx, ly] = e;
      if (!line) continue;
      ctx.strokeStyle = LINE_COLOURS[line];
      ctx.globalAlpha = 0.45;
      ctx.beginPath(); ctx.moveTo(px(x), py(y)); ctx.lineTo(px(lx), py(ly)); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  for (const e of frame.e) {
    const [slot, shape, x, y, hp, flags] = e;
    const cx = px(x), cy = py(y);
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
      ctx.stroke(); ctx.globalAlpha = 1; continue;
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
  }

  if (document.getElementById('layerFog').checked) drawFog(frame);

  const body = document.querySelector('#headers tbody');
  body.innerHTML = frame.h.map(h =>
    '<tr><td style=""color:' + SLOT_COLOURS[h[0] % SLOT_COLOURS.length] + '"">slot ' + h[0] +
    '</td><td>' + h[1] + '</td><td>' + h[2] + '</td><td>' + h[3] + '</td><td>' + h[4] + '</td></tr>').join('');

  tickLabel.textContent = 'tick ' + frame.t + '  (' + (index + 1) + '/' + frames.length + ')';
  scrub.value = index;
}

function step(delta) {
  index = Math.min(frames.length - 1, Math.max(0, index + delta));
  draw();
}

scrub.addEventListener('input', () => { index = +scrub.value; draw(); });
document.getElementById('prev').addEventListener('click', () => step(-1));
document.getElementById('next').addEventListener('click', () => step(1));
for (const id of ['layerLines','layerHealth','layerFog']) {
  document.getElementById(id).addEventListener('change', draw);
}
fogSlot.addEventListener('change', draw);

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
  if (e.key === 'ArrowRight') step(1);
  if (e.key === 'ArrowLeft') step(-1);
});
</script>
</body>
</html>
";
    }
}
