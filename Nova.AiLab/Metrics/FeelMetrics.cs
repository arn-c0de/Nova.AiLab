using System.Collections.Generic;
using System.Text;

namespace Nova.AiLab
{
    /// <summary>
    /// The four numbers that try to describe how a match FEELS, next to the
    /// ones that describe who won (NEXT-STEPS.md section 7).
    /// <para>
    /// WHY THESE EXIST. Deciding tick, losses and win rate measure strength and
    /// speed. None of them can tell "the army gathered and struck once" from
    /// "the army trickled in one by one and died the same way" — and that is
    /// the difference a player notices in the first minute. Two behaviour
    /// changes were rejected on those columns alone (journal V002, V003) and
    /// the methodology finding M001 says the rejection is open, not proven.
    /// A staging rule will RAISE the deciding tick on purpose, because waiting
    /// is the point; without a column that shows the rhythm it changed, it
    /// reads as a regression.
    /// </para>
    /// <para>
    /// STILL NOT A SCORE (decision 11). These four are not summed, not
    /// weighted and not ranked. They are four more columns a human reads.
    /// Whether a wave "announces itself" or a retreat "looks alive" has no
    /// metric and is not supposed to get one — that is what the played match
    /// decides.
    /// </para>
    /// <para>
    /// Integers only, like everything that leaves this lab. Two of the four
    /// carry a documented "not measurable here" value of <c>-1</c> instead of
    /// a zero that would read as a measurement.
    /// </para>
    /// </summary>
    public sealed class FeelMetrics
    {
        public byte Slot;

        // ---- 1 · exchange ratio ----------------------------------------

        /// <summary>
        /// Enemy entities lost per 100 own entities lost, at the end of the
        /// match. 100 means "traded evenly", 200 means "killed two for one".
        /// <c>-1</c> when the slot lost nothing at all — there is no ratio
        /// then, and a 0 would claim the opposite of what happened.
        /// <para>
        /// ONLY MEANINGFUL ONE-SIDED (M001). In symmetric self-play both
        /// slots carry the same new rule, so this number sits near 100 no
        /// matter how good or bad the rule is. It says something when a
        /// candidate WITH the behaviour plays the reference WITHOUT it —
        /// which is exactly what <see cref="TournamentRunner"/> does.
        /// </para>
        /// </summary>
        public int ExchangeRatioPercent = -1;

        // ---- 2 · combat density ----------------------------------------

        /// <summary>
        /// Metric intervals in which this slot lost at least one entity. Few
        /// intervals with large jumps = pitched battles; many intervals with
        /// jumps of one = the trickle NEXT-STEPS section 1 describes.
        /// </summary>
        public int CombatIntervals;

        /// <summary>Metric intervals in which this slot lost nothing.</summary>
        public int QuietIntervals;

        /// <summary>
        /// The largest number of entities lost inside ONE metric interval.
        /// Read together with <see cref="CombatIntervals"/>: the same total
        /// losses spread over 40 intervals and concentrated into 6 are two
        /// very different matches, and only these two columns tell them apart.
        /// </summary>
        public int LargestLossJump;

        // ---- 3 · reaction latency --------------------------------------

        /// <summary>
        /// Mean ticks between "this unit lost health" and "this unit received
        /// a NEW movement order". <c>-1</c> when the slot never answered a
        /// single damage event, which is a finding and not a zero.
        /// <para>
        /// WHY A MOVEMENT ORDER AND NOT AN INTENT COUNT. The AI submits
        /// intents on a fixed cadence whether or not anything happened, so
        /// "ticks until the next intent" measures the cadence, not the
        /// reaction. A change of <c>TargetGridPos</c> to a different VALID
        /// cell can only come from a move command (<c>UnitState.SetTarget</c>);
        /// arrival clears it through <c>Stop()</c> and is excluded. Attack
        /// targets are excluded on purpose too: D-087 auto-acquisition writes
        /// that field by itself, so a change there would credit the AI with a
        /// reaction the combat system had.
        /// </para>
        /// <para>
        /// This is the column NEXT-STEPS section 3 hangs on — "who attacks the
        /// AI notices nothing". A retreat rule has to move this number down,
        /// or it did not do what it claims.
        /// </para>
        /// </summary>
        public int MeanReactionLatencyTicks = -1;

        /// <summary>Damage events that DID get an answer — the sample size behind the mean.</summary>
        public int ReactionEvents;

        /// <summary>
        /// Damage events whose unit never received a new movement order — it
        /// kept walking where it walked until the match ended or it died.
        /// Today this is the overwhelming majority, and that IS the finding.
        /// </summary>
        public int UnansweredDamageEvents;

        // ---- 4 · actions per minute ------------------------------------

        /// <summary>
        /// <c>intentsSubmitted x 600 / finalTick</c> on the canonical 10 Hz
        /// clock — the intent column read as a human rate rather than as
        /// churn. The literature check in NEXT-STEPS section 7 uses it as a
        /// ceiling: superhuman APM reads as cheating. At r2 the AI sits near
        /// 24, far under human RTS level, so the danger is not that it acts
        /// too much.
        /// </summary>
        public int ActionsPerMinute;

        public void AppendJson(StringBuilder json)
        {
            json.Append("{\"slot\":").Append(Slot)
                .Append(",\"exchangeRatioPercent\":").Append(ExchangeRatioPercent)
                .Append(",\"combatIntervals\":").Append(CombatIntervals)
                .Append(",\"quietIntervals\":").Append(QuietIntervals)
                .Append(",\"largestLossJump\":").Append(LargestLossJump)
                .Append(",\"meanReactionLatencyTicks\":").Append(MeanReactionLatencyTicks)
                .Append(",\"reactionEvents\":").Append(ReactionEvents)
                .Append(",\"unansweredDamageEvents\":").Append(UnansweredDamageEvents)
                .Append(",\"actionsPerMinute\":").Append(ActionsPerMinute)
                .Append('}');
        }

        /// <summary>
        /// Derives every slot's feel metrics from a finished run.
        /// <para>
        /// Needs the trace (density is a per-interval difference) and the
        /// reaction tally the <see cref="TraceCollector"/> kept per tick.
        /// Without a trace the run gets no feel metrics at all rather than
        /// zeros — a missing column is honest, an invented one is not.
        /// </para>
        /// </summary>
        public static List<FeelMetrics> Compute(
            IReadOnlyList<MetricSample> trace,
            ReactionTally[] reactions,
            uint finalTick,
            int slotCount)
        {
            var all = new List<FeelMetrics>(slotCount);
            if (trace == null || trace.Count == 0) return all;

            MetricSample last = trace[trace.Count - 1];

            for (byte slot = 0; slot < slotCount && slot < last.Slots.Length; slot++)
            {
                var feel = new FeelMetrics { Slot = slot };

                // ---- exchange ratio: own losses against everyone else's ----
                long ownLost = last.Slots[slot].UnitsLost;
                long otherLost = 0;
                for (int s = 0; s < last.Slots.Length; s++)
                {
                    if (s != slot) otherLost += last.Slots[s].UnitsLost;
                }
                feel.ExchangeRatioPercent = ownLost > 0 ? (int)(otherLost * 100 / ownLost) : -1;

                // ---- density: the shape of the loss curve, not its height ----
                for (int i = 1; i < trace.Count; i++)
                {
                    if (slot >= trace[i].Slots.Length) continue;
                    int jump = trace[i].Slots[slot].UnitsLost - trace[i - 1].Slots[slot].UnitsLost;
                    if (jump > 0)
                    {
                        feel.CombatIntervals++;
                        if (jump > feel.LargestLossJump) feel.LargestLossJump = jump;
                    }
                    else
                    {
                        feel.QuietIntervals++;
                    }
                }

                // ---- reaction latency ----
                if (reactions != null && slot < reactions.Length)
                {
                    ReactionTally tally = reactions[slot];
                    feel.ReactionEvents = tally.Events;
                    feel.UnansweredDamageEvents = tally.Unanswered;
                    feel.MeanReactionLatencyTicks = tally.Events > 0
                        ? (int)(tally.LatencySumTicks / tally.Events)
                        : -1;
                }

                // ---- actions per minute ----
                feel.ActionsPerMinute = finalTick > 0
                    ? (int)((long)last.Slots[slot].IntentsSubmitted * 600 / finalTick)
                    : 0;

                all.Add(feel);
            }

            return all;
        }
    }

    /// <summary>
    /// One slot's running reaction bookkeeping, filled per tick by
    /// <see cref="TraceCollector"/>. A plain struct-like carrier: the
    /// arithmetic that turns it into a mean lives in <see cref="FeelMetrics"/>,
    /// so the per-tick path stays as cheap as it has to be.
    /// </summary>
    public sealed class ReactionTally
    {
        /// <summary>Damage events that were answered with a new movement order.</summary>
        public int Events;

        /// <summary>Summed ticks between damage and answer, over <see cref="Events"/>.</summary>
        public long LatencySumTicks;

        /// <summary>Damage events that never got an answer (the unit died or the match ended first).</summary>
        public int Unanswered;
    }
}
