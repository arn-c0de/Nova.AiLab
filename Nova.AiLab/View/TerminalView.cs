using System;
using System.Text;
using Nova.Core;

namespace Nova.AiLab
{
    /// <summary>
    /// The live half of the view window (plan section 3.4): an ANSI grid,
    /// downscaled, no dependencies. It answers one question — <i>is something
    /// going wrong right now?</i> — and deliberately not more; the HTML player
    /// is where a run gets picked apart afterwards.
    /// <para>
    /// It reads the SAME frame stream the recorder writes (decision 10), so
    /// there is no second capture path that could disagree with the file.
    /// </para>
    /// </summary>
    public sealed class TerminalView
    {
        private const string Esc = "\u001b[";
        private const string Reset = Esc + "0m";
        private const string Invert = Esc + "7m";
        private const string ClearToEol = Esc + "K";

        /// <summary>Owner colours, slot 0..7 (section 3.4, "Grundfarbe").</summary>
        private static readonly string[] SlotColours =
        {
            Esc + "38;5;39m",  // blue
            Esc + "38;5;203m", // red
            Esc + "38;5;41m",  // green
            Esc + "38;5;221m", // yellow
            Esc + "38;5;141m", // violet
            Esc + "38;5;80m",  // cyan
            Esc + "38;5;209m", // orange
            Esc + "38;5;250m", // grey
        };

        /// <summary>Shape glyphs, indexed by <see cref="ViewShape"/>.</summary>
        private static readonly char[] Glyphs = { '#', '.', '+', 'o', '^' };

        private readonly int _columns;
        private readonly int _rows;
        private readonly int _mapWidth;
        private readonly int _mapHeight;
        private readonly char[] _glyph;
        private readonly byte[] _owner;
        private readonly bool[] _weak;
        private bool _drewOnce;

        public TerminalView(int mapWidth, int mapHeight, int columns = 64, int rows = 32)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            _columns = columns;
            _rows = rows;
            _glyph = new char[columns * rows];
            _owner = new byte[columns * rows];
            _weak = new bool[columns * rows];
        }

        public void Draw(ViewFrame frame)
        {
            Array.Fill(_glyph, ' ');
            Array.Fill(_owner, (byte)0);
            Array.Fill(_weak, false);

            for (int i = 0; i < frame.Entities.Count; i++)
            {
                ViewEntity e = frame.Entities[i];
                int cellX = SimFixed.FromRaw(e.XRaw).Floor();
                int cellY = SimFixed.FromRaw(e.YRaw).Floor();

                int column = cellX * _columns / _mapWidth;
                int row = cellY * _rows / _mapHeight;
                if (column < 0 || row < 0 || column >= _columns || row >= _rows) continue;

                int index = row * _columns + column;
                // Downscaling collapses cells, so the more telling glyph wins:
                // a fighting unit matters more than the warehouse it stands on.
                if (_glyph[index] != ' ' && (int)e.Shape < ShapePriority(_glyph[index])) continue;

                _glyph[index] = Glyphs[(int)e.Shape];
                _owner[index] = e.Slot;
                _weak[index] = (e.Flags & ViewFlags.BelowRetreatThreshold) != 0;
            }

            var screen = new StringBuilder(_columns * _rows * 12);
            // Redraw in place: jump back over what was printed last time.
            if (_drewOnce) screen.Append(Esc).Append(_rows + frame.Headers.Length + 1).Append('A');
            _drewOnce = true;

            screen.Append("tick ").Append(frame.Tick).Append(ClearToEol).Append('\n');
            for (int slot = 0; slot < frame.Headers.Length; slot++)
            {
                ViewSlotHeader h = frame.Headers[slot];
                screen.Append(SlotColours[h.Slot % SlotColours.Length])
                      .Append($"slot {h.Slot}  credits {h.Credits,7}  power {h.PowerMargin,4}  " +
                              $"army {h.ArmySize,3}  sees {h.VisibleEnemies,3}")
                      .Append(Reset).Append(ClearToEol).Append('\n');
            }

            for (int row = _rows - 1; row >= 0; row--) // y upward, like the map
            {
                for (int column = 0; column < _columns; column++)
                {
                    int index = row * _columns + column;
                    char glyph = _glyph[index];
                    if (glyph == ' ')
                    {
                        screen.Append(' ');
                        continue;
                    }
                    screen.Append(SlotColours[_owner[index] % SlotColours.Length]);
                    if (_weak[index]) screen.Append(Invert); // rim marker
                    screen.Append(glyph).Append(Reset);
                }
                screen.Append(ClearToEol).Append('\n');
            }

            Console.Write(screen.ToString());
        }

        /// <summary>Glyph back to shape index, for the collapse rule above.</summary>
        private static int ShapePriority(char glyph)
        {
            for (int i = 0; i < Glyphs.Length; i++)
            {
                if (Glyphs[i] == glyph) return i;
            }
            return -1;
        }

        public static string Legend =>
            "legend: # building  . site  + builder  o harvester  ^ combat   " +
            "inverted = below retreat threshold, colour = owner slot";
    }
}
