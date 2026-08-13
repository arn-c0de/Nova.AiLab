using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// The admin panel: what every unit is trying to do, and the buttons that
    /// take the decision away from the AI.
    /// <para>
    /// IT IS DELIBERATELY NOT THE PLAYER. <c>player.html</c> is a recording —
    /// scrubbing, trails, fog, a whole map — and every one of those is about
    /// looking BACK. This page is about a match that is standing still right
    /// now, and the question it answers is "what will she do next, and what if I
    /// stop her". Folding both into one page would have meant one of the two
    /// answering the other's question badly.
    /// </para>
    /// <para>
    /// One file, the shared kit written in, nothing fetched but its own
    /// endpoints — the same rules the player holds to, for the same reason.
    /// </para>
    /// </summary>
    public static class LivePage
    {
        public static string Build(MatchSpec spec)
        {
            var html = new StringBuilder(Template, 48 * 1024);
            html.Replace("__SEED__", "0x" + spec.Seed.ToString("X"))
                .Replace("__BUDGET__", spec.TickBudget.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("__ROLE_NAMES__", UiRoles.JsArray())
                .Replace("__UIKIT_CSS__", Kit("uikit.tokens.css"));
            return html.ToString();
        }

        private static string Kit(string name)
        {
            Assembly assembly = typeof(LivePage).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "the UI kit resource '" + name + "' is not in the assembly");
                }
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }

        private const string Template = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Nova.AiLab — live</title>
<style>__UIKIT_CSS__</style>
<style>
  * { box-sizing: border-box; }
  body { margin:0; font:13px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace;
         background:var(--bg); color:var(--ink); }
  header { display:flex; gap:10px; align-items:center; flex-wrap:wrap;
           padding:10px 14px; border-bottom:1px solid var(--line); background:var(--bg2); }
  h1 { font-size:14px; margin:0 10px 0 0; font-weight:600; }
  button, select { font:inherit; background:var(--bg3); color:var(--ink);
                   border:1px solid var(--line); border-radius:6px; padding:4px 10px; cursor:pointer; }
  button:hover { border-color:var(--ink2); }
  button.on { background:var(--accent); border-color:var(--accent); color:#0b0f14; }
  .chip { border:1px solid var(--line); border-radius:999px; padding:2px 10px; color:var(--ink2); }
  .chip.bad { color:var(--bad); border-color:var(--bad); }
  main { display:grid; grid-template-columns: 1fr 1fr; gap:14px; padding:14px; align-items:start; }
  @media (max-width: 900px) { main { grid-template-columns: 1fr; } }
  section { border:1px solid var(--line); border-radius:8px; background:var(--bg2); overflow:hidden; }
  h2 { font-size:12px; margin:0; padding:8px 12px; background:var(--bg3);
       border-bottom:1px solid var(--line); color:var(--ink2); text-transform:uppercase; letter-spacing:.06em; }
  table { width:100%; border-collapse:collapse; }
  th, td { text-align:left; padding:5px 10px; border-bottom:1px solid var(--line); white-space:nowrap; }
  th { color:var(--ink2); font-weight:500; }
  tr.sel td { background:var(--bg3); }
  tr[data-id] { cursor:pointer; }
  .num { text-align:right; font-variant-numeric:tabular-nums; }
  .sub { color:var(--ink2); }
  .ok { color:var(--good); } .warn { color:var(--warn); } .bad { color:var(--bad); }
  .seat { padding:10px 12px; border-bottom:1px solid var(--line); }
  .seat b { font-size:13px; }
  .bar { height:6px; border-radius:3px; background:var(--bg3); margin-top:6px; overflow:hidden; }
  .bar i { display:block; height:100%; background:var(--accent); }
  .foot { padding:10px 12px; color:var(--ink2); }
  .goal-retreat { color:var(--bad); } .goal-attack { color:var(--warn); }
  .goal-hold { color:var(--ink2); } .goal-advance { color:var(--accent); }
  .goal-defendHome { color:var(--bad); font-weight:600; }
  .goal-reinforce { color:var(--warn); font-style:italic; }
</style>
</head>
<body>
<header>
  <h1>Nova.AiLab · live</h1>
  <button id=""playPause"">pause</button>
  <button data-step=""1"">+1</button>
  <button data-step=""20"">+20 (one decision)</button>
  <button data-step=""200"">+200</button>
  <select id=""speed"">
    <option value=""10"">10 ticks/s</option>
    <option value=""20"" selected>20 ticks/s</option>
    <option value=""60"">60 ticks/s</option>
    <option value=""200"">200 ticks/s</option>
  </select>
  <span class=""chip"" id=""tick"">tick —</span>
  <span class=""chip"" id=""seedChip"">seed __SEED__</span>
  <span class=""chip bad"" id=""intervened"" hidden>intervened — not a measurement</span>
  <button id=""save"">write goals.ndjson + overrides.ndjson</button>
  <span class=""sub"" id=""saved""></span>
</header>

<main>
  <section>
    <h2>seats — what the army decided</h2>
    <div id=""seats""></div>
    <div class=""foot"" id=""seatFoot"">—</div>
  </section>

  <section>
    <h2>force a goal</h2>
    <div class=""seat"">
      <div id=""picked"" class=""sub"">no unit picked — click a row below</div>
      <div style=""margin-top:8px; display:flex; gap:6px; flex-wrap:wrap"">
        <button data-goal=""1"">retreat</button>
        <button data-goal=""2"">attack</button>
        <button data-goal=""3"">hold</button>
        <button data-goal=""4"">advance</button>
        <button data-goal=""5"">defend home</button>
        <button data-goal=""6"">reinforce</button>
        <button data-goal=""0"">release</button>
      </div>
      <div class=""sub"" style=""margin-top:8px"">
        A forced goal takes effect on the NEXT tick and is written down with that tick,
        so the session can be replayed from <code>overrides.ndjson</code> and has to come out bit for bit
        the same. Anything else would make this panel a way of producing runs nobody can check.
      </div>
    </div>
    <h2>interventions</h2>
    <table><tbody id=""log""></tbody></table>
  </section>

  <section style=""grid-column:1/-1"">
    <h2>units — goal, and the numbers its condition weighed</h2>
    <table>
      <thead><tr>
        <th>id</th><th>slot</th><th>role</th><th>goal</th><th>forced</th>
        <th class=""num"">life</th><th class=""num"">threat</th>
        <th class=""num"">to staging</th><th class=""num"">to home</th><th>cell</th>
      </tr></thead>
      <tbody id=""units""></tbody>
    </table>
  </section>
</main>

<script>
const GOALS = ['—', 'retreat', 'attack', 'hold', 'advance', 'defendHome', 'reinforce'];
const WAVE_MODE = ['off', 'units', 'points'];
/*
 * GENERATED FROM UnitRole, not typed out here. This table used to be a hand
 * written map of numbers to names and four of its five combat entries were off
 * by two: role 12 is BasicInfantry and read ""tank"", role 13 is
 * AntiArmorInfantry and read ""artillery"", and 10 and 11 — labelled infantry
 * and anti-armour — are Radar and DefensePlatform. Nothing could go red over
 * it, because a name table has nothing to disagree with unless something asks
 * the enum. UiRoles asks it, for this page and the player both.
 */
const ROLE_NAME = __ROLE_NAMES__;
let picked = null, state = null;

const $ = id => document.getElementById(id);

async function call(path) {
  const response = await fetch(path);
  if (!response.ok) return;
  render(await response.json());
}

function render(next) {
  state = next;
  $('tick').textContent = 'tick ' + next.tick + ' / ' + next.budget +
    (next.decided ? ' · decided: ' + next.outcome + ' (slot ' + next.winnerSlot + ')' : '');
  $('playPause').textContent = next.paused ? 'run' : 'pause';
  $('playPause').classList.toggle('on', next.paused);
  $('intervened').hidden = !next.intervened;
  $('speed').value = String(next.speed);

  $('seats').innerHTML = next.seats.map(seat => {
    if (seat.decided < 0) return card(seat, '<span class=""sub"">has not decided yet</span>');
    if (!seat.engages) return card(seat, '<span class=""sub"">below the squad threshold — no goal is handed out</span>');
    const unit = WAVE_MODE[seat.waveMode];
    const gap = seat.threshold - (seat.waveMode === 2 ? seat.strength : seat.gathered);
    const have = seat.waveMode === 2 ? seat.strength : seat.gathered;
    const share = seat.threshold > 0 ? Math.min(100, Math.round(have * 100 / seat.threshold)) : 100;
    return card(seat,
      '<div>wave ' + have + ' / ' + seat.threshold + ' ' + unit + ' ' +
        (seat.waveReady ? '<span class=""ok"">marches</span>' : '<span class=""warn"">waits</span>') +
        (gap > 0 ? ' <span class=""sub"">· ' + gap + ' short</span>' : '') + '</div>' +
      '<div class=""bar""><i style=""width:' + share + '%""></i></div>' +
      '<div class=""sub"">in the ring ' + seat.gathered + ' · out with a wave ' + seat.committed +
        ' · target ' + (seat.target ? '#' + seat.target : 'none') +
        ' · marching on ' + seat.move.join(',') + ' · staging ' + seat.staging.join(',') + '</div>');
  }).join('');
  $('seatFoot').textContent = 'The numbers are the AI’s own, taken at the decision — nothing here is re-derived.';

  $('units').innerHTML = next.units.filter(u => !u.site).map(u => {
    const goal = u.judged ? '<span class=""goal-' + GOALS[u.goal] + '"">' + GOALS[u.goal] + '</span>'
                          : '<span class=""sub"">none</span>';
    return '<tr data-id=""' + u.id + '"" class=""' + (picked === u.id ? 'sel' : '') + '"">' +
      '<td>#' + u.id + '</td><td>' + u.slot + '</td><td>' + (ROLE_NAME[u.role] || u.role) + '</td>' +
      '<td>' + goal + '</td>' +
      '<td>' + (u.forced ? '<span class=""bad"">' + GOALS[u.forced] + '</span>' : '<span class=""sub"">—</span>') + '</td>' +
      '<td class=""num"">' + Math.round(u.hp * 100 / Math.max(1, u.hpMax)) + '%</td>' +
      '<td class=""num"">' + fmt(u.threat) + '</td>' +
      '<td class=""num"">' + fmt(u.toStaging) + '</td>' +
      '<td class=""num"">' + fmt(u.toHome) + '</td>' +
      '<td class=""sub"">' + u.x + ',' + u.y + '</td></tr>';
  }).join('');

  $('log').innerHTML = next.overrides.slice().reverse().map(o =>
    '<tr><td class=""sub"">tick ' + o.t + '</td>' +
    '<td>' + (o.id ? '#' + o.id : 'slot ' + o.slot) + '</td>' +
    '<td>' + (o.goal ? GOALS[o.goal] : '<span class=""sub"">released</span>') + '</td></tr>').join('')
    || '<tr><td class=""sub"">nobody has intervened — this is still a measurement</td></tr>';

  const unit = picked === null ? null : next.units.find(u => u.id === picked);
  $('picked').innerHTML = unit
    ? '#' + unit.id + ' ' + (ROLE_NAME[unit.role] || unit.role) + ' of slot ' + unit.slot +
      ' · goal ' + (unit.judged ? GOALS[unit.goal] : 'none')
    : '<span class=""sub"">no unit picked — a goal would then apply to EVERY unit of every seat</span>';
}

const fmt = v => v === undefined || v < 0 ? '<span class=""sub"">—</span>' : v;

function card(seat, body) {
  return '<div class=""seat""><b>slot ' + seat.slot + ' · ' + seat.faction + '</b> ' +
    '<span class=""sub"">· ' + seat.credits + ' AE · power ' + seat.power +
    ' · decided at tick ' + (seat.decided < 0 ? '—' : seat.decided) + '</span>' + body + '</div>';
}

$('playPause').onclick = () => call('/live/pause?on=' + (state && state.paused ? '0' : '1'));
$('speed').onchange = e => call('/live/speed?value=' + e.target.value);
$('save').onclick = async () => { $('saved').textContent = await (await fetch('/live/save')).text(); };
for (const button of document.querySelectorAll('[data-step]')) {
  button.onclick = () => call('/live/step?ticks=' + button.dataset.step);
}
for (const button of document.querySelectorAll('[data-goal]')) {
  button.onclick = () => call('/live/override?goal=' + button.dataset.goal +
    '&id=' + (picked === null ? 0 : picked) + '&slot=-1');
}
$('units').onclick = e => {
  const row = e.target.closest('tr[data-id]');
  if (!row) return;
  const id = +row.dataset.id;
  picked = picked === id ? null : id;
  if (state) render(state);
};

call('/live/state');
setInterval(() => call('/live/state'), 500);
</script>
</body>
</html>";
    }
}
