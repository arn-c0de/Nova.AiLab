using System;
using System.Globalization;
using System.Text;
using Nova.AI;
using Nova.Simulation.Combat;
using Nova.Simulation.Definitions;
using Nova.Simulation.State;

namespace Nova.AiLab
{
    /// <summary>
    /// Writes the recorded half of the view window (plan section 3.4): one
    /// self-contained HTML file that loads the run's artifacts from beside it
    /// and plays the match back — forwards, backwards, and TICK BY TICK.
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
    /// FIVE SURFACES, each answering a different question — the shape the
    /// Unreal Visual Logger settled on for the same job: the map says WHERE,
    /// the unit list says WHO, the detail panel says WHAT IT IS DOING, the
    /// event band says WHEN IT CHANGED, and the match log says WHAT ELSE
    /// HAPPENED AT THE SAME TIME. The last one is the difference between
    /// watching one unit die and understanding why.
    /// </para>
    /// <para>
    /// THE SCRUBBER RUNS ON TICKS, NOT ON FRAMES. Frames arrive every n ticks
    /// and would make stepping jump in blocks of n; the track carries every
    /// tick and the events carry the exact tick they happened at, so the page
    /// REBUILDS the state at any tick instead of showing the nearest picture.
    /// Position, health, orders, targets and flags are exact. Two things
    /// cannot be: the fog layer and the per-slot header row exist only in the
    /// frames, so both come from the nearest frame at or before the tick and
    /// the page says which one.
    /// </para>
    /// <para>
    /// AND IT CHECKS ITSELF. On load the reconstruction is compared against
    /// every recorded frame — position, shape, flags, health. The result
    /// stands in the status line. A viewer that quietly disagrees with the
    /// file it was built from is worse than no viewer.
    /// </para>
    /// </summary>
    public static class HtmlPlayer
    {
        public const string FileName = "player.html";

        public static string Build(int mapWidth, int mapHeight, int slotCount, ulong seed)
        {
            return Build(mapWidth, mapHeight, seed, null);
        }

        /// <param name="slots">
        /// The seats of the run, in slot order — the page names the faction and
        /// the profile per slot and weighs each side with the game's own
        /// strength formula. Null keeps the page readable without them: the top
        /// bar then counts units and says that it cannot weigh them.
        /// </param>
        public static string Build(int mapWidth, int mapHeight, ulong seed, SlotSpec[] slots)
        {
            int slotCount = slots != null ? slots.Length : 0;
            var html = new StringBuilder(64 * 1024);
            html.Append(Template
                .Replace("__MAP_WIDTH__", mapWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("__MAP_HEIGHT__", mapHeight.ToString(CultureInfo.InvariantCulture))
                .Replace("__SLOT_COUNT__", slotCount.ToString(CultureInfo.InvariantCulture))
                .Replace("__SEED__", "0x" + seed.ToString("X", CultureInfo.InvariantCulture))
                .Replace("__SLOTS_JSON__", BuildSlotsJson(slots))
                .Replace("__WEAPONS_JSON__", BuildWeaponsJson(slots))
                .Replace("__WAVE_JSON__", BuildWaveJson(slots))
                .Replace("__VIEW_FILE__", RunArtifacts.ViewFileName)
                .Replace("__TRACKS_FILE__", RunArtifacts.TracksFileName)
                .Replace("__EVENTS_FILE__", RunArtifacts.EventsFileName)
                .Replace("__UNITS_FILE__", RunArtifacts.UnitsFileName));
            return html.ToString();
        }

        /// <summary>
        /// The numbers the wave gate decides on, per seat — so the page can
        /// say what the AI is waiting for instead of only what it has.
        /// <para>
        /// A GATHERED ARMY IS NOT ""EVERY COMBAT UNIT"". The AI counts what
        /// stands inside the ring around its own HQ
        /// (<c>StagingDistanceCells + StagingToleranceCells</c>); everything
        /// outside it marched with an earlier wave and is never called back.
        /// Without the ring radius the page could only show a total, and a
        /// total cannot explain a wave that does not launch.
        /// </para>
        /// <para>
        /// <c>produced</c> is the full-health strength of the role the Barracks
        /// queues, <c>producerRole</c> the role that has to be standing for
        /// production to count at all — the two inputs of the gate's ceiling
        /// clause. All of it is profile and definition data, and none of it is
        /// in the artifacts.
        /// </para>
        /// </summary>
        private static string BuildWaveJson(SlotSpec[] slots)
        {
            if (slots == null) return "[]";
            var json = new StringBuilder(128 * slots.Length);
            json.Append('[');
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) json.Append(',');
                SlotSpec seat = slots[i];
                // A seat built by hand can carry the default struct, whose
                // wave values are all zero — that would read as ""the gate is
                // off"" instead of ""nobody said"". The shipped profile is what
                // MatchSpec.DefaultSlots puts there, so it is also the honest
                // stand-in here.
                AiFactionProfile profile = seat.Profile.Profile.WaveSize > 0
                    ? seat.Profile
                    : SlotSpec.CanonicalProfile(seat.Faction);
                json.Append("{\"slot\":").Append(seat.Slot)
                    .Append(",\"ring\":")
                        .Append(profile.Profile.StagingDistanceCells + profile.Profile.StagingToleranceCells)
                    .Append(",\"waveSize\":").Append(profile.Profile.WaveSize)
                    .Append(",\"points\":").Append(profile.Profile.WaveStrengthPoints)
                    .Append(",\"cap\":").Append(profile.TargetArmySize)
                    .Append(",\"cadence\":").Append(profile.Profile.DecisionTickInterval)
                    .Append(",\"produced\":")
                        .Append(CombatStrength.OfFullHealth(seat.Faction, ProducedCombatRole))
                    .Append(",\"producerRole\":").Append((int)UnitRole.Barracks)
                    .Append('}');
            }
            json.Append(']');
            return json.ToString();
        }

        /// <summary>
        /// The role the Barracks queues — <c>SkirmishAiSystem.ProducedCombatRole</c>,
        /// which is private there. Named here so the page's ceiling clause uses
        /// the same unit the AI counts on, and so a change over there is one
        /// grep away rather than a silent disagreement.
        /// </summary>
        private const UnitRole ProducedCombatRole = UnitRole.BasicInfantry;

        /// <summary>Who sat in which seat: slot, faction, profile id.</summary>
        private static string BuildSlotsJson(SlotSpec[] slots)
        {
            if (slots == null) return "[]";
            var json = new StringBuilder(64 * slots.Length);
            json.Append('[');
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) json.Append(',');
                SlotSpec s = slots[i];
                json.Append("{\"slot\":").Append(s.Slot)
                    .Append(",\"faction\":\"").Append(s.Faction.ToString().ToLowerInvariant())
                    .Append("\",\"profile\":\"").Append(s.IsAi ? s.ProfileId ?? "unknown" : "none")
                    .Append("\"}");
            }
            json.Append(']');
            return json.ToString();
        }

        /// <summary>
        /// The two numbers <see cref="Nova.AI.CombatStrength"/> multiplies and
        /// divides by, per slot and role: attack damage and firing interval.
        /// <para>
        /// THE NUMBERS TRAVEL, THE FORMULA IS ONE LINE. The page must be able
        /// to say which side is stronger — a head count calls twelve Legion
        /// recruits the same wave as twelve Alliance riflemen, which is the
        /// error <c>CombatStrength</c> exists to correct. Shipping the table
        /// instead of the values keeps the definitions the single source: the
        /// page cannot invent a strength for a role the game does not arm, and
        /// a definition change lands in the next generated player by itself.
        /// </para>
        /// <para>
        /// Per SLOT and not per faction, because that is how the page indexes
        /// it — a unit knows its slot, and the seat knows the faction.
        /// </para>
        /// </summary>
        private static string BuildWeaponsJson(SlotSpec[] slots)
        {
            if (slots == null) return "[]";
            var roles = (UnitRole[])Enum.GetValues(typeof(UnitRole));
            int highest = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                if ((int)roles[i] > highest) highest = (int)roles[i];
            }

            var json = new StringBuilder(256 * slots.Length);
            json.Append('[');
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) json.Append(',');
                json.Append('[');
                for (int role = 0; role <= highest; role++)
                {
                    if (role > 0) json.Append(',');
                    WeaponProfile weapon = WeaponProfiles.Get(slots[i].Faction, (UnitRole)role);
                    json.Append('[').Append(weapon.AttackDamage).Append(',')
                        .Append(weapon.AttackCooldownTicks).Append(']');
                }
                json.Append(']');
            }
            json.Append(']');
            return json.ToString();
        }

        // The page is one string on purpose: an artifact directory should be
        // copyable as a unit, and a player split across files is not.
        private const string Template = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>Nova AI Lab — view player (seed __SEED__)</title>
<style>
  /* AN APP SHELL, NOT A DOCUMENT.
     The page used to be a document that grew downwards: the map sat in the
     flow, the panels underneath it, and the window scrolled. With a map that
     is the whole point of opening the page that is exactly backwards — the
     thing one looks at scrolled away while the panels stayed. Now the body is
     the window: nothing scrolls except a panel's own contents, the map takes
     the room that is left, and the side panel folds out of the way. */
  :root { color-scheme: dark; }
  html, body { height:100%; }
  body { margin:0; background:#0d1117; color:#c9d1d9; overflow:hidden;
         display:flex; flex-direction:column;
         font:13px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace; }
  header { padding:6px 12px; border-bottom:1px solid #21262d; flex:0 0 auto;
           display:flex; gap:10px; align-items:baseline; flex-wrap:wrap; }
  h1 { font-size:13px; margin:0; font-weight:600; }
  .sub { color:#8b949e; font-size:12px; }
  .warn { color:#d29922; }
  .ok { color:#3fb950; }
  .derived { color:#d29922; font-style:italic; }

  .bar { display:flex; gap:6px; align-items:center; padding:6px 12px 4px; flex:0 0 auto; }
  #bandWrap { padding:0 12px 6px; flex:0 0 auto; }
  main { flex:1 1 auto; display:flex; gap:10px; padding:0 12px 10px; min-height:0; }

  /* flex-basis 0, not auto: with auto the column measures itself against the
     canvas inside it, the canvas is then sized from the column, and the two
     chase each other a pixel at a time on every redraw. */
  .mapcol { flex:1 1 0; min-width:0; min-height:0; display:flex; flex-direction:column;
            align-items:center; justify-content:flex-start; }

  /* The scoreboard over the map. flex:0 0 auto and a measured height: the
     canvas is sized from what is LEFT in the column, so a bar that grows by a
     row must shrink the map instead of pushing it out of the window. */
  /* Thirteen columns in a column that can be folded narrow: the bar scrolls
     sideways on its own rather than widening the page under the map. */
  #topbar { flex:0 0 auto; align-self:stretch; margin-bottom:6px; overflow-x:auto; }
  #topbar table { font-size:11px; }
  #topbar th, #topbar td { padding:2px 6px; white-space:nowrap; }
  #topbar th { color:#8b949e; font-weight:400; }
  #topbar td.num { font-variant-numeric:tabular-nums; }
  #topNote { margin-top:2px; font-size:11px; }
  /* One bar, one glance: how the fighting strength is split right now. */
  #shareBar { display:flex; height:8px; border-radius:4px; overflow:hidden;
              background:#161b22; margin-bottom:4px; }
  #shareBar div { height:100%; }
  canvas#map { background:#010409; border:1px solid #21262d; border-radius:4px;
           image-rendering:pixelated; display:block; cursor:crosshair; flex:0 0 auto; }
  aside#side { flex:0 0 430px; width:430px; min-height:0; display:flex; flex-direction:column; }
  body.collapsed aside#side { display:none; }

  .tabs { display:flex; gap:4px; flex:0 0 auto; margin-bottom:6px; }
  .tabs button { border-radius:4px 4px 0 0; }
  .tabs button.on { background:#1f6feb2e; border-color:#1f6feb; color:#e6edf3; }
  .panel { flex:1 1 auto; min-height:0; border:1px solid #21262d; border-radius:5px;
           padding:8px 10px; display:flex; flex-direction:column; overflow:hidden; }
  .panel[hidden] { display:none; }
  .panel > .scroll { flex:1 1 auto; min-height:0; overflow:auto; }

  input[type=range] { flex:1; min-width:200px; }
  button, label.file, select { background:#21262d; color:#c9d1d9; border:1px solid #30363d;
           border-radius:4px; padding:4px 9px; cursor:pointer; font:inherit; }
  button:hover, label.file:hover { background:#30363d; }
  button.wide { min-width:48px; }
  table { border-collapse:collapse; width:100%; font-size:12px; }
  th,td { text-align:right; padding:3px 7px; border-bottom:1px solid #21262d; }
  th:first-child, td:first-child { text-align:left; }
  .layers { display:flex; flex-direction:column; gap:3px; font-size:12px; margin-top:10px; }
  .legend { color:#8b949e; font-size:12px; line-height:1.8; }
  .sw { display:inline-block; width:10px; height:10px; border-radius:2px; vertical-align:-1px; }
  canvas#band { width:100%; height:26px; display:block; background:#010409;
                border:1px solid #21262d; border-radius:4px; cursor:pointer; }
  #unitList tr { cursor:pointer; }
  #unitList tr:hover td { background:#161b22; }
  #unitList tr.sel td { background:#1f6feb33; }
  #unitList tr.dead td { opacity:0.45; }
  #detail { flex:0 0 auto; margin-top:8px; border-top:1px solid #21262d; padding-top:8px;
            font-size:12px; max-height:44%; overflow:auto; }
  #detail h2 { font-size:12px; margin:0 0 6px; font-weight:600; }
  #detail dl { display:grid; grid-template-columns:auto 1fr; gap:1px 10px; margin:0; }
  #detail dt { color:#8b949e; }
  #detail dd { margin:0; }
  #logBox { font-size:11px; }
  #logBox div.row { white-space:nowrap; padding:0 4px; cursor:pointer; }
  #logBox div.row:hover { background:#161b22; }
  #logBox div.past { color:#6e7681; }
  #logBox div.now { background:#1f6feb26; color:#e6edf3; }
  #logBox div.future { color:#484f58; }
  #logBox div.sel { border-left:2px solid #ffffff; padding-left:2px; }
  #logBox b.id { color:#8b949e; font-weight:400; }
  .filters { display:flex; gap:5px; align-items:center; margin-bottom:6px; flex:0 0 auto;
             flex-wrap:wrap; font-size:12px; }
  .hint { color:#8b949e; font-size:11px; margin-top:4px; flex:0 0 auto; }
</style>
</head>
<body>
<header>
  <h1>Nova AI Lab</h1>
  <span class=""sub"">seed __SEED__ · __MAP_WIDTH__×__MAP_HEIGHT__ · __SLOT_COUNT__ slots</span>
  <label class=""file"">open the run's files<input type=""file"" id=""file"" multiple
    accept="".ndjson,.json,.txt"" hidden></label>
  <span class=""sub"" id=""status"">loading…</span>
  <span style=""flex:1""></span>
  <span class=""warn"" style=""font-size:12px"">diagnosis, never proof — what was not seen in the running game is unseen</span>
</header>

<div class=""bar"">
  <button id=""first"" title=""tick 0"">⏮</button>
  <button id=""back25"" class=""wide"" title=""25 ticks back"">◀◀</button>
  <button id=""back1"" class=""wide"" title=""one tick back"">◀</button>
  <button id=""play"" class=""wide"">play</button>
  <button id=""fwd1"" class=""wide"" title=""one tick on"">▶</button>
  <button id=""fwd25"" class=""wide"" title=""25 ticks on"">▶▶</button>
  <button id=""last"" title=""last tick"">⏭</button>
  <select id=""speed"" title=""ticks per playback step"">
    <option value=""1"">1 tick/step</option>
    <option value=""5"" selected>5 ticks/step</option>
    <option value=""25"">25 ticks/step</option>
    <option value=""100"">100 ticks/step</option>
  </select>
  <input type=""range"" id=""scrub"" min=""0"" max=""0"" value=""0"" step=""1"">
  <span id=""tickLabel"" class=""sub"">—</span>
  <button id=""zoomOut"" title=""zoom out (-)"">−</button>
  <span id=""zoomLabel"" class=""sub"">1.0×</span>
  <button id=""zoomIn"" title=""zoom in (+)"">+</button>
  <button id=""fit"" title=""whole map (0, or double-click)"">fit</button>
  <button id=""focus"" title=""centre the selected unit (f)"">focus</button>
  <button id=""sideToggle"" title=""fold the side panel away (s)"">»</button>
</div>

<div id=""bandWrap"">
  <canvas id=""band"" width=""2000"" height=""30""></canvas>
  <div class=""sub"" id=""bandLabel"">event band — every event of the match faintly, the selected unit's
    brightly (<b>n</b> / <b>p</b> jump between them, <b>N</b> / <b>P</b> through all of them)</div>
</div>

<main>
  <div class=""mapcol"">
    <!-- THE SCOREBOARD. Everything that used to need the layers tab, a count
         by hand, or a guess: who is stronger, who has the army, who has the
         money, who is losing units right now. -->
    <div id=""topbar"">
      <div id=""shareBar""></div>
      <table id=""top""><thead><tr>
        <th>slot</th><th>strength</th><th>share</th><th>gathered</th><th>wave</th>
        <th>army</th><th>workers</th><th>buildings</th><th>health</th><th>AE</th>
        <th>power</th><th>sees</th><th>spawned</th><th>lost</th><th>damage taken</th>
      </tr></thead><tbody></tbody></table>
      <div class=""sub"" id=""topNote"">—</div>
    </div>
    <canvas id=""map"" width=""700"" height=""700""></canvas>
    <div class=""hint"" id=""mapNote"">—</div>
  </div>

  <aside id=""side"">
    <div class=""tabs"">
      <button data-tab=""units"" class=""on"">units</button>
      <button data-tab=""log"">log</button>
      <button data-tab=""layers"">layers</button>
      <button data-tab=""legend"">legend</button>
    </div>

    <div class=""panel"" id=""tab-units"">
      <div class=""filters"">
        <select id=""filterSlot""><option value=""-1"">every slot</option></select>
        <select id=""filterShape"">
          <option value=""-1"">every shape</option>
          <option value=""4"">combat</option>
          <option value=""3"">harvester</option>
          <option value=""2"">builder</option>
          <option value=""0"">building</option>
          <option value=""1"">site</option>
        </select>
        <label><input type=""checkbox"" id=""filterDead""> the dead</label>
      </div>
      <div class=""scroll"" id=""unitList""><table><tbody></tbody></table></div>
      <div id=""detail""><span class=""sub"">no unit selected — click one in the list or on the map</span></div>
    </div>

    <div class=""panel"" id=""tab-log"" hidden>
      <div class=""filters"">
        <select id=""logSlot""><option value=""-1"">every slot</option></select>
        <select id=""logKind"">
          <option value=""all"">everything</option>
          <option value=""combat"">combat</option>
          <option value=""movement"">movement</option>
          <option value=""economy"">economy</option>
          <option value=""life"">life and building</option>
        </select>
        <label><input type=""checkbox"" id=""logOnlySelected""> only the selection</label>
        <label><input type=""checkbox"" id=""logFollow"" checked> follow</label>
      </div>
      <div class=""scroll"" id=""logBox""></div>
      <div class=""hint"" id=""logNote"">—</div>
    </div>

    <div class=""panel"" id=""tab-layers"" hidden>
      <div class=""scroll"">
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
      </div>
    </div>

    <div class=""panel"" id=""tab-legend"" hidden>
      <div class=""scroll legend"">
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
        <b>scoreboard</b> strength is the AI's own measure —
        damage × health ÷ firing interval, summed over the combat units, so twelve recruits
        and twelve riflemen are not the same wave. AE, power and ""sees"" are as old as the
        newest frame; everything else on the bar is exact for the tick.<br>
        <b>hollow</b> returning cargo · <b>white rim</b> below retreat threshold ·
        <b>yellow rim</b> stuck · <b>red circles</b> hits landing ·
        <b>fading cross</b> died just now<br><br>
        <b>map</b><br>
        wheel zooms on the pointer · drag moves · double-click fits<br>
        + − zoom · 0 fits · f centres the selection<br><br>
        <b>keys</b><br>
        ← → one tick · shift for 25 · space plays<br>
        n / p the selection's events · N / P every event<br>
        s the side panel · 1–4 the tabs · Esc clears the selection · Home / End<br><br>
        <span class=""warn"">fog is the most common reason an AI ""did not react"" — check it
        before blaming the logic.</span><br><br>
        <span class=""derived"">who fired is DERIVED from state and is never reported by the
        simulation — see notes/schadensquelle.md.</span>
      </div>
    </div>
  </aside>
</main>

<script>
const MAP_W = __MAP_WIDTH__, MAP_H = __MAP_HEIGHT__;
const ONE = 65536;                       // Q16.16: positions arrive as raw integers
const SLOT_COLOURS = ['#58a6ff','#f85149','#3fb950','#d29922','#bc8cff','#39c5cf','#ff9e64','#8b949e'];
const LINE_COLOURS = [null,'#f85149','#3fb950','#58a6ff'];
const SHAPE_GLYPH = ['▣','▢','✚','●','▲'];
const ROLE_NAME = ['unit','builder','harvester','HQ','refinery','power','storage','barracks',
                   'vehicleFactory','researchLab','radar','defensePlatform','basicInfantry',
                   'antiArmorInfantry','scoutVehicle','lightTank','battleTank','artillery'];

// WHO SAT WHERE, and what this run's definition table arms them with. Both are
// baked in when the page is written: the artifacts carry ids and health, not
// factions and weapon values, and a page that guessed them would weigh the two
// armies against a table nobody measured with.
const SLOTS = __SLOTS_JSON__;
const WEAPONS = __WEAPONS_JSON__;   // [slot][role] = [attackDamage, cooldownTicks]
const WAVE = __WAVE_JSON__;         // per slot: ring, waveSize, points, cap, produced, …

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
const EVENT_GROUP = {
  damage:'combat', death:'combat', heal:'combat', retreatBelow:'combat', retreatAbove:'combat',
  attackStart:'combat', attackSwitch:'combat', attackStop:'combat',
  order:'movement', goal:'movement', moveStart:'movement', moveStop:'movement',
  stuck:'movement', unstuck:'movement',
  harvestStart:'economy', harvestStop:'economy', cargoFull:'economy', cargoDelivered:'economy',
  spawn:'life', siteOpen:'life', siteDone:'life'
};

const canvas = document.getElementById('map'), ctx = canvas.getContext('2d');
const band = document.getElementById('band'), bctx = band.getContext('2d');
const scrub = document.getElementById('scrub'), tickLabel = document.getElementById('tickLabel');
const status = document.getElementById('status');
const fogSlot = document.getElementById('fogSlot'), trailSlot = document.getElementById('trailSlot');
const filterSlot = document.getElementById('filterSlot'), logSlot = document.getElementById('logSlot');

let frames = [];               // view.ndjson — fog and the per-slot header row live only here
let frameTicks = [];
let tracks = new Map();        // id -> {t:[], x:[], y:[]}
let events = [], eventTicks = [], eventsById = new Map();
let units = new Map();         // id -> row of units.json
let world = new Map();         // reconstructed state at `tick`
let tick = 0, lastTick = 0;
let selected = null, playing = false, timer = null;
let haveIds = false;

const loaded = { view:false, tracks:false, events:false, units:false };

// ---------------------------------------------------------------- loading

function parseNdjson(text) {
  return text.split('\n').filter(l => l.trim()).map(JSON.parse);
}

function loadView(text) {
  frames = parseNdjson(text);
  if (!frames.length) { status.textContent = 'no frames in the view file'; return; }
  frameTicks = frames.map(f => f.t);
  haveIds = frames.some(f => f.e.some(e => e.length > 9));
  lastTick = Math.max(lastTick, frames[frames.length - 1].t);

  const slots = frames[0].h.map(h => h[0]);
  const options = slots.map(s => '<option value=""' + s + '"">' + s + '</option>').join('');
  fogSlot.innerHTML = options;
  trailSlot.innerHTML = options + '<option value=""-1"">allen</option>';
  const named = '<option value=""-1"">every slot</option>' +
    slots.map(s => '<option value=""' + s + '"">slot ' + s + '</option>').join('');
  filterSlot.innerHTML = named;
  logSlot.innerHTML = named;

  loaded.view = true;
  ready();
}

// The track is the whole point of the trail AND of tick-exact playback: it
// carries every tick, while a frame carries every n-th. Rebuilt into one array
// per id so a position at any tick is a binary search, not a replay.
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
    lastTick = Math.max(lastTick, f.t);
  }
  loaded.tracks = true;
  ready();

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
  eventTicks = events.map(e => e.t);
  eventsById = new Map();
  for (const e of events) {
    let list = eventsById.get(e.id);
    if (!list) { list = []; eventsById.set(e.id, list); }
    list.push(e);
    lastTick = Math.max(lastTick, e.t);
  }
  loaded.events = true;
  ready();
}

function loadUnits(text) {
  units = new Map((JSON.parse(text).units || []).map(u => [u.id, u]));
  loaded.units = true;
  ready();
}

function ready() {
  scrub.max = lastTick;
  const wanted = readHash();
  if (wanted !== null) tick = Math.min(lastTick, Math.max(0, wanted));
  note();
  draw();
}

/** Reads ""#u=1043&t=2200"" and returns the wanted tick, or null. */
function readHash() {
  const params = new URLSearchParams((location.hash || '').replace(/^#/, ''));
  if (params.has('u')) selected = +params.get('u');
  return params.has('t') ? +params.get('t') : null;
}

function note() {
  const missing = Object.keys(loaded).filter(k => !loaded[k]);
  let text = frames.length ? frames.length + ' frames' : 'no frames';
  text += ' · ticks 0…' + lastTick;
  if (loaded.tracks) text += ' · ' + tracks.size + ' tracked units';
  if (loaded.events) text += ' · ' + events.length + ' events';
  if (missing.length) text += ' · missing: ' + missing.join(', ');
  if (frames.length && !haveIds) {
    text += ' · this view file predates the id column — no unit can be followed in it';
  }
  status.innerHTML = text + (loaded.view && loaded.tracks && loaded.events ? ' · ' + selfCheck() : '');
}

// The page proving itself against the file it was built from. Position, shape
// and the flags are reconstructed from track and events; the frames were
// written by a different piece of code. If the two disagree, one of them is
// wrong and the picture is worthless either way.
function selfCheck() {
  let checked = 0, wrong = 0;
  for (let i = 0; i < frames.length; i += Math.max(1, Math.floor(frames.length / 40))) {
    const frame = frames[i];
    const rebuilt = stateAt(frame.t);
    for (const e of frame.e) {
      if (e.length < 10) return '<span class=""warn"">no ids in the frames — self-check skipped</span>';
      checked++;
      const u = rebuilt.get(e[9]);
      const p = u ? posAt(e[9], frame.t) : null;
      if (!u || !p || p[0] !== e[2] || p[1] !== e[3] || shapeOf(u) !== e[1] || flagsOf(u) !== e[5]) wrong++;
    }
  }
  return wrong === 0
    ? '<span class=""ok"">reconstruction agrees with the frames (' + checked + ' checked)</span>'
    : '<span class=""warn"">reconstruction differs from the frames in ' + wrong + ' of ' + checked +
      ' — do not trust this picture</span>';
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

// ------------------------------------------------- the state at one tick

/** First index whose value is >= t. */
function lowerBound(list, t) {
  let lo = 0, hi = list.length;
  while (lo < hi) { const mid = (lo + hi) >> 1; if (list[mid] < t) lo = mid + 1; else hi = mid; }
  return lo;
}

function posAt(id, atTick) {
  const tr = tracks.get(id);
  if (!tr) return null;
  const i = lowerBound(tr.t, atTick + 1) - 1;
  return i < 0 ? null : [tr.x[i], tr.y[i]];
}

/**
 * Ticks a unit stays on the map after it died, fading out.
 * <p>
 * Without it a unit that dies simply is not there on the next redraw, and at
 * one tick per step that is the single most important moment of its life
 * passing unnoticed. It is drawn, not remembered: the position comes from the
 * death event, which carries the cell the unit stood in.
 */
const DEATH_FADE_TICKS = 120;

/** Ticks a hit stays visible as a spark at the victim. */
const HIT_FLASH_TICKS = 8;

/**
 * Replays the events up to `atTick`. All of them, from tick 0, every time —
 * a match carries a few thousand, which is nothing, and a replay from the
 * start cannot drift the way an incremental state can when you scrub back.
 * <p>
 * The returned map carries a second one on `.dying`: the units that died
 * within the last DEATH_FADE_TICKS, with the tick and the place. And on
 * `.tally`, per slot, what the run has cost so far — built, lost, damage
 * taken. Counted HERE and not in a second pass: the walk over the events is
 * already happening, and a number that came from a different walk could
 * disagree with the picture beside it.
 */
function stateAt(atTick) {
  const state = new Map();
  const dying = new Map();
  const tally = new Map();
  const of = slot => {
    let t = tally.get(slot);
    if (!t) { t = { built:0, lost:0, damageTaken:0 }; tally.set(slot, t); }
    return t;
  };
  const end = lowerBound(eventTicks, atTick + 1);
  for (let i = 0; i < end; i++) {
    const e = events[i];
    let u = state.get(e.id);
    switch (e.k) {
      case 'spawn':
        state.set(e.id, { id:e.id, slot:e.slot, role:e.role, hp:e.hp, hpMax:e.hpMax || e.hp,
                          site:false, siteDef:0, moving:false, attack:0, field:0, fx:0, fy:0,
                          cargo:false, cargoAE:0, goalX:-1, goalY:-1, orderX:-1, orderY:-1,
                          below:false, stuck:false, born:e.t });
        of(e.slot).built++;
        break;
      case 'death':
        if (u && atTick - e.t <= DEATH_FADE_TICKS && e.x !== undefined) {
          dying.set(e.id, { unit:u, t:e.t, x:e.x, y:e.y });
        }
        of(e.slot).lost++;
        state.delete(e.id);
        break;
      default:
        if (!u) break;
        if (e.k === 'damage') { of(e.slot).damageTaken += Math.max(0, e.from - e.to); u.hp = e.to; }
        else if (e.k === 'heal') u.hp = e.to;
        else if (e.k === 'siteOpen') { u.site = true; u.siteDef = e.def; }
        else if (e.k === 'siteDone') { u.site = false; u.role = e.role; }
        else if (e.k === 'moveStart') u.moving = true;
        else if (e.k === 'moveStop') u.moving = false;
        else if (e.k === 'goal') { u.goalX = e.tx; u.goalY = e.ty; }
        else if (e.k === 'order') { u.orderX = e.tx; u.orderY = e.ty; }
        else if (e.k === 'attackStart' || e.k === 'attackSwitch') u.attack = e.target;
        else if (e.k === 'attackStop') u.attack = 0;
        else if (e.k === 'harvestStart') { u.field = e.field; u.fx = e.x; u.fy = e.y; }
        else if (e.k === 'harvestStop') u.field = 0;
        else if (e.k === 'cargoFull') { u.cargo = true; u.cargoAE = e.cargo; }
        else if (e.k === 'cargoDelivered') u.cargo = false;
        else if (e.k === 'stuck') u.stuck = true;
        else if (e.k === 'unstuck') u.stuck = false;
        else if (e.k === 'retreatBelow') u.below = true;
        else if (e.k === 'retreatAbove') u.below = false;
        if (e.role !== undefined && e.k !== 'siteDone') u.role = e.role;
    }
  }
  state.dying = dying;
  state.tally = tally;
  return state;
}

function shapeOf(u) {
  if (u.site) return 1;
  if (u.role >= 3 && u.role <= 11) return 0;   // the nine building roles
  if (u.role === 1) return 2;
  if (u.role === 2) return 3;
  return 4;
}

function healthPercentOf(u) {
  if (u.site) return siteProgress(u.id);
  return u.hpMax > 0 ? Math.floor(u.hp * 100 / u.hpMax) : 0;
}

/** Build progress lives only in the frames: it is not an edge, it creeps. */
function siteProgress(id) {
  const frame = frameAt(tick);
  if (!frame) return 0;
  const row = frame.e.find(e => e.length > 9 && e[9] === id);
  return row ? row[4] : 0;
}

function flagsOf(u) {
  return (u.cargo ? 1 : 0) | (u.below ? 2 : 0) | (u.moving ? 4 : 0);
}

function lineOf(u) {
  // The same priority the recorder uses: an attack order says more about what
  // a unit is doing than the move that carries it there.
  if (u.attack) {
    const p = posAt(u.attack, tick);
    if (p && world.has(u.attack)) return [1, p[0], p[1]];
  }
  // Ein Lauf, der aufgezeichnet wurde bevor harvestStart die Zelle des Feldes
  // mitschrieb, hat hier nichts. Dann lieber keine Linie als eine nach NaN.
  if (u.field && Number.isFinite(u.fx) && Number.isFinite(u.fy)) return [2, u.fx, u.fy];
  if (u.moving && u.goalX >= 0) return [3, u.goalX * ONE, u.goalY * ONE];
  return [0, 0, 0];
}

/** The newest frame at or before `atTick` — fog and the header row live there. */
function frameAt(atTick) {
  if (!frames.length) return null;
  const i = lowerBound(frameTicks, atTick + 1) - 1;
  return i < 0 ? frames[0] : frames[i];
}

// ---------------------------------------------------------------- drawing

/**
 * The camera. A 128×128 map in 700 pixels puts a cell at five and a half of
 * them and a unit at three — at that size a wave and a traffic jam look the
 * same, which is the one distinction the whole tool exists to make. So the
 * map is scalable and draggable, and the numbers below are what the screen
 * shows, never what is recorded.
 */
const view = { zoom: 1, cx: MAP_W / 2, cy: MAP_H / 2 };
const MIN_ZOOM = 0.5, MAX_ZOOM = 40;

const pxPerCell = () => (canvas.width / MAP_W) * view.zoom;
const px = raw => (raw / ONE - view.cx) * pxPerCell() + canvas.width / 2;
const py = raw => canvas.height / 2 - (raw / ONE - view.cy) * pxPerCell();

/** Screen back to map cells — for the wheel, the drag and picking a unit. */
function toCell(sx, sy) {
  const scale = pxPerCell();
  return [(sx - canvas.width / 2) / scale + view.cx, view.cy - (sy - canvas.height / 2) / scale];
}

function fitMap() {
  view.zoom = 1; view.cx = MAP_W / 2; view.cy = MAP_H / 2;
  draw();
}

/** Puts the selected unit in the middle without changing the scale. */
function focusSelected() {
  if (selected === null) return;
  const p = posAt(selected, tick);
  if (!p) return;
  view.cx = p[0] / ONE; view.cy = p[1] / ONE;
  draw();
}

canvas.addEventListener('wheel', event => {
  event.preventDefault();
  const rect = canvas.getBoundingClientRect();
  const sx = (event.clientX - rect.left) * (canvas.width / rect.width);
  const sy = (event.clientY - rect.top) * (canvas.height / rect.height);

  // The cell under the pointer stays under the pointer — anything else and
  // zooming into a corner walks the map out from under you.
  const before = toCell(sx, sy);
  const factor = event.deltaY < 0 ? 1.15 : 1 / 1.15;
  view.zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, view.zoom * factor));
  const after = toCell(sx, sy);
  view.cx += before[0] - after[0];
  view.cy += before[1] - after[1];
  draw();
}, { passive: false });

let dragFrom = null, dragged = 0;

canvas.addEventListener('pointerdown', event => {
  dragFrom = [event.clientX, event.clientY, view.cx, view.cy];
  dragged = 0;
  canvas.setPointerCapture(event.pointerId);
});

canvas.addEventListener('pointermove', event => {
  if (!dragFrom) return;
  const rect = canvas.getBoundingClientRect();
  const scale = pxPerCell() * (rect.width / canvas.width);
  const dx = event.clientX - dragFrom[0], dy = event.clientY - dragFrom[1];
  dragged = Math.max(dragged, Math.abs(dx) + Math.abs(dy));
  view.cx = dragFrom[2] - dx / scale;
  view.cy = dragFrom[3] + dy / scale;
  draw();
});

for (const kind of ['pointerup', 'pointercancel', 'pointerleave']) {
  canvas.addEventListener(kind, () => { dragFrom = null; });
}

canvas.addEventListener('dblclick', fitMap);

const markerScale = () => Math.min(3, Math.max(1, Math.sqrt(view.zoom)));

function drawFog(frame) {
  if (!frame || !frame.fog) return;
  const runs = frame.fog[+fogSlot.value];
  if (!runs) return;
  const cw = pxPerCell();
  let cell = 0;
  for (let i = 0; i < runs.length; i += 2) {
    const count = runs[i], state = runs[i + 1];
    if (state !== 2) {
      ctx.fillStyle = state === 0 ? 'rgba(0,0,0,0.82)' : 'rgba(0,0,0,0.45)';
      for (let k = 0; k < count; k++) {
        const c = cell + k, x = c % MAP_W, y = (c / MAP_W) | 0;
        ctx.fillRect(px(x * ONE), py((y + 1) * ONE), cw + 0.5, cw + 0.5);
      }
    }
    cell += count;
  }
}

// A hard budget on POINTS across the bulk layer. Points are cheap — they are
// lineTo calls inside one path — so this is high enough to draw a whole slot
// over a whole match and still a real cap.
const TRAIL_BUDGET = 250000;

// A hard budget on STROKES for the fading trail. Those are not cheap: one
// stroke per segment over a 9000-tick match is nine thousand of them per
// redraw. Longer trails are drawn in bundles instead, which changes how fine
// the fade is and nothing about where the line runs.
const FADE_STROKES = 900;

function drawTrail(id, span, colour, width, fade, budget) {
  const tr = tracks.get(id);
  if (!tr || !tr.t.length) return 0;
  const to = lowerBound(tr.t, tick + 1);
  const from = span > 0 ? lowerBound(tr.t, tick - span) : 0;
  if (to - from < 2) return 0;

  const count = Math.min(to - from, budget);
  const start = to - count;
  ctx.lineWidth = width;
  ctx.lineJoin = 'round';
  ctx.strokeStyle = colour;

  if (!fade) {
    ctx.globalAlpha = 0.5;
    ctx.beginPath();
    ctx.moveTo(px(tr.x[start]), py(tr.y[start]));
    for (let i = start + 1; i < to; i++) ctx.lineTo(px(tr.x[i]), py(tr.y[i]));
    ctx.stroke();
    ctx.globalAlpha = 1;
    return count;
  }

  // Bundles overlap by one point, otherwise the trail is drawn with gaps.
  const bundle = Math.max(1, Math.ceil(count / FADE_STROKES));
  for (let i = start; i < to - 1; i += bundle) {
    const end = Math.min(to - 1, i + bundle);
    ctx.globalAlpha = 0.12 + 0.78 * ((i - start) / count);
    ctx.beginPath();
    ctx.moveTo(px(tr.x[i]), py(tr.y[i]));
    for (let k = i + 1; k <= end; k++) ctx.lineTo(px(tr.x[k]), py(tr.y[k]));
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
  return count;
}

function drawTrails() {
  const span = +document.getElementById('trailSpan').value;

  if (document.getElementById('layerAllTrails').checked) {
    const slot = +trailSlot.value;                 // -1 = alle Slots übereinander
    let budget = TRAIL_BUDGET;

    // Every unit the slot EVER had up to this tick, not only the ones still
    // standing. The dead are the interesting half of a traffic picture: they
    // are the ones that walked into something.
    const rows = units.size
      ? [...units.values()].filter(u => u.firstTick <= tick)
      : [...world.values()].map(u => ({ id:u.id, slot:u.slot }));

    for (const row of rows) {
      if (budget <= 0) break;
      if (slot >= 0 && row.slot !== slot) continue;
      budget -= drawTrail(row.id, span, SLOT_COLOURS[row.slot % SLOT_COLOURS.length], 1, false, budget);
    }
  }

  if (selected !== null && document.getElementById('layerTrail').checked) {
    drawTrail(selected, span, '#ffffff', 1.8, true, TRAIL_BUDGET);
  }
}

/**
 * The canvas takes whatever room the map column has. It is a square because
 * the map is one — a stretched canvas would put the distances out of true,
 * and distances are the whole point of drawing this at all.
 */
function fitCanvas() {
  const column = document.querySelector('.mapcol');
  // The column is a flex item with a real height now, so the room it has is
  // its own — no arithmetic against window.innerHeight that goes wrong the
  // moment anything above the map changes height.
  // …minus what stands above and below it. The scoreboard grows with the seat
  // count, so the room for the map is measured, never assumed.
  const chrome = document.getElementById('topbar').offsetHeight +
                 document.getElementById('mapNote').offsetHeight + 10;
  const size = Math.round(Math.max(320,
    Math.min(column.clientWidth, column.clientHeight - chrome, 1800)));
  if (canvas.width !== size) { canvas.width = size; canvas.height = size; }
}

function draw() {
  fitCanvas();
  if (!loaded.events || !loaded.tracks) { drawEmpty(); return; }
  world = stateAt(tick);

  ctx.fillStyle = '#010409';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // Wo die Karte aufhört. Sobald man hineinzoomt, ist der leere Rand sonst
  // nicht vom unerkundeten Gelände zu unterscheiden.
  ctx.save();
  ctx.strokeStyle = '#21262d'; ctx.lineWidth = 1;
  ctx.strokeRect(px(0), py(MAP_H * ONE), MAP_W * pxPerCell(), MAP_H * pxPerCell());
  ctx.restore();

  const showLines = document.getElementById('layerLines').checked;
  const showHealth = document.getElementById('layerHealth').checked;

  drawTrails();

  const drawn = [];
  for (const u of world.values()) {
    const p = posAt(u.id, tick);
    if (!p) continue;
    drawn.push([u, p]);
  }
  drawn.sort((a, b) => a[0].id - b[0].id);   // stable paint order, so a screenshot repeats

  if (showLines) {
    ctx.lineWidth = 1;
    ctx.globalAlpha = 0.45;
    for (const [u, p] of drawn) {
      const [kind, lx, ly] = lineOf(u);
      if (!kind) continue;
      ctx.strokeStyle = LINE_COLOURS[kind];
      ctx.beginPath(); ctx.moveTo(px(p[0]), py(p[1])); ctx.lineTo(px(lx), py(ly)); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  for (const [u, p] of drawn) {
    paintUnit(u, px(p[0]), py(p[1]), healthPercentOf(u), flagsOf(u), showHealth ? 1 : 0, 1);
    if (u.id === selected) ring(px(p[0]), py(p[1]), 5);
  }

  drawDying();
  drawHits();

  const frame = frameAt(tick);
  if (document.getElementById('layerFog').checked) drawFog(frame);
  markDeath();

  renderTopBar(frame);
  document.getElementById('mapNote').innerHTML =
    'positions, health, orders and flags are rebuilt for tick <b>' + tick + '</b> exactly · ' +
    'fog and the header row come from frame t=' + (frame ? frame.t : '—') +
    ', the newest one at or before it';

  tickLabel.textContent = 'tick ' + tick + ' / ' + lastTick;
  document.getElementById('zoomLabel').textContent = view.zoom.toFixed(1) + '×';
  scrub.value = tick;

  renderUnits();
  renderDetail();
  renderLog();
  drawBand();
}

function drawEmpty() {
  ctx.fillStyle = '#010409';
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = '#8b949e';
  ctx.font = '13px ui-monospace, monospace';
  ctx.fillText('waiting for tracks.ndjson and events.ndjson', 20, 30);
}

/**
 * One marker. Shared by the living and the dying, so a unit does not change
 * its shape in the moment it is most worth looking at.
 * `healthDims` 1 lets health darken the marker, 0 keeps it flat;
 * `fade` scales everything for the units that are on their way out.
 */
function paintUnit(u, cx, cy, hp, flags, healthDims, fade) {
  const shape = shapeOf(u);
  const base = SLOT_COLOURS[u.slot % SLOT_COLOURS.length];
  const dim = healthDims ? Math.max(0.3, hp / 100) : 1;
  ctx.globalAlpha = dim * fade;
  ctx.fillStyle = base; ctx.strokeStyle = base; ctx.lineWidth = 1.4;

  const hollow = (flags & 1) !== 0;
  const weak   = (flags & 2) !== 0;
  // Markers grow with the scale, but slower than it: a building drawn at true
  // size fills the screen at zoom 20, and one drawn at a fixed size stops
  // telling a bunker from a soldier.
  const r = (shape === 0 ? 5 : shape === 1 ? 4.5 : 3.2) * markerScale();

  ctx.beginPath();
  if (shape === 0 || shape === 1) {
    ctx.rect(cx - r, cy - r, r * 2, r * 2);
  } else if (shape === 2) {
    ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
    ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
    ctx.stroke(); ctx.globalAlpha = 1;
    return;
  } else if (shape === 3) {
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
  } else {
    ctx.moveTo(cx, cy - r); ctx.lineTo(cx + r, cy + r); ctx.lineTo(cx - r, cy + r); ctx.closePath();
  }
  if (hollow || shape === 1) ctx.stroke(); else ctx.fill();

  if (weak) {
    ctx.globalAlpha = fade; ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.arc(cx, cy, r + 2.5, 0, Math.PI * 2); ctx.stroke();
  }
  if (u.stuck) {
    ctx.globalAlpha = fade; ctx.strokeStyle = '#d29922'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(cx, cy, r + 4.5, 0, Math.PI * 2); ctx.stroke();
  }
  ctx.globalAlpha = 1;
}

/** The recently dead, fading where they fell, with a cross that fades with them. */
function drawDying() {
  if (!world.dying) return;
  for (const entry of world.dying.values()) {
    const age = tick - entry.t;
    if (age < 0 || age > DEATH_FADE_TICKS) continue;
    const fade = 1 - age / DEATH_FADE_TICKS;
    const cx = px(entry.x), cy = py(entry.y);

    paintUnit(entry.unit, cx, cy, 100, 0, 0, fade * 0.6);
    ctx.save();
    ctx.globalAlpha = fade;
    ctx.strokeStyle = '#f85149'; ctx.lineWidth = 1.6;
    const r = 4 + (1 - fade) * 5;                 // der Riss geht auf, während er verblasst
    ctx.beginPath();
    ctx.moveTo(cx - r, cy - r); ctx.lineTo(cx + r, cy + r);
    ctx.moveTo(cx + r, cy - r); ctx.lineTo(cx - r, cy + r);
    ctx.stroke();
    ctx.restore();
  }
}

/**
 * Where hits landed in the last few ticks. The order lines say who is aiming
 * at whom; this says where it is actually connecting — a wave that stalls out
 * of range looks identical to one that is trading, until the sparks show up.
 */
function drawHits() {
  const from = lowerBound(eventTicks, Math.max(0, tick - HIT_FLASH_TICKS));
  const to = lowerBound(eventTicks, tick + 1);
  ctx.save();
  ctx.lineWidth = 1.4;
  for (let i = from; i < to; i++) {
    const e = events[i];
    if (e.k !== 'damage') continue;
    const p = posAt(e.id, e.t);
    if (!p) continue;
    const fade = 1 - (tick - e.t) / (HIT_FLASH_TICKS + 1);
    ctx.globalAlpha = fade * 0.9;
    ctx.strokeStyle = '#f85149';
    ctx.beginPath();
    ctx.arc(px(p[0]), py(p[1]), 3 + (1 - fade) * 7, 0, Math.PI * 2);
    ctx.stroke();
  }
  ctx.restore();
}

function ring(cx, cy, r) {
  ctx.save();
  ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1.5; ctx.globalAlpha = 0.9;
  ctx.beginPath(); ctx.arc(cx, cy, r + 6, 0, Math.PI * 2); ctx.stroke();
  ctx.restore();
}

// A unit that dies does not simply vanish from the page: the spot it died on
// stays marked, because ""where did it die"" is half the question.
function markDeath() {
  if (selected === null) return;
  const list = eventsById.get(selected);
  if (!list) return;
  const death = list.find(e => e.k === 'death');
  if (!death || death.t > tick || death.x === undefined) return;

  const cx = px(death.x), cy = py(death.y);
  ctx.save();
  ctx.strokeStyle = '#f85149'; ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(cx - 5, cy - 5); ctx.lineTo(cx + 5, cy + 5);
  ctx.moveTo(cx + 5, cy - 5); ctx.lineTo(cx - 5, cy + 5);
  ctx.stroke();
  ctx.restore();
}

// ------------------------------------------------------- the scoreboard

/**
 * What one entity is worth in a fight — <c>Nova.AI.CombatStrength.Of</c>,
 * the same one line: damage times remaining health per firing interval,
 * truncating integer division.
 * <p>
 * NOT A HEAD COUNT, and that is the whole reason it is here. Twelve Legion
 * recruits and twelve Alliance riflemen are the same number and not the same
 * army — the AI's wave gate weighs them with this formula, so a bar that
 * claims to show ""who is stronger"" has to weigh them the same way or it
 * contradicts the decision it is supposed to explain.
 */
function strengthOf(u) {
  const table = WEAPONS[u.slot];
  if (!table || u.hp <= 0) return 0;
  const weapon = table[u.role];
  if (!weapon) return 0;
  const damage = weapon[0], cooldown = weapon[1];
  if (damage <= 0 || cooldown <= 0) return 0;
  return Math.floor(damage * u.hp / cooldown);
}

/**
 * THE ARMY THAT IS ACTUALLY GATHERED, and what it is waiting for.
 * <p>
 * The AI does not weigh ""every combat unit"": it weighs what stands inside the
 * ring around its own HQ. Everything outside marched with an earlier wave and
 * is never called back — so a side can hold twenty units and still not launch,
 * and the total strength beside it explains nothing. This is the number that
 * does.
 * <p>
 * RECOMPUTED, NOT READ. No artifact carries the AI's verdict, so the page
 * repeats its arithmetic — ring membership, the strength sum, and the gate's
 * ceiling clause — from the state at this tick. Two honest consequences: the
 * page can only be as right as this copy of the rule, and the AI decides on
 * its own cadence, so between two decisions the flag here can already have
 * flipped while the army has not moved yet. Both are why the column says
 * ""derived"".
 */
function waveState(slot) {
  const rules = WAVE.find(w => w.slot === slot);
  const home = baseOf(slot);
  if (!rules || !home) return null;

  const hqX = Math.floor(home[0] / ONE), hqY = Math.floor(home[1] / ONE);
  let gathered = 0, gatheredStrength = 0, committed = 0, canProduce = false;

  for (const u of world.values()) {
    if (u.slot !== slot) continue;
    if (!u.site && u.role === rules.producerRole) canProduce = true;
    if (shapeOf(u) !== 4) continue;
    const p = posAt(u.id, tick);
    if (!p) continue;
    const dx = Math.abs(Math.floor(p[0] / ONE) - hqX);
    const dy = Math.abs(Math.floor(p[1] / ONE) - hqY);
    if (Math.max(dx, dy) > rules.ring) committed++;
    else { gathered++; gatheredStrength += strengthOf(u); }
  }

  // Clamped to the army cap, exactly as EffectiveWaveSize does: a wave size
  // above the cap would wait for units production can never deliver.
  const size = Math.min(rules.waveSize, rules.cap);
  const shared = { gathered, gatheredStrength, committed, cadence: rules.cadence };
  if (size <= 1) return Object.assign(shared, { mode:'off', reason:'waveSize 1 — every unit marches' });

  if (rules.points > 0 && rules.produced > 0) {
    // The gate's ceiling: the threshold never exceeds what the ring can still
    // GROW to, or the wave waits for strength that cannot arrive.
    let free = canProduce ? rules.cap - committed - gathered : 0;
    if (free < 0) free = 0;
    const attainable = gatheredStrength + free * rules.produced;
    const need = Math.min(rules.points, attainable);
    return Object.assign(shared, { mode:'points', have:gatheredStrength, need,
                                   ready: gatheredStrength >= need, canProduce });
  }

  let reachable = rules.cap - committed;
  if (reachable < 1) reachable = 1;
  const need = Math.min(size, reachable);
  return Object.assign(shared, { mode:'count', have:gathered, need, ready: gathered >= need, canProduce });
}

/** Everything the scoreboard shows, per seat, at the current tick. */
function slotStats(frame) {
  const stats = new Map();
  const seats = SLOTS.length ? SLOTS : [...new Set([...world.values()].map(u => u.slot))]
    .sort((a, b) => a - b).map(slot => ({ slot, faction:'?', profile:'?' }));

  for (const seat of seats) {
    stats.set(seat.slot, { seat, army:0, workers:0, buildings:0, sites:0, strength:0,
                           hp:0, hpMax:0, built:0, lost:0, damageTaken:0,
                           credits:null, power:null, sees:null });
  }

  for (const u of world.values()) {
    const s = stats.get(u.slot);
    if (!s) continue;
    const shape = shapeOf(u);
    if (shape === 1) s.sites++;
    else if (shape === 0) s.buildings++;
    else if (shape === 4) { s.army++; s.strength += strengthOf(u); }
    else s.workers++;
    s.hp += u.hp; s.hpMax += u.hpMax;
  }

  for (const s of stats.values()) s.wave = waveState(s.seat.slot);

  if (world.tally) {
    for (const [slot, t] of world.tally) {
      const s = stats.get(slot);
      if (s) { s.built = t.built; s.lost = t.lost; s.damageTaken = t.damageTaken; }
    }
  }

  // Credits, power and what a side can see exist ONLY in the frames. They are
  // as old as the newest frame at or before this tick, and the note says so —
  // the alternative is a number that looks exact and is not.
  if (frame) {
    for (const h of frame.h) {
      const s = stats.get(h[0]);
      if (s) { s.credits = h[1]; s.power = h[2]; s.sees = h[4]; }
    }
  }
  return [...stats.values()];
}

/** What stands in the ring: how many of the army, and what they weigh. */
function gatheredCell(wave) {
  if (!wave) return '<span class=""sub"">—</span>';
  const army = wave.gathered + wave.committed;
  if (army === 0) return '<span class=""sub"">no army</span>';
  return wave.gatheredStrength +
    ' <span class=""sub"">· ' + wave.gathered + ' of ' + army + '</span>';
}

/** What the gate wants before it lets them march. */
function waveCell(wave) {
  if (!wave) return '<span class=""sub"">—</span>';
  if (wave.mode === 'off') return '<span class=""sub"">off · ' + wave.reason + '</span>';
  // An empty ring passes the gate arithmetically — the ceiling clause drops the
  // threshold to zero — but there is nobody to send. ""0 / 0 marches"" would be
  // the one line on this bar that reads like a decision and is none.
  if (wave.gathered === 0) return '<span class=""sub"">ring empty · all out</span>';
  const unit = wave.mode === 'count' ? ' units' : '';
  const verdict = wave.ready
    ? '<span class=""ok"">marches</span>'
    : '<span class=""warn"">waits</span>';
  return '<span class=""derived"">' + wave.have + ' / ' + wave.need + unit + '</span> ' + verdict;
}

function renderTopBar(frame) {
  const stats = slotStats(frame);
  const total = stats.reduce((sum, s) => sum + s.strength, 0);
  const colour = s => SLOT_COLOURS[s.seat.slot % SLOT_COLOURS.length];
  const num = v => v === null ? '<span class=""sub"">—</span>' : v;

  document.getElementById('shareBar').innerHTML = total === 0
    ? '<div style=""flex:1;background:#161b22""></div>'
    : stats.map(s => '<div style=""flex:' + s.strength + ';background:' + colour(s) + '"" title=""slot ' +
        s.seat.slot + ': ' + s.strength + ' strength""></div>').join('');

  document.querySelector('#top tbody').innerHTML = stats.map(s =>
    '<tr>' +
    '<td style=""color:' + colour(s) + '"" title=""profile ' + s.seat.profile + '"">slot ' +
      s.seat.slot + ' · ' + s.seat.faction + '</td>' +
    '<td class=""num"">' + s.strength + '</td>' +
    '<td class=""num"">' + (total ? Math.round(s.strength * 100 / total) + '%' : '—') + '</td>' +
    '<td class=""num"">' + gatheredCell(s.wave) + '</td>' +
    '<td class=""num"">' + waveCell(s.wave) + '</td>' +
    '<td class=""num"">' + s.army + '</td>' +
    '<td class=""num"">' + s.workers + '</td>' +
    '<td class=""num"">' + s.buildings + (s.sites ? ' +' + s.sites + '&nbsp;site' : '') + '</td>' +
    '<td class=""num"">' + (s.hpMax ? Math.round(s.hp * 100 / s.hpMax) + '%' : '—') + '</td>' +
    '<td class=""num"">' + num(s.credits) + '</td>' +
    '<td class=""num"">' + num(s.power) + '</td>' +
    '<td class=""num"">' + num(s.sees) + '</td>' +
    '<td class=""num"">' + s.built + '</td>' +
    '<td class=""num"">' + s.lost + '</td>' +
    '<td class=""num"">' + s.damageTaken + '</td>' +
    '</tr>').join('');

  const cadence = stats.length && stats[0].wave ? stats[0].wave.cadence : 0;
  document.getElementById('topNote').innerHTML =
    'army strength, counts, health, spawned, lost and damage are exact for tick <b>' + tick + '</b> · ' +
    'AE, power and ""sees"" come from frame t=' + (frame ? frame.t : '—') +
    ' · <span class=""derived"">gathered/wave is the gate recomputed here' +
    (cadence ? ', while the AI decides every ' + cadence + ' ticks' : '') + '</span>' +
    (WEAPONS.length ? '' : ' · <span class=""warn"">no weapon table in this player — strength is 0</span>');
}

// -------------------------------------------------------------- the band

function drawBand() {
  bctx.clearRect(0, 0, band.width, band.height);
  bctx.fillStyle = '#010409';
  bctx.fillRect(0, 0, band.width, band.height);
  if (!events.length) return;

  const at = t => (t / Math.max(1, lastTick)) * (band.width - 2) + 1;

  // Every event of the match, faintly — the shape of the whole run.
  bctx.globalAlpha = 0.35;
  bctx.fillStyle = '#484f58';
  for (const e of events) bctx.fillRect(at(e.t), 21, 1, 6);
  bctx.globalAlpha = 1;

  const list = selected === null ? null : eventsById.get(selected);
  if (list) {
    for (const e of list) {
      bctx.fillStyle = EVENT_COLOUR[e.k] || '#8b949e';
      bctx.fillRect(at(e.t) - 1, 3, 2, 15);
    }
  }

  bctx.fillStyle = '#ffffff';
  bctx.fillRect(at(tick) - 1, 0, 2, band.height);

  document.getElementById('bandLabel').innerHTML = selected === null
    ? 'event band — every event of the match faintly (<b>N</b> / <b>P</b> step through them); ' +
      'pick a unit to see its own life brightly'
    : '#' + selected + ': ' + (list ? list.length : 0) + ' events of ' + events.length +
      ' in the match (<b>n</b> / <b>p</b> its own, <b>N</b> / <b>P</b> all)';
}

band.addEventListener('click', ev => {
  const rect = band.getBoundingClientRect();
  seek(Math.round(((ev.clientX - rect.left) / rect.width) * lastTick));
});

// --------------------------------------------------------- list + detail

function renderUnits() {
  const body = document.querySelector('#unitList tbody');
  const slotFilter = +filterSlot.value, shapeFilter = +document.getElementById('filterShape').value;
  const rows = [];

  for (const u of world.values()) {
    if (slotFilter >= 0 && u.slot !== slotFilter) continue;
    const shape = shapeOf(u);
    if (shapeFilter >= 0 && shape !== shapeFilter) continue;
    rows.push({ id:u.id, slot:u.slot, shape, hp:healthPercentOf(u), u, dead:false });
  }

  if (document.getElementById('filterDead').checked) {
    for (const [id, row] of units) {
      if (world.has(id) || row.firstTick > tick) continue;
      if (slotFilter >= 0 && row.slot !== slotFilter) continue;
      rows.push({ id, slot:row.slot, shape:-1, hp:0, role:row.role, dead:true });
    }
  }

  rows.sort((a, b) => a.slot - b.slot || a.id - b.id);

  body.innerHTML = rows.map(r => {
    const colour = SLOT_COLOURS[r.slot % SLOT_COLOURS.length];
    const glyph = r.shape >= 0 ? SHAPE_GLYPH[r.shape] : '✖';
    // The ROLE, not the shape: the glyph already says ▲ combat, and ""combat""
    // spelled out next to it says nothing a reader could look for in the game.
    // ""basicInfantry"" is the thing that stands on the map.
    const name = roleNameOf(r.id);
    // Short words, not pictograms: the monospace stacks this page runs in fall
    // back to a replacement box for half the symbol block, and a box says
    // nothing at all.
    let mark = '';
    if (r.u) {
      if (r.u.attack) mark = 'atk';
      else if (r.u.field) mark = 'hrv';
      else if (r.u.moving) mark = 'mov';
      if (r.u.stuck) mark = '<span class=""warn"">stuck</span>';
    }
    return '<tr data-id=""' + r.id + '"" class=""' + (r.id === selected ? 'sel ' : '') + (r.dead ? 'dead' : '') + '"">' +
      '<td style=""color:' + colour + '"">' + glyph + ' #' + r.id + ' ' + name + '</td>' +
      '<td>' + (r.dead ? '†' : r.hp + '%') + '</td><td>' + mark + '</td></tr>';
  }).join('') || '<tr><td class=""sub"">nothing matches the filter</td></tr>';
}

// Delegation, not a listener per row: both lists are rebuilt on every redraw,
// and while the match plays that is sixteen times a second.
document.getElementById('unitList').addEventListener('click', ev => {
  const row = ev.target.closest('tr[data-id]');
  if (row) select(+row.dataset.id);
});

document.getElementById('logBox').addEventListener('click', ev => {
  const row = ev.target.closest('div.row[data-i]');
  if (!row) return;
  const e = logRows[+row.dataset.i];
  if (!e) return;
  if (ev.target.classList.contains('id')) select(e.id); else seek(e.t);
});

// The selection lives in the URL fragment, so ""look at #1043 around tick
// 2200"" is a link and not a set of instructions.
function select(id) {
  selected = selected === id ? null : id;
  try {
    history.replaceState(null, '', selected === null ? '#t=' + tick : '#u=' + selected + '&t=' + tick);
  } catch (ignored) {}
  draw();
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
    case 'attackStart': return 'attacks ' + shortLabel(e.target);
    case 'attackSwitch': return 'switches to ' + shortLabel(e.target);
    case 'attackStop': return 'stops attacking ' + shortLabel(e.target);
    case 'harvestStart': return 'harvests field ' + e.field;
    case 'harvestStop': return 'leaves field ' + e.field;
    case 'cargoFull': return 'cargo full (' + e.cargo + ' AE)';
    case 'cargoDelivered': return 'cargo delivered (' + e.cargo + ' AE)';
    case 'spawn': return 'spawned, ' + e.hp + '/' + (e.hpMax || e.hp) + ' hp';
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

// ---------------------------------------------- who a unit is, what it does

/**
 * WHAT THE THING IS IN THE GAME, not only which number it carries. ""#1044""
 * is an identifier; ""#1044 basicInfantry"" is a unit one can recognise in the
 * match. Both lists, the log and the detail panel say it the same way.
 * <p>
 * The LIVE role wins over the recorded one: a construction site becomes a
 * building mid-match, and only the reconstructed state knows which of the two
 * this tick is looking at.
 */
function roleNameOf(id) {
  const live = world.get(id), row = units.get(id);
  if (!live && !row) return 'unit';
  if (live && live.site) return 'construction site';
  const role = live ? live.role : row.role;
  return ROLE_NAME[role] || 'unit';
}

function slotOf(id) {
  const live = world.get(id), row = units.get(id);
  return live ? live.slot : (row ? row.slot : -1);
}

/** ""#1044 basicInfantry"" — short enough for a log row. */
function shortLabel(id) {
  const slot = slotOf(id);
  const colour = slot >= 0 ? SLOT_COLOURS[slot % SLOT_COLOURS.length] : '#c9d1d9';
  return '<span style=""color:' + colour + '"">#' + id + ' ' + roleNameOf(id) + '</span>';
}

/** The same with its owner, and a mark when it is not alive at this tick. */
function unitLabel(id) {
  const slot = slotOf(id);
  return shortLabel(id) + (slot >= 0 ? ' · slot ' + slot : '') +
    (world.has(id) ? '' : ' <span class=""sub"">(gone)</span>');
}

/** Cells between two Q16.16 points — ""how far away is what it shoots at"". */
function cellDistance(a, b) {
  const dx = (a[0] - b[0]) / ONE, dy = (a[1] - b[1]) / ONE;
  return Math.round(Math.sqrt(dx * dx + dy * dy));
}

/** The tick the newest of these events happened at, at or before `tick`. */
function sinceTick(id, kinds) {
  const list = eventsById.get(id);
  if (!list) return -1;
  for (let i = list.length - 1; i >= 0; i--) {
    if (list[i].t <= tick && kinds.indexOf(list[i].k) >= 0) return list[i].t;
  }
  return -1;
}

/** How many attackers the detail panel names before it counts the rest. */
const ATTACKER_ROWS = 6;

/** Everyone who is shooting at this unit right now. */
function attackersOf(id) {
  const out = [];
  for (const u of world.values()) if (u.attack === id) out.push(u.id);
  return out;
}

/** The owner's headquarters, for the one direction question below. */
function baseOf(slot) {
  for (const u of world.values()) {
    if (u.slot === slot && u.role === 3 && !u.site) return posAt(u.id, tick);
  }
  return null;
}

/**
 * Whether the unit is walking TOWARDS its own base — the visible half of a
 * retreat, and no more than that.
 * <p>
 * The AI pulls a wounded unit back to the STAGING CELL, and neither that cell
 * nor the profile's retreat percentage is in the artifacts: the white rim is
 * a fixed 25 % drawing marker, not the rule that fires. So this is read off
 * the geometry and labelled derived, instead of printing a decision the
 * recording does not carry.
 */
function walkingHome(u) {
  // Combat units only. A harvester walks home every trip — saying so there
  // would print the word ""retreat"" over the economy running normally, which
  // is the same mistake as the white rim on a builder was.
  if (shapeOf(u) !== 4) return false;
  if (!u.moving || u.goalX < 0) return false;
  const home = baseOf(u.slot), here = posAt(u.id, tick);
  if (!home || !here) return false;
  // Two cells of slack: a unit that circles its goal is not walking home.
  return cellDistance([u.goalX * ONE, u.goalY * ONE], home) + 2 < cellDistance(here, home);
}

/**
 * ONE SENTENCE FOR WHAT THE UNIT IS DOING at this tick. The panel used to say
 * ""moving"" and leave the reader to join ""attacking #1044"", ""goal 109,109""
 * and a white rim into a behaviour by themselves.
 * <p>
 * Same priority the recorder and the map lines use: an attack says more about
 * a unit than the walk that carries it there.
 */
function behaviourOf(u) {
  const here = posAt(u.id, tick);
  const marks = [];
  let main, movementIsTheSentence = false;

  if (u.site) {
    main = 'being built (def ' + u.siteDef + ')';
  } else if (u.attack) {
    const target = posAt(u.attack, tick);
    const away = here && target ? ', ' + cellDistance(here, target) + ' cells away' : '';
    const since = sinceTick(u.id, ['attackStart', 'attackSwitch']);
    main = (world.has(u.attack) ? 'attacking ' : 'attacking the gone ') + unitLabel(u.attack) + away +
      (since >= 0 ? ' <span class=""sub"">· since tick ' + since + '</span>' : '');
  } else if (u.cargo) {
    main = 'carrying ' + u.cargoAE + ' AE back to a refinery';
  } else if (u.field) {
    main = 'harvesting field ' + u.field +
      (Number.isFinite(u.fx) ? ' at ' + cell(Math.floor(u.fx / ONE), Math.floor(u.fy / ONE)) : '');
  } else if (u.moving && u.goalX >= 0) {
    const away = here ? ' (' + cellDistance(here, [u.goalX * ONE, u.goalY * ONE]) + ' cells to go)' : '';
    main = 'walking to ' + cell(u.goalX, u.goalY) + away;
    movementIsTheSentence = true;
  } else if (u.moving) {
    main = 'moving';
    movementIsTheSentence = true;
  } else {
    main = 'standing';
    movementIsTheSentence = true;
  }

  // The walk belongs in the line even when something else is the headline: a
  // unit that shoots while closing in and one that shoots where it stands are
  // two different behaviours, and the panel used to show both as ""attacking"".
  if (!movementIsTheSentence && !u.site) marks.push(u.moving ? 'on the move' : 'not moving');
  // …unless carrying IS the sentence, and then it is in there already.
  if (u.cargo && u.attack) marks.push('cargo ' + u.cargoAE + ' AE');
  if (u.stuck) marks.push('<span class=""warn"">STUCK</span>');
  if (u.below) marks.push('<span class=""warn"">below the retreat mark</span>');
  if (walkingHome(u)) marks.push('<span class=""derived"">heading for its own base (derived)</span>');
  return main + (marks.length ? ' · ' + marks.join(' · ') : '');
}

function renderDetail() {
  const detail = document.getElementById('detail');
  if (selected === null) {
    detail.innerHTML = '<span class=""sub"">no unit selected — click one in the list or on the map</span>';
    return;
  }

  const live = world.get(selected);
  const unit = units.get(selected);
  const rows = [];

  rows.push(['unit', (live || unit)
    ? SHAPE_GLYPH[shapeOf(live || { site:false, role:unit.role })] + ' ' + unitLabel(selected)
    : '#' + selected + ' <span class=""sub"">(not in this run)</span>']);
  if (unit) {
    rows.push(['life', 'tick ' + unit.firstTick + '…' + unit.lastTick + (unit.died ? ' · died' : '')]);
  }
  if (live) {
    const p = posAt(selected, tick);
    rows.push(['health', live.hp + '/' + live.hpMax + '  (' + healthPercentOf(live) + '%)']);
    rows.push(['cell', p ? Math.floor(p[0] / ONE) + ',' + Math.floor(p[1] / ONE) : '—']);
    rows.push(['doing', behaviourOf(live)]);
    rows.push(['goal', cell(live.goalX, live.goalY)]);
    rows.push(['order', cell(live.orderX, live.orderY)]);
    rows.push(['attacking', live.attack ? unitLabel(live.attack) : '—']);

    // THE OTHER HALF OF A FIGHT. Who this unit shoots at is one event away;
    // who shoots at IT was, until now, a search through the log — and that is
    // the question one has while watching something die.
    const attackers = attackersOf(selected);
    rows.push(['attacked by', attackers.length
      ? attackers.length + (attackers.length === 1 ? ' unit<br>' : ' units<br>') +
        attackers.slice(0, ATTACKER_ROWS).map(unitLabel).join('<br>') +
        (attackers.length > ATTACKER_ROWS
          ? '<br><span class=""sub"">… and ' + (attackers.length - ATTACKER_ROWS) + ' more</span>' : '')
      : '<span class=""sub"">nobody — nothing has this one as its target</span>']);
  } else {
    rows.push(['at this tick', unit && unit.firstTick > tick ? 'not born yet' : 'dead']);
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

  detail.innerHTML = '<h2>#' + selected + ' ' + roleNameOf(selected) + '</h2><dl>' +
    rows.map(r => '<dt>' + r[0] + '</dt><dd>' + r[1] + '</dd>').join('') + '</dl>';
}

// ------------------------------------------------------- the match log

/** The window of rows the log renders around the current tick. */
const LOG_WINDOW = 500;

function logFiltered() {
  const slot = +logSlot.value, group = document.getElementById('logKind').value;
  const only = document.getElementById('logOnlySelected').checked;
  return events.filter(e =>
    (slot < 0 || e.slot === slot) &&
    (group === 'all' || EVENT_GROUP[e.k] === group) &&
    (!only || e.id === selected));
}

/** Rows the log currently shows, so a click can resolve back to its event. */
let logRows = [];

function renderLog() {
  const box = document.getElementById('logBox');
  const list = logFiltered();
  const note = document.getElementById('logNote');
  logRows = list;
  if (!list.length) { box.innerHTML = '<div class=""row sub"">nothing matches the filter</div>'; note.textContent = '—'; return; }

  // Binary search on the list itself — building an array of ticks for it would
  // allocate thousands of numbers per redraw, and this redraws while playing.
  let lo = 0, hi = list.length;
  while (lo < hi) { const mid = (lo + hi) >> 1; if (list[mid].t < tick + 1) lo = mid + 1; else hi = mid; }
  const here = lo;
  const from = Math.max(0, here - Math.floor(LOG_WINDOW / 2));
  const to = Math.min(list.length, from + LOG_WINDOW);

  const rows = [];
  for (let i = from; i < to; i++) {
    const e = list[i];
    const when = e.t < tick ? 'past' : e.t > tick ? 'future' : 'now';
    rows.push('<div class=""row ' + when + (e.id === selected ? ' sel' : '') + '"" data-i=""' + i + '"">' +
      't' + String(e.t).padStart(5, ' ') + ' ' +
      '<b class=""id"" style=""color:' + SLOT_COLOURS[e.slot % SLOT_COLOURS.length] + '"">#' + e.id + '</b> ' +
      '<span style=""color:' + (EVENT_COLOUR[e.k] || '#8b949e') + '"">' + e.k + '</span> ' +
      describe(e) + '</div>');
  }
  box.innerHTML = rows.join('');

  note.textContent = 'showing ' + (from + 1) + '…' + to + ' of ' + list.length +
    ' matching events (' + events.length + ' in the match)';

  if (document.getElementById('logFollow').checked) {
    const current = box.querySelector('.now') || box.querySelector('.future');
    // scrollTop, NOT scrollIntoView: that one scrolls every ancestor that can
    // scroll, so with the page inside a frame it dragged the whole window
    // around on every redraw — sixteen times a second while playing.
    if (current) box.scrollTop = current.offsetTop - box.clientHeight / 2 + current.offsetHeight / 2;
  }
}

// ------------------------------------------------------------ navigation

function seek(target) {
  tick = Math.min(lastTick, Math.max(0, Math.round(target)));
  try {
    history.replaceState(null, '', selected === null ? '#t=' + tick : '#u=' + selected + '&t=' + tick);
  } catch (ignored) {}
  draw();
}

function step(delta) { seek(tick + delta); }

/** Jumps to the next or previous event — of the selection, or of the match. */
function jumpEvent(direction, mine) {
  const list = mine ? (selected === null ? null : eventsById.get(selected)) : events;
  if (!list || !list.length) return;
  const next = direction > 0
    ? list.find(e => e.t > tick)
    : [...list].reverse().find(e => e.t < tick);
  if (next) seek(next.t);
}

canvas.addEventListener('click', ev => {
  if (dragged > 4) return;                   // das war ein Verschieben, keine Auswahl
  const rect = canvas.getBoundingClientRect();
  const x = (ev.clientX - rect.left) * (canvas.width / rect.width);
  const y = (ev.clientY - rect.top) * (canvas.height / rect.height);

  let best = null, bestDistance = 18 * 18;
  for (const u of world.values()) {
    const p = posAt(u.id, tick);
    if (!p) continue;
    const dx = px(p[0]) - x, dy = py(p[1]) - y;
    const d = dx * dx + dy * dy;
    if (d < bestDistance) { bestDistance = d; best = u.id; }
  }
  if (best !== null) select(best); else { selected = null; draw(); }
});

scrub.addEventListener('input', () => seek(+scrub.value));
document.getElementById('first').addEventListener('click', () => seek(0));
document.getElementById('last').addEventListener('click', () => seek(lastTick));
document.getElementById('back25').addEventListener('click', () => step(-25));
document.getElementById('back1').addEventListener('click', () => step(-1));
document.getElementById('fwd1').addEventListener('click', () => step(1));
document.getElementById('fwd25').addEventListener('click', () => step(25));

function zoomBy(factor) {
  view.zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, view.zoom * factor));
  draw();
}
document.getElementById('zoomIn').addEventListener('click', () => zoomBy(1.4));
document.getElementById('zoomOut').addEventListener('click', () => zoomBy(1 / 1.4));
document.getElementById('fit').addEventListener('click', fitMap);
document.getElementById('focus').addEventListener('click', focusSelected);

for (const id of ['layerLines','layerHealth','layerFog','layerTrail','layerAllTrails',
                  'trailSpan','filterShape','filterDead','logKind','logOnlySelected','logFollow']) {
  document.getElementById(id).addEventListener('change', draw);
}
for (const select of [fogSlot, trailSlot, filterSlot, logSlot]) {
  select.addEventListener('change', draw);
}

function togglePlay() {
  playing = !playing;
  document.getElementById('play').textContent = playing ? 'pause' : 'play';
  clearInterval(timer);
  if (!playing) return;
  timer = setInterval(() => {
    const stepSize = +document.getElementById('speed').value;
    seek(tick >= lastTick ? 0 : tick + stepSize);
  }, 60);
}
document.getElementById('play').addEventListener('click', togglePlay);

function toggleSide() {
  const collapsed = document.body.classList.toggle('collapsed');
  document.getElementById('sideToggle').textContent = collapsed ? '«' : '»';
  draw();                                    // die Karte nimmt den frei gewordenen Platz
}
document.getElementById('sideToggle').addEventListener('click', toggleSide);
addEventListener('resize', draw);

// Reiter statt Spalten nebeneinander: vier Flächen, die sich denselben Platz
// teilen, statt einer Seite, die nach unten wächst und die Karte wegschiebt.
const TABS = ['units', 'log', 'layers', 'legend'];

function showTab(name) {
  for (const tab of TABS) document.getElementById('tab-' + tab).hidden = tab !== name;
  for (const button of document.querySelectorAll('.tabs button')) {
    button.classList.toggle('on', button.dataset.tab === name);
  }
  draw();                                    // das Protokoll muss neu mitlaufen
}
for (const button of document.querySelectorAll('.tabs button')) {
  button.addEventListener('click', () => showTab(button.dataset.tab));
}

addEventListener('keydown', e => {
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
  const big = e.shiftKey ? 25 : 1;
  if (e.key === 'ArrowRight') { step(big); e.preventDefault(); }
  else if (e.key === 'ArrowLeft') { step(-big); e.preventDefault(); }
  else if (e.key === 'Home') seek(0);
  else if (e.key === 'End') seek(lastTick);
  else if (e.key === ' ') { togglePlay(); e.preventDefault(); }
  else if (e.key === 'n') jumpEvent(1, true);
  else if (e.key === 'p') jumpEvent(-1, true);
  else if (e.key === 'N') jumpEvent(1, false);
  else if (e.key === 'P') jumpEvent(-1, false);
  else if (e.key === 's') toggleSide();
  else if (e.key === 'f') focusSelected();
  else if (e.key === '+' || e.key === '=') zoomBy(1.4);
  else if (e.key === '-') zoomBy(1 / 1.4);
  else if (e.key === '0') fitMap();
  else if (e.key >= '1' && e.key <= '4') showTab(TABS[+e.key - 1]);
  else if (e.key === 'Escape') { selected = null; draw(); }
});
</script>
</body>
</html>
";
    }
}
