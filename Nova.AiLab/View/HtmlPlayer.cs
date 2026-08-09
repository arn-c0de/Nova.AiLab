using System;
using System.Globalization;
using System.IO;
using System.Text;

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
            // The Replace chain already produces the finished string; the
            // StringBuilder that used to wrap it copied 54 kB once more for
            // nothing.
            return Template
                .Replace("__MAP_WIDTH__", mapWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("__MAP_HEIGHT__", mapHeight.ToString(CultureInfo.InvariantCulture))
                .Replace("__SLOT_COUNT__", slotCount.ToString(CultureInfo.InvariantCulture))
                .Replace("__SEED__", "0x" + seed.ToString("X", CultureInfo.InvariantCulture))
                .Replace("__VIEW_FILE__", RunArtifacts.ViewFileName)
                .Replace("__TRACKS_FILE__", RunArtifacts.TracksFileName)
                .Replace("__EVENTS_FILE__", RunArtifacts.EventsFileName)
                .Replace("__UNITS_FILE__", RunArtifacts.UnitsFileName);
        }

        /// <summary>
        /// The page, loaded from an embedded resource instead of a 1.300-line
        /// string literal in this file.
        /// <para>
        /// THE OUTPUT IS UNCHANGED and stays one self-contained file: an
        /// artifact directory is copyable as a unit and opens with a
        /// double-click. That is what the comment standing here used to defend
        /// — and it was defending it in the wrong place. The argument is about
        /// what <see cref="Build"/> WRITES, not about where the source lives.
        /// As a literal, 1.300 lines of HTML, CSS and JavaScript had no syntax
        /// highlighting, no linter, no formatter, and every quote inside them
        /// was doubled: a page nobody could edit safely, guarding a page
        /// anyone should be able to fix.
        /// </para>
        /// </summary>
        private const string ResourceName = "Nova.AiLab.View.Player.template.html";

        private static readonly string Template = LoadTemplate();

        private static string LoadTemplate()
        {
            using Stream stream = typeof(HtmlPlayer).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                throw new InvalidOperationException(
                    $"[AiLab] the embedded player template '{ResourceName}' is not in this assembly. " +
                    "Nova.AiLab.csproj has to carry View/Player.template.html as an EmbeddedResource " +
                    "under exactly that LogicalName.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
