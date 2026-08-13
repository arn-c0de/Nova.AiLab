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

            // ---- the army cap, alone ----
            //
            // targetArmySize does TWO jobs at once, and at 12 both of them go
            // wrong together. It caps "alive + queued" in the production step,
            // where it counts the units that are OUT FIGHTING — so the barracks
            // is idle for exactly as long as a wave is away, and only a death
            // restarts it. And it is the ceiling the r5 wave threshold derives
            // from (`reachable = targetArmySize - committed`), so with a full
            // wave out that threshold collapses to its floor of 1 and every
            // single replacement marches off alone.
            //
            // These two change NOTHING else — waveSize stays 12 (and
            // EffectiveWaveSize clamps it to the cap, not the other way round),
            // so what moves is only the room the AI has to build the next wave
            // while the current one fights.
            // Fünf Stellungen, weil ein mittlerer Wert nicht automatisch ein
            // Kompromiss ist — `wave-6` lag unter `wave-off`, und dieselbe
            // Warnung gilt hier, bis die Kurve dagegen spricht.
            Derive("army-16", targetArmySize: 16),
            Derive("army-18", targetArmySize: 18),
            Derive("army-20", targetArmySize: 20),
            Derive("army-24", targetArmySize: 24),
            Derive("army-36", targetArmySize: 36),

            // ---- das Wellentor in Kampfpunkten (r6) ----
            //
            // Die Referenz traegt seit r6 waveStrengthPoints 1200. `strength-off`
            // ist deshalb die Aus-Stellung und damit die einzige Art, die Regel
            // gegen ihre eigene Abwesenheit zu messen — dieselbe Rolle, die
            // `wave-off` und `retreat-off` weiter oben spielen (M001).
            Derive("strength-off", waveStrengthPoints: 0),

            // ---- die Basisverteidigung (r8) ----
            //
            // Die Referenz traegt seit r8 defendHomeCells 10, also ist
            // `defend-off` die Aus-Stellung und die einzige Art, die Regel
            // gegen ihre eigene Abwesenheit zu messen (M001). Dieselbe Rolle
            // wie `wave-off`, `retreat-off` und `strength-off`.
            //
            // Die zwei Radien daneben sind die Messfrage, nicht die Regel:
            // 8 ist der Rueckzugsradius, 16 der Sammelring. Wer sie
            // ueberschreitet, ruft Verteidiger zu einem Scharmuetzel am
            // Sammelpunkt — und genau das waere V002 unter neuem Namen.
            Derive("defend-off", defendHomeCells: 0),
            Derive("defend-8", defendHomeCells: 8),
            Derive("defend-16", defendHomeCells: 16),

            // DIE EIGENTLICHE FRAGE, und sie braucht ZWEI Zeilen, nicht eine.
            // Bei Obergrenze 12 bindet die Erreichbarkeitsdecke zuerst (zwoelf
            // Allianz-Schuetzen SIND 1200 Punkte, und eine dreizehnte Einheit
            // laesst die Kappe nicht zu), also entscheidet das Tor dort
            // identisch zum Kopfzaehlen. Sichtbar wird es erst mit angehobener
            // Kappe — und dann ist die Frage nicht "Kappe hoeher, ja oder
            // nein", sondern "aendert das Tor etwas an einer hoeheren Kappe".
            // Deshalb je Kappe ein Paar: mit Tor (erbt 1200) gegen ohne.
            Derive("army-24-count", targetArmySize: 24, waveStrengthPoints: 0),
            Derive("army-36-count", targetArmySize: 36, waveStrengthPoints: 0),

            // Die Klippe aus der Messreihe lag zwischen 18 und 20, und die
            // Erklaerung war das an die Kappe gekoppelte Wellentor: bei Kappe
            // 16 und zwoelf draussen marschieren Vierergruppen. Genau dort
            // muss das Punkttor den Unterschied machen, wenn die Erklaerung
            // stimmt — sonst ist sie falsch und gehoert widerrufen.
            Derive("army-16-count", targetArmySize: 16, waveStrengthPoints: 0),

            // Das Gegenstueck zur vorgeschlagenen Stellung: dieselbe Kappe,
            // kein Tor. Ohne diese Zeile laesst sich "die Kappe allein schadet
            // der Legion" nicht bei 30 zeigen, sondern nur bei 16/24/36.
            Derive("army-30-count", targetArmySize: 30, waveStrengthPoints: 0),

            // Die Kappenkurve MIT Tor, dicht abgetastet. Drei Stuetzpunkte
            // reichten nicht und waren irrefuehrend: Kappe 20 gewinnt, 19 und
            // 21 verlieren beide. Aus so einer Kurve das Maximum zu nehmen ist
            // eine Einzelpartie treffen, keine Messung.
            //
            // Was traegt, ist eine Ableitung: 1200 Punkte sind 28 Legions-
            // Rekruten zu je 44, und die Punktklausel entscheidet nur, solange
            // noch ein Kopf frei ist — sie greift also erstmals bei Kappe 29.
            // Darunter faellt die Welle nicht auf Kopfzahl zurueck, sondern
            // degeneriert zu "sammle die gesamte Armeeobergrenze": genau die
            // Zermuerbungspartien bei 22, 24 und 28. Diese neun Stellungen
            // zeigen beide Seiten der Grenze. Zahlen: Journal V007.
            Derive("army-19", targetArmySize: 19),
            Derive("army-21", targetArmySize: 21),
            Derive("army-22", targetArmySize: 22),
            Derive("army-28", targetArmySize: 28),
            Derive("army-30", targetArmySize: 30),
            Derive("army-32", targetArmySize: 32),
            Derive("army-34", targetArmySize: 34),
            Derive("army-40", targetArmySize: 40),
            Derive("army-48", targetArmySize: 48),

            // ---- die Nachschub-Doktrin (r9) ----
            //
            // Die Referenz traegt reinforceMinStrengthPercent 0, also ist hier
            // ausnahmsweise die AUS-Stellung die Referenz und der Kandidat die
            // Regel — dieselbe einseitige Messung wie sonst, nur andersherum
            // (M001). Ein Prozentsatz ist ein Anteil der vollen Wellenschwelle:
            // bei 1200 Punkten sind 20/50/70 genau 240/600/840.
            //
            // Drei Stellungen und nicht eine, weil ein mittlerer Wert nicht
            // automatisch ein Kompromiss ist: `wave-6` lag unter beiden Raendern
            // (V006), und dieselbe Warnung gilt hier, bis die Kurve dagegen
            // spricht.
            //
            // DIE ERSTE ZAHL IST NICHT DIE SIEGQUOTE, sondern Intents je 1.000
            // Ticks: am Schwellwert kann eine Einheit zwischen "nachruecken" und
            // "sammeln" kippen, und das ist exakt der Fehlermodus, an dem
            // DefendBase gescheitert ist (V002, +23 % Intents ohne besseres
            // Spiel). Die zweite ist der Entscheidungstick — Fall (c) haelt den
            // Ring an einer Schwelle fest, die eine ueberlebende Restwelle
            // blockieren kann (V006 unter neuem Namen).
            Derive("reinforce-20", reinforceMinStrengthPercent: 20),
            Derive("reinforce-50", reinforceMinStrengthPercent: 50),
            Derive("reinforce-70", reinforceMinStrengthPercent: 70),
            Derive("reinforce-40", reinforceMinStrengthPercent: 40),
            Derive("reinforce-25", reinforceMinStrengthPercent: 25),
            Derive("reinforce-30", reinforceMinStrengthPercent: 30),
            Derive("reinforce-35", reinforceMinStrengthPercent: 35),
            Derive("reinforce-45", reinforceMinStrengthPercent: 45),
            Derive("reinforce-60", reinforceMinStrengthPercent: 60),
            Derive("reinforce-80", reinforceMinStrengthPercent: 80),
            Derive("reinforce-90", reinforceMinStrengthPercent: 90),
            Derive("reinforce-100", reinforceMinStrengthPercent: 100),

            // Die Gegenprobe zur Kappe: bei Obergrenze 12 bindet die
            // Erreichbarkeitsdecke ohnehin, das Nachruecken passiert also heute
            // schon bedingungslos. Sichtbar wird der Unterschied zwischen
            // "bedingungslos" und "bedingt" erst dort, wo die Decke NICHT
            // bindet — sonst misst man die Kappe und nennt es die Doktrin.
            // ---- das HQ-Gewicht statt des Kurzschlusses (r10) ----
            //
            // Die Referenz traegt targetHqWeight 0, und 0 IST der Kurzschluss:
            // das HQ wird genommen, sobald es gesehen wird. Jeder Wert darueber
            // laesst es mitspielen statt gewinnen. Die Skala ist die des Scores
            // — die anderen vier Gewichte multiplizieren Terme im niedrigen
            // dreistelligen Bereich, ein Gewicht von 100.000 ist also praktisch
            // wieder der Kurzschluss und steht als Gegenprobe dafuer da.
            //
            // DIE ERSTE ZAHL IST NICHT DIE SIEGQUOTE, sondern die Zahl
            // verschiedener Zielarten je Partie: vor r10 faktisch 1. Eine Regel,
            // die daran nichts aendert, hat nichts geaendert, egal was die
            // Verlustspalte sagt.
            //
            // Die Referenz traegt seit r10 targetHqWeight 100, also ist
            // `hq-short-circuit` die Aus-Stellung und die einzige Art, die Regel
            // gegen ihre eigene Abwesenheit zu messen (M001) — dieselbe Rolle
            // wie `wave-off`, `retreat-off`, `strength-off` und `defend-off`.
            // 100 selbst ist kein Kandidat mehr: es IST die Referenz.
            Derive("hq-short-circuit", targetHqWeight: 0),
            Derive("hq-weight-1", targetHqWeight: 1),
            Derive("hq-weight-25", targetHqWeight: 25),
            Derive("hq-weight-50", targetHqWeight: 50),
            Derive("hq-weight-75", targetHqWeight: 75),
            Derive("hq-weight-150", targetHqWeight: 150),
            Derive("hq-weight-200", targetHqWeight: 200),
            Derive("hq-weight-250", targetHqWeight: 250),
            Derive("hq-weight-500", targetHqWeight: 500),
            Derive("hq-weight-1000", targetHqWeight: 1000),
            Derive("hq-weight-2000", targetHqWeight: 2000),
            Derive("hq-weight-100000", targetHqWeight: 100000),

            // Die Gegenprobe auf einer zweiten echten Achse. Die Seed-Achse des
            // Labors ist leer, also ist ein einzelner guter Wert erst dann keine
            // Einzelpartie, wenn er eine ANDERE Variation ueberlebt. Jede Kappe
            // wird gegen dieselbe Kappe ohne Gewicht gemessen, sonst misst man
            // die Kappe und nennt es das Gewicht.
            // ---- r11: der Standoff-Ring ----
            //
            // Die Referenz traegt engagementStandoffPercent 0, und 0 IST die
            // Aus-Stellung: die Einheit laeuft an ihr Ziel heran und bleibt
            // dort stehen. Die Aus-Stellung braucht hier also keinen eigenen
            // Kandidaten, sie IST die Referenz — anders als bei `wave-off`,
            // `retreat-off` oder `defend-off`, wo die Regel eingeschaltet
            // ausgeliefert wird (M001 bleibt erfuellt, nur andersherum).
            //
            // ES LIEST DEN WERT NOCH NIEMAND. r11 ist bisher die Datenhaelfte:
            // das Feld steht im Profil und im Profilhash, die Bewegungshaelfte
            // ist nicht geschrieben. Bis sie es ist, messen diese Kandidaten
            // dasselbe wie die Referenz — die Liste haelt die Achse bereit,
            // sie beweist noch nichts.
            //
            // ES IST EIN ANTEIL DER EIGENEN REICHWEITE, keine Kachelzahl. Die
            // Achse misst deshalb nicht "wie weit", sondern "wie viel von dem,
            // was die Waffe kann" — 100 haelt exakt auf der Grenze, die
            // CombatSystem.IsInRange prueft, und der erste Schubs der
            // Trennsteuerung nimmt die Einheit wieder heraus. Ueber 100 steht
            // sie ausser Schussweite: als Kandidat drin, weil eine Achse, die
            // nur dort misst, wo es gut ausgeht, keine Achse ist.
            // DIE AUS-STELLUNG IST SEIT r12 EIN KANDIDAT UND KEINE REFERENZ
            // MEHR. Solange die Referenz auf 0 stand, war sie es; mit dem
            // Verhalten ist der ausgelieferte Wert 80, und eine Achse ohne
            // ihre Aus-Stellung kann die Regel nicht gegen ihre eigene
            // Abwesenheit messen (M001). Ohne diese Zeile misst die Reihe nur
            // noch, welcher Prozentsatz der beste ist, und nie, ob die Regel
            // ueberhaupt einen macht.
            Derive("standoff-off", engagementStandoffPercent: 0),
            Derive("standoff-25", engagementStandoffPercent: 25),
            Derive("standoff-40", engagementStandoffPercent: 40),
            Derive("standoff-55", engagementStandoffPercent: 55),
            Derive("standoff-65", engagementStandoffPercent: 65),
            Derive("standoff-70", engagementStandoffPercent: 70),
            Derive("standoff-90", engagementStandoffPercent: 90),
            Derive("standoff-100", engagementStandoffPercent: 100),
            Derive("standoff-120", engagementStandoffPercent: 120),

            // Gegenprobe auf der Armeekappe, aus demselben Grund wie beim
            // HQ-Gewicht: ein einzelner guter Wert ist erst dann keine
            // Einzelpartie, wenn er eine ANDERE Variation ueberlebt. Jede Kappe
            // gegen dieselbe Kappe ohne Standoff, sonst misst man die Kappe.
            // DIE SEITE, DIE HIER STEHT, HAT SICH MIT r12 GEDREHT. Solange die
            // Referenz auf 0 stand, war `army-N` selbst die Aus-Seite und nur
            // die AN-Seite brauchte einen Namen. Jetzt steht die Referenz auf
            // 80, also IST `army-N-hq-100` zugleich `army-N-standoff-80` — ein
            // zweiter Name auf dasselbe Profil misst nichts doppelt, er macht
            // die Liste laenger und eine Zeile zur scheinbaren Bestaetigung der
            // anderen. Benannt wird deshalb die AUS-Seite.
            Derive("army-16-standoff-off", targetArmySize: 16, engagementStandoffPercent: 0),
            Derive("army-20-standoff-off", targetArmySize: 20, engagementStandoffPercent: 0),
            Derive("army-30-standoff-off", targetArmySize: 30, engagementStandoffPercent: 0),
            Derive("army-16-hq-100", targetArmySize: 16, targetHqWeight: 100),
            Derive("army-20-hq-100", targetArmySize: 20, targetHqWeight: 100),
            Derive("army-30-hq-100", targetArmySize: 30, targetHqWeight: 100),

            Derive("army-30-reinforce-30", targetArmySize: 30, reinforceMinStrengthPercent: 30),
            Derive("army-30-reinforce-40", targetArmySize: 30, reinforceMinStrengthPercent: 40),
            Derive("army-30-reinforce-50", targetArmySize: 30, reinforceMinStrengthPercent: 50),
            Derive("army-30-reinforce-60", targetArmySize: 30, reinforceMinStrengthPercent: 60),
            Derive("army-30-reinforce-70", targetArmySize: 30, reinforceMinStrengthPercent: 70),
            Derive("army-30-reinforce-80", targetArmySize: 30, reinforceMinStrengthPercent: 80),
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
            int? retreatDangerCells = null,
            int? waveStrengthPoints = null,
            int? defendHomeCells = null,
            int? reinforceMinStrengthPercent = null,
            int? targetHqWeight = null,
            int? engagementStandoffPercent = null)
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
                retreatDangerCells: retreatDangerCells ?? b.RetreatDangerCells,
                waveStrengthPoints: waveStrengthPoints ?? b.WaveStrengthPoints,
                defendHomeCells: defendHomeCells ?? b.DefendHomeCells,
                reinforceMinStrengthPercent:
                    reinforceMinStrengthPercent ?? b.ReinforceMinStrengthPercent,
                targetHqWeight: targetHqWeight ?? b.TargetHqWeight,
                engagementStandoffPercent:
                    engagementStandoffPercent ?? b.EngagementStandoffPercent);
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
            if (candidate.WaveStrengthPoints != r.WaveStrengthPoints)
                diffs.Add($"waveStrength {r.WaveStrengthPoints}→{candidate.WaveStrengthPoints}");
            if (candidate.DefendHomeCells != r.DefendHomeCells)
                diffs.Add($"defendHome {r.DefendHomeCells}→{candidate.DefendHomeCells}");
            if (candidate.TargetHqWeight != r.TargetHqWeight)
                diffs.Add($"hqWeight {r.TargetHqWeight}→{candidate.TargetHqWeight}");
            if (candidate.ReinforceMinStrengthPercent != r.ReinforceMinStrengthPercent)
                diffs.Add(
                    $"reinforceAt {r.ReinforceMinStrengthPercent}→{candidate.ReinforceMinStrengthPercent}%");
            if (candidate.EngagementStandoffPercent != r.EngagementStandoffPercent)
                diffs.Add(
                    $"standoff {r.EngagementStandoffPercent}→{candidate.EngagementStandoffPercent}%");
            return diffs;
        }
    }
}
