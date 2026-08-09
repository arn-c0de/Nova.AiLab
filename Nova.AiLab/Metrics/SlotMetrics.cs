using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// One slot's metrics at one metric tick (plan section 3.3).
    /// <para>
    /// HARD RULE: <b>no float leaves the simulation</b>. Every field here is an
    /// integer, positions would be Q16.16 raw values. Otherwise comparing two
    /// runs is luck instead of arithmetic.
    /// </para>
    /// <para>
    /// Three deviations from the catalogue in section 3.3, each because the
    /// named number does not exist in the committed state today. Naming them
    /// is the point — a metric that measures something other than its name is
    /// worse than a missing one:
    /// </para>
    /// <list type="bullet">
    /// <item><c>damageDealt</c>/<c>damageTaken</c>/<c>kills</c>: there is no
    /// damage ledger in the state. What IS observable is
    /// <see cref="UnitsLost"/> and <see cref="HealthLost"/> per slot, sampled
    /// between two metric ticks. With two slots the report derives dealt from
    /// the opponent's taken; with more slots damage is not attributable at
    /// all, and pretending otherwise would invent a number.</item>
    /// <item><c>queueStallTicks</c>: a stall has no marker in the state. The
    /// documented production brake does — low power halves
    /// <c>ProductionSpeedMultiplierQ16</c> — so <see cref="LowPowerTicks"/>
    /// counts exactly that, under its own name.</item>
    /// <item><b>The accumulating fields are RUNNING TOTALS</b>
    /// (<see cref="LowPowerTicks"/>, <see cref="UnitsLost"/>,
    /// <see cref="HealthLost"/>): every sample carries the value since the
    /// start of the match, not since the previous sample. Reading them as
    /// per-interval numbers turns a flat economy into a collapsing one.</item>
    /// <item><c>activeGoal</c>/<c>goalUtility</c>/<c>goalSwitches</c>: the goal
    /// system does not exist before E7. These fields appear when it does, not
    /// as zeros pretending to be measurements.</item>
    /// </list>
    /// </summary>
    public sealed class SlotMetrics
    {
        public byte Slot;

        // Economy — PlayerEconomyState
        public long Credits;
        public int PowerProvided;
        public int PowerRequired;
        public int IsLowPower;

        // Harvest — UnitState, TryGetField
        public int Harvesters;
        public int IdleHarvesters;
        public long CargoInTransitAE;
        public long FieldReserveAE;

        // Construction — TryGetSite, IsCompletedPlacement
        public int SitesOpen;
        /// <summary>The nine MS-1 building roles, HQ (3) through DefensePlatform (11).</summary>
        public int[] BuildingsByRole = new int[BuildingRoleCount];

        // Production — TryGetProducer / TryGetQueueEntry
        public int Producers;
        public int QueuedUnits;

        /// <summary>
        /// Ticks the slot has spent under the low-power brake SINCE THE START
        /// OF THE MATCH — a running total, not a per-interval figure. Take a
        /// difference between two samples for the interval.
        /// </summary>
        public int LowPowerTicks;

        // Army — entity scan
        public int ArmySize;
        public long ArmyHealthSum;

        /// <summary>
        /// Owned ENTITIES that vanished since the start of the match — a
        /// running total.
        /// <para>
        /// The name says units and the number counts entities: a destroyed
        /// building and an abandoned construction site are entities too, and
        /// both land here. That is the honest reading of what the entity scan
        /// can see, and it matters when a slot loses a refinery rather than a
        /// squad. Kept under this name because archived result sets key on
        /// <c>unitsLost</c>.
        /// </para>
        /// </summary>
        public int UnitsLost;

        /// <summary>
        /// Health lost since the start of the match, dead entities included —
        /// a running total, same reading as <see cref="UnitsLost"/>.
        /// </summary>
        public long HealthLost;

        // Sight — GetVisibleEntities
        public int VisibleEnemyUnits;
        public int VisibleEnemyBuildings;

        // AI — session sequence vs. host watermark
        public int IntentsSubmitted;
        public int IntentsAccepted;
        /// <summary>
        /// The underestimated number (plan section 3.3): where the AI runs into
        /// executor rules. <c>Submit()</c> deliberately does not evaluate the
        /// verdict, so this is the only place it becomes visible.
        /// </summary>
        public int IntentsRejected;

        public const int BuildingRoleCount = 9;

        /// <summary>Lowest role value that is a building (UnitRole.HQ).</summary>
        public const int FirstBuildingRole = 3;

        /// <summary>This slot's numbers as one JSON object — comparison shorthand.</summary>
        public string ToJsonOfOneSlot()
        {
            var json = new StringBuilder(256);
            AppendJson(json);
            return json.ToString();
        }

        public void AppendJson(StringBuilder json)
        {
            json.Append("{\"slot\":").Append(Slot)
                .Append(",\"credits\":").Append(Credits)
                .Append(",\"powerProvided\":").Append(PowerProvided)
                .Append(",\"powerRequired\":").Append(PowerRequired)
                .Append(",\"isLowPower\":").Append(IsLowPower)
                .Append(",\"harvesters\":").Append(Harvesters)
                .Append(",\"idleHarvesters\":").Append(IdleHarvesters)
                .Append(",\"cargoInTransitAE\":").Append(CargoInTransitAE)
                .Append(",\"fieldReserveAE\":").Append(FieldReserveAE)
                .Append(",\"sitesOpen\":").Append(SitesOpen)
                .Append(",\"buildingsByRole\":[");
            for (int i = 0; i < BuildingsByRole.Length; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(BuildingsByRole[i]);
            }
            json.Append("],\"producers\":").Append(Producers)
                .Append(",\"queuedUnits\":").Append(QueuedUnits)
                .Append(",\"lowPowerTicks\":").Append(LowPowerTicks)
                .Append(",\"armySize\":").Append(ArmySize)
                .Append(",\"armyHealthSum\":").Append(ArmyHealthSum)
                .Append(",\"unitsLost\":").Append(UnitsLost)
                .Append(",\"healthLost\":").Append(HealthLost)
                .Append(",\"visibleEnemyUnits\":").Append(VisibleEnemyUnits)
                .Append(",\"visibleEnemyBuildings\":").Append(VisibleEnemyBuildings)
                .Append(",\"intentsSubmitted\":").Append(IntentsSubmitted)
                .Append(",\"intentsAccepted\":").Append(IntentsAccepted)
                .Append(",\"intentsRejected\":").Append(IntentsRejected)
                .Append('}');
        }
    }

    /// <summary>One metric tick: the tick number and every slot's numbers.</summary>
    public sealed class MetricSample
    {
        public uint Tick;
        public SlotMetrics[] Slots;

        public string ToJsonLine()
        {
            var json = new StringBuilder(256 * Slots.Length);
            json.Append("{\"tick\":").Append(Tick).Append(",\"slots\":[");
            for (int i = 0; i < Slots.Length; i++)
            {
                if (i > 0) json.Append(',');
                Slots[i].AppendJson(json);
            }
            json.Append("]}");
            return json.ToString();
        }
    }
}
