using System;
using System.Collections.Generic;
using Nova.AI.Data;

namespace Nova.AiLab
{
    /// <summary>
    /// Candidate profiles that exist only in the lab (plan section 4.6:
    /// "Abweichende Profile existieren zunächst nur im Labor").
    /// <para>
    /// These are the second axis of a comparison — and since the first sweep
    /// proved the SEED axis empty (no simulation system draws from the kernel
    /// PRNG), they are currently the ONLY axis with variance in it. A report
    /// over n seeds is one observation; a report over n profiles is n.
    /// </para>
    /// <para>
    /// Every candidate is a deliberate, one-sentence-explainable deviation from
    /// the shipped profile. Rastering all eight values would produce thousands
    /// of runs and no insight: the lab does not rank
    /// (<see cref="ComparisonReport"/>), a human picks, and a human cannot pick
    /// from a thousand rows.
    /// </para>
    /// </summary>
    public static class LabProfiles
    {
        /// <summary>The shipped profile, unchanged — the fixed yardstick every comparison runs against.</summary>
        public static AiProfile Reference => AiProfiles.Ms1Canonical;

        /// <summary>
        /// The named candidates. Ordered, not a dictionary literal iterated by
        /// hash: a report whose row order depends on hashing is a report two
        /// runs disagree about.
        /// </summary>
        public static IReadOnlyList<AiProfile> Candidates { get; } = new[]
        {
            Reference,

            // Attacks earlier with a smaller army. The question it answers:
            // does the shipped threshold of 6 leave the AI standing around?
            Derive("early-push", attackSquadThreshold: 3, targetArmySize: 10),

            // Waits for a bigger army before marching.
            Derive("late-push", attackSquadThreshold: 12, targetArmySize: 20),

            // More harvesters, later army — the economy question.
            Derive("greedy-economy", targetHarvesters: 4, targetArmySize: 16, attackSquadThreshold: 8),

            // Keeps a power reserve instead of building reactively. The shipped
            // value is 0, which means "react when the margin would go negative".
            Derive("power-buffer", powerReserve: 30),

            // Decides twice as often. Costs decision ticks, reacts sooner —
            // and this value was UNREACHABLE before E6, because it was a const.
            Derive("fast-cadence", decisionTickInterval: 10),

            // ---- waves ----
            //
            // Everything above differs from the reference in numbers the
            // shipped behaviour already reads. These differ in a value that
            // switches a CODE PATH on or off, and that is the point (finding
            // M001): the same binary plays with waves against without, in one
            // run, one-sided.
            //
            // THE OFF SETTING IS A CANDIDATE, not a footnote. Since the
            // shipped profile carries waveSize 12, `wave-off` is the only way
            // left to measure the rule against its own absence — and a
            // behaviour that can no longer be switched off can no longer be
            // judged.
            Derive("wave-off", waveSize: 1),

            // Two sizes below the shipped one. Measured one-sided over 4, 6,
            // 8, 10 and 12, every column improved monotonically with the size
            // — these two keep the trend visible without carrying five rows
            // that say the same thing. 12 is not a candidate: it IS the
            // reference now.
            Derive("wave-6", waveSize: 6),
            Derive("wave-10", waveSize: 10),

            // The staging point moved FORWARD, to two thirds of the way to the
            // enemy start area (the canonical map seats the two bases 112
            // cells apart). Measured worse than gathering at home, which was
            // the opposite of the expectation: units that gather far out have
            // already made the dangerous part of the walk alone.
            Derive("wave-6-far", waveSize: 6, stagingDistanceCells: 70, stagingToleranceCells: 6),

            // ---- retreat ----
            //
            // A wounded unit walks home instead of dying where it stands.
            // There is no health hysteresis and there cannot be one: MS-1
            // units never heal (Repair validates its target as a completed
            // BUILDING), so an exit percentage would never be reached. Three
            // entry thresholds, one danger radius apart, so the shape of the
            // trade-off is visible instead of a single point.
            // The off setting stays reachable, same reason as `wave-off`.
            Derive("retreat-off", retreatHealthPercent: 0),

            // Below and above the shipped 60. Measured one-sided over 25, 40,
            // 60, 75 and 90: the exchange ratio rises to 75 and turns down at
            // 90, but 75 pays for it with a match twice as long and twice the
            // own losses. These two keep both sides of the turn visible.
            Derive("retreat-40", retreatHealthPercent: 40),
            Derive("retreat-75", retreatHealthPercent: 75),

            // Same threshold, half the danger radius — measured worse (128
            // against 138 as Alliance), which says the radius is not where the
            // effect comes from.
            Derive("retreat-25-near", retreatHealthPercent: 25, retreatDangerCells: 4),
        };

        public static bool TryGet(string profileId, out AiProfile profile)
        {
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (!string.Equals(Candidates[i].ProfileId, profileId, StringComparison.Ordinal)) continue;
                profile = Candidates[i];
                return true;
            }
            profile = default;
            return false;
        }

        public static string KnownIds()
        {
            var ids = new List<string>(Candidates.Count);
            for (int i = 0; i < Candidates.Count; i++) ids.Add(Candidates[i].ProfileId);
            return string.Join(", ", ids);
        }

        /// <summary>
        /// A candidate is the shipped profile with named values replaced —
        /// so a candidate differs from the reference in exactly the ways its
        /// definition names, and in no other way that could creep in later.
        /// </summary>
        private static AiProfile Derive(
            string profileId,
            ushort? decisionTickInterval = null,
            int? placementSearchRadius = null,
            int? powerReserve = null,
            int? targetHarvesters = null,
            int? harvesterQueueBatch = null,
            int? targetArmySize = null,
            int? attackSquadThreshold = null,
            int? infantryQueueBatch = null,
            int? targetDamageWeight = null,
            int? targetThreatWeight = null,
            int? targetFinishWeight = null,
            int? targetDistanceWeight = null,
            int? waveSize = null,
            int? stagingDistanceCells = null,
            int? stagingToleranceCells = null,
            int? retreatHealthPercent = null,
            int? retreatDangerCells = null)
        {
            AiProfile b = Reference;
            return new AiProfile(
                profileId: profileId,
                decisionTickInterval: decisionTickInterval ?? b.DecisionTickInterval,
                placementSearchRadius: placementSearchRadius ?? b.PlacementSearchRadius,
                powerReserve: powerReserve ?? b.PowerReserve,
                targetHarvesters: targetHarvesters ?? b.TargetHarvesters,
                harvesterQueueBatch: harvesterQueueBatch ?? b.HarvesterQueueBatch,
                targetArmySize: targetArmySize ?? b.TargetArmySize,
                attackSquadThreshold: attackSquadThreshold ?? b.AttackSquadThreshold,
                infantryQueueBatch: infantryQueueBatch ?? b.InfantryQueueBatch,
                targetDamageWeight: targetDamageWeight ?? b.TargetDamageWeight,
                targetThreatWeight: targetThreatWeight ?? b.TargetThreatWeight,
                targetFinishWeight: targetFinishWeight ?? b.TargetFinishWeight,
                targetDistanceWeight: targetDistanceWeight ?? b.TargetDistanceWeight,
                waveSize: waveSize ?? b.WaveSize,
                stagingDistanceCells: stagingDistanceCells ?? b.StagingDistanceCells,
                stagingToleranceCells: stagingToleranceCells ?? b.StagingToleranceCells,
                retreatHealthPercent: retreatHealthPercent ?? b.RetreatHealthPercent,
                retreatDangerCells: retreatDangerCells ?? b.RetreatDangerCells);
        }

        /// <summary>Which values a candidate changed against the reference, for the report.</summary>
        public static List<string> DifferencesFromReference(AiProfile candidate)
        {
            AiProfile r = Reference;
            var diffs = new List<string>();
            if (candidate.DecisionTickInterval != r.DecisionTickInterval)
                diffs.Add($"cadence {r.DecisionTickInterval}→{candidate.DecisionTickInterval}");
            if (candidate.PlacementSearchRadius != r.PlacementSearchRadius)
                diffs.Add($"placementRadius {r.PlacementSearchRadius}→{candidate.PlacementSearchRadius}");
            if (candidate.PowerReserve != r.PowerReserve)
                diffs.Add($"powerReserve {r.PowerReserve}→{candidate.PowerReserve}");
            if (candidate.TargetHarvesters != r.TargetHarvesters)
                diffs.Add($"harvesters {r.TargetHarvesters}→{candidate.TargetHarvesters}");
            if (candidate.HarvesterQueueBatch != r.HarvesterQueueBatch)
                diffs.Add($"harvesterBatch {r.HarvesterQueueBatch}→{candidate.HarvesterQueueBatch}");
            if (candidate.TargetArmySize != r.TargetArmySize)
                diffs.Add($"armySize {r.TargetArmySize}→{candidate.TargetArmySize}");
            if (candidate.AttackSquadThreshold != r.AttackSquadThreshold)
                diffs.Add($"squadThreshold {r.AttackSquadThreshold}→{candidate.AttackSquadThreshold}");
            if (candidate.InfantryQueueBatch != r.InfantryQueueBatch)
                diffs.Add($"infantryBatch {r.InfantryQueueBatch}→{candidate.InfantryQueueBatch}");
            // Ohne diese vier meldete der Bericht "geaendert: —" fuer einen
            // Kandidaten, der sich sehr wohl unterscheidet — eine stille
            // Luecke waere schlimmer als eine fehlende Zeile.
            if (candidate.TargetDamageWeight != r.TargetDamageWeight)
                diffs.Add($"targetDmg {r.TargetDamageWeight}→{candidate.TargetDamageWeight}");
            if (candidate.TargetThreatWeight != r.TargetThreatWeight)
                diffs.Add($"targetThreat {r.TargetThreatWeight}→{candidate.TargetThreatWeight}");
            if (candidate.TargetFinishWeight != r.TargetFinishWeight)
                diffs.Add($"targetFinish {r.TargetFinishWeight}→{candidate.TargetFinishWeight}");
            if (candidate.TargetDistanceWeight != r.TargetDistanceWeight)
                diffs.Add($"targetDist {r.TargetDistanceWeight}→{candidate.TargetDistanceWeight}");
            if (candidate.WaveSize != r.WaveSize)
                diffs.Add($"waveSize {r.WaveSize}→{candidate.WaveSize}");
            if (candidate.StagingDistanceCells != r.StagingDistanceCells)
                diffs.Add($"staging {r.StagingDistanceCells}→{candidate.StagingDistanceCells}");
            if (candidate.StagingToleranceCells != r.StagingToleranceCells)
                diffs.Add($"stagingTol {r.StagingToleranceCells}→{candidate.StagingToleranceCells}");
            if (candidate.RetreatHealthPercent != r.RetreatHealthPercent)
                diffs.Add($"retreatAt {r.RetreatHealthPercent}→{candidate.RetreatHealthPercent}%");
            if (candidate.RetreatDangerCells != r.RetreatDangerCells)
                diffs.Add($"retreatDanger {r.RetreatDangerCells}→{candidate.RetreatDangerCells}");
            return diffs;
        }
    }
}
