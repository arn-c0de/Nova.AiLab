using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// The admin panel's counterpart: a held match, a page to watch it on, and
    /// four verbs to interfere with it.
    /// <para>
    /// ONLY 127.0.0.1, and that is a rule rather than a default. This thing can
    /// be told to change what an AI does; it has no authentication and will
    /// never have any, so the only defensible reach is the machine it runs on.
    /// The prefix is built from the loopback address and nothing on the command
    /// line can widen it.
    /// </para>
    /// <para>
    /// STANDARD LIBRARY ONLY, like <c>report/gui_server.py</c> beside it. The
    /// lab is measuring equipment; a dependency here is a dependency in every
    /// measurement it ever produces.
    /// </para>
    /// <para>
    /// WHY THIS IS NOT <c>gui_server.py</c>. The plan sketched the endpoints as
    /// an addition to the Python control page, and that cannot work: the match
    /// is C#, and holding it means holding it in the process that owns the
    /// kernel. The Python server keeps its job — starting runs, picking
    /// branches, laying two results side by side — and this one keeps the single
    /// job it can do alone.
    /// </para>
    /// </summary>
    public sealed class LiveServer
    {
        private readonly LiveMatch _match;
        private readonly HttpListener _listener = new HttpListener();
        private readonly string _outputDirectory;
        private Thread _clock;
        private volatile bool _running;

        public LiveServer(LiveMatch match, int port, string outputDirectory)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _outputDirectory = outputDirectory;
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public void Run()
        {
            _running = true;
            _listener.Start();

            // The clock is the only thread that steps on its own; everything
            // else reaches the match through LiveMatch's gate.
            _clock = new Thread(Tick) { IsBackground = true, Name = "AiLabLiveClock" };
            _clock.Start();

            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break; // stopped
                }
                try
                {
                    Handle(context);
                }
                catch (Exception error)
                {
                    Send(context, 500, "text/plain", error.Message);
                }
            }
        }

        private void Tick()
        {
            while (_running)
            {
                int speed = _match.Paused ? 0 : _match.TicksPerSecond;
                if (speed <= 0 || _match.Decided)
                {
                    Thread.Sleep(50);
                    continue;
                }
                // A tenth of a second of simulated time per wake-up: fine enough
                // to look continuous, coarse enough not to spend the session in
                // the scheduler.
                int batch = Math.Max(1, speed / 10);
                _match.Step(batch);
                Thread.Sleep(1000 * batch / speed);
            }
        }

        private void Handle(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;
            switch (path)
            {
                case "/":
                case "/index.html":
                    Send(context, 200, "text/html; charset=utf-8", LivePage.Build(_match.Spec));
                    return;

                case "/live/state":
                    Send(context, 200, "application/json", _match.StateJson());
                    return;

                case "/live/pause":
                    _match.SetPaused(Flag(context, "on"));
                    Send(context, 200, "application/json", _match.StateJson());
                    return;

                case "/live/step":
                    _match.SetPaused(true);
                    _match.Step(Math.Max(1, Number(context, "ticks", 1)));
                    Send(context, 200, "application/json", _match.StateJson());
                    return;

                case "/live/speed":
                    _match.SetSpeed(Number(context, "value", 20));
                    Send(context, 200, "application/json", _match.StateJson());
                    return;

                case "/live/override":
                    _match.Force(
                        Number(context, "slot", GoalOverrideEntry.AllSlots),
                        (uint)Number(context, "id", 0),
                        (GoalKind)Number(context, "goal", 0));
                    Send(context, 200, "application/json", _match.StateJson());
                    return;

                case "/live/save":
                    Send(context, 200, "text/plain", Save());
                    return;

                case "/live/stop":
                    _running = false;
                    Send(context, 200, "text/plain", Save());
                    _listener.Stop();
                    return;

                default:
                    Send(context, 404, "text/plain", "no such endpoint");
                    return;
            }
        }

        /// <summary>
        /// Writes the session down: the goal recording, the interventions, and a
        /// result that ADMITS to them.
        /// <para>
        /// A directory produced here is deliberately not the shape the report
        /// builder archives. That is the point of <c>intervened</c>: a history in
        /// which somebody played along compares things that are not comparable,
        /// which is the same rule <c>COMPARISON REFUSED</c> already exists for.
        /// </para>
        /// </summary>
        private string Save()
        {
            if (string.IsNullOrEmpty(_outputDirectory)) return "no --out given, nothing written";

            Directory.CreateDirectory(_outputDirectory);
            var goals = new StringBuilder();
            foreach (GoalFrame frame in _match.Goals.Frames) goals.Append(frame.ToJsonLine()).Append('\n');
            File.WriteAllText(Path.Combine(_outputDirectory, RunArtifacts.GoalsFileName), goals.ToString());
            File.WriteAllText(Path.Combine(_outputDirectory, OverridesFileName), _match.Overrides.ToNdjson());

            var result = new StringBuilder(512);
            result.Append("{\n  \"seed\": \"0x")
                .Append(_match.Spec.Seed.ToString("X", CultureInfo.InvariantCulture)).Append("\",\n");
            result.Append("  \"finalTick\": ").Append(_match.Tick).Append(",\n");
            result.Append("  \"finalStateHash\": \"0x")
                .Append(_match.Host.Kernel.CalculateStateHash().ToString("X16", CultureInfo.InvariantCulture))
                .Append("\",\n");
            result.Append("  \"aiBehaviorId\": \"").Append(AiBehaviorId.Value).Append("\",\n");
            result.Append("  \"intervened\": ").Append(_match.Overrides.Intervened ? "true" : "false").Append(",\n");
            result.Append("  \"interventions\": ").Append(_match.Overrides.Entries.Count).Append(",\n");
            result.Append("  \"evidence\": \"")
                .Append(_match.Overrides.Intervened
                    ? "NOT A MEASUREMENT — somebody intervened. This run says what the AI COULD have done, "
                      + "never what it does, and it is not archived."
                    : "DIAGNOSIS — a lab run is never proof; what was not seen in the running game is "
                      + "reported as unseen.")
                .Append("\"\n}\n");
            File.WriteAllText(Path.Combine(_outputDirectory, RunArtifacts.ResultFileName), result.ToString());

            return $"written to {_outputDirectory} " +
                   $"({_match.Overrides.Entries.Count} interventions, " +
                   $"{_match.Goals.Frames.Count} decisions)";
        }

        public const string OverridesFileName = "overrides.ndjson";

        // ----------------------------------------------------------------

        private static bool Flag(HttpListenerContext context, string name)
        {
            string value = context.Request.QueryString[name];
            return value == null || value == "1" || value == "true";
        }

        private static int Number(HttpListenerContext context, string name, int fallback)
        {
            string value = context.Request.QueryString[name];
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static void Send(HttpListenerContext context, int status, string contentType, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            // The page is served to itself and nothing else may reach it.
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }
    }
}
