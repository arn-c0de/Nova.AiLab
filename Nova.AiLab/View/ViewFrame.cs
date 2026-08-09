using System.Collections.Generic;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>What an entity is drawn as (plan section 3.4, "Form").</summary>
    public enum ViewShape
    {
        Building = 0,       // ▣
        ConstructionSite = 1, // ▢
        Builder = 2,        // ✚
        Harvester = 3,      // ●
        Combat = 4,         // ▲
    }

    /// <summary>What the line from an entity points at, and therefore its colour.</summary>
    public enum ViewLine
    {
        None = 0,
        Attack = 1,   // red   — AttackTarget
        Harvest = 2,  // green — HarvestFieldId
        Move = 3,     // blue  — GoalGridPos while IsMoving
    }

    /// <summary>Per-entity flags. Bit values, so one integer carries them all.</summary>
    public static class ViewFlags
    {
        /// <summary>Drawn hollow: the harvester is on its way home with cargo.</summary>
        public const int ReturningCargo = 1;
        /// <summary>Rim marker: below the retreat threshold that E7 will act on.</summary>
        public const int BelowRetreatThreshold = 2;
        public const int Moving = 4;
    }

    /// <summary>One entity in one frame. Integers only — positions are Q16.16 raw.</summary>
    public struct ViewEntity
    {
        /// <summary>
        /// The raw entity id — index and version in one integer, the value
        /// <c>UnitCommandStateView.ToRawEntityId</c> produces.
        /// <para>
        /// WITHOUT THIS THERE IS NO ROUTE. An entry in frame n cannot be tied
        /// to an entry in frame n+1 by position alone; what is left is a point
        /// cloud per tick. The VERSION half is what makes a reused pool slot
        /// read as a NEW unit instead of the old one teleporting — the same
        /// property <c>TraceCollector</c> builds its loss attribution on.
        /// </para>
        /// </summary>
        public uint Id;

        public byte Slot;
        public ViewShape Shape;
        public int XRaw;
        public int YRaw;
        /// <summary>
        /// The brightness channel: <c>CurrentHealth * 100 / MaxHealth</c> — but
        /// BUILD PROGRESS on a construction site, which sits at 1 HP its whole
        /// life and would otherwise encode nothing.
        /// </summary>
        public int HealthPercent;
        public int Flags;
        public ViewLine Line;
        /// <summary>Line endpoint in Q16.16 raw; meaningless when <see cref="Line"/> is None.</summary>
        public int LineXRaw;
        public int LineYRaw;
    }

    /// <summary>The per-slot header line of section 3.4.</summary>
    public struct ViewSlotHeader
    {
        public byte Slot;
        public long Credits;
        public int PowerMargin;
        public int ArmySize;
        public int VisibleEnemies;
    }

    /// <summary>
    /// One view frame: everything needed to draw one tick, and nothing that
    /// could be recomputed from it.
    /// <para>
    /// The JSON is deliberately terse — short keys and positional arrays rather
    /// than named objects per entity. At roughly 200 entities and hundreds of
    /// frames per match the readable form would multiply the file for no gain;
    /// nothing reads this by hand, two renderers read it by code.
    /// </para>
    /// </summary>
    public sealed class ViewFrame
    {
        public uint Tick;
        public List<ViewEntity> Entities = new List<ViewEntity>();
        public ViewSlotHeader[] Headers;

        /// <summary>
        /// Run-length encoded fog per slot, or null when the layer is off:
        /// pairs of (cellCount, visionState). The full mask would be 16 KB per
        /// team per frame; the encoded one is a few hundred bytes, because a
        /// fog map is overwhelmingly one long unexplored run.
        /// </summary>
        public int[][] FogRle;

        public string ToJsonLine()
        {
            var json = new StringBuilder(64 + Entities.Count * 48);
            json.Append("{\"t\":").Append(Tick);

            json.Append(",\"h\":[");
            for (int i = 0; i < Headers.Length; i++)
            {
                if (i > 0) json.Append(',');
                ViewSlotHeader h = Headers[i];
                json.Append('[').Append(h.Slot).Append(',').Append(h.Credits).Append(',')
                    .Append(h.PowerMargin).Append(',').Append(h.ArmySize).Append(',')
                    .Append(h.VisibleEnemies).Append(']');
            }
            json.Append(']');

            json.Append(",\"e\":[");
            for (int i = 0; i < Entities.Count; i++)
            {
                if (i > 0) json.Append(',');
                ViewEntity e = Entities[i];
                // The id is APPENDED, not inserted: a view.ndjson written
                // before it existed still reads correctly in the first nine
                // columns, and the player switches trails off when the tenth
                // is missing instead of drawing a route it cannot know.
                json.Append('[').Append(e.Slot).Append(',').Append((int)e.Shape).Append(',')
                    .Append(e.XRaw).Append(',').Append(e.YRaw).Append(',')
                    .Append(e.HealthPercent).Append(',').Append(e.Flags).Append(',')
                    .Append((int)e.Line).Append(',').Append(e.LineXRaw).Append(',')
                    .Append(e.LineYRaw).Append(',').Append(e.Id).Append(']');
            }
            json.Append(']');

            if (FogRle != null)
            {
                json.Append(",\"fog\":[");
                for (int slot = 0; slot < FogRle.Length; slot++)
                {
                    if (slot > 0) json.Append(',');
                    json.Append('[');
                    int[] runs = FogRle[slot];
                    for (int i = 0; i < runs.Length; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append(runs[i]);
                    }
                    json.Append(']');
                }
                json.Append(']');
            }

            json.Append('}');
            return json.ToString();
        }
    }
}
