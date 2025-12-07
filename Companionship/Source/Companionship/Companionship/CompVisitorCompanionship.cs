using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class CompProperties_VisitorCompanionship : CompProperties
    {
        public CompProperties_VisitorCompanionship()
        {
            this.compClass = typeof(CompVisitorCompanionship);
        }
    }

    /// <summary>
    /// Tracks a visitor's desire for companionship over the course of their stay.
    /// Also handles moving the visitor to a Companion Spot to wait for a Companion
    /// when appropriate.
    /// </summary>
    public class CompVisitorCompanionship : ThingComp
    {
        // DEBUG: cranked to 100% so we can clearly see the behavior.
        // Once things are working and feel right, we can tune this back down.
        private const float BaseInitialDesireChance = 1.0f; // 100% of valid visitors want companionship (for testing)
        private const float BaseAdditionalServiceChance = 0.25f; // ~25% chance they want a second date

        private bool hasEvaluatedInitialDesire;
        private bool desiresCompanionship;
        private bool isWaitingForCompanion;
        private bool hasReceivedService;
        private bool hasRolledForAdditionalService;
        private bool wantsAdditionalService;

        private Pawn Pawn => parent as Pawn;

        public bool HasEvaluatedInitialDesire => hasEvaluatedInitialDesire;
        public bool DesiresCompanionship => desiresCompanionship;

        public bool IsWaitingForCompanion
        {
            get => isWaitingForCompanion;
            set => isWaitingForCompanion = value;
        }

        public bool HasReceivedService => hasReceivedService;
        public bool WantsAdditionalService => wantsAdditionalService;

        public override void CompTickRare()
        {
            base.CompTickRare();
            EvaluateInitialDesireIfNeeded();
            TryHandleWaitingBehavior();
        }

        /// <summary>
        /// Decide once per visit whether this visitor wants companionship at all.
        /// DEBUG: for now, this is basically always true for valid visitors.
        /// </summary>
        private void EvaluateInitialDesireIfNeeded()
        {
            if (hasEvaluatedInitialDesire)
            {
                return;
            }

            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            // Only visitors / non-player pawns; we don't want colonists using this logic.
            if (pawn.Faction == null || pawn.Faction == Faction.OfPlayerSilentFail)
            {
                return;
            }

            if (!pawn.RaceProps?.Humanlike ?? true)
            {
                return;
            }

            hasEvaluatedInitialDesire = true;

            float chance = BaseInitialDesireChance;
            desiresCompanionship = Rand.Value < chance;

            // DEBUG LOG
            Log.Message($"[Companionship] Visitor {pawn.LabelShort} from {pawn.Faction?.Name ?? "no faction"} desire roll: {(desiresCompanionship ? "WANTS" : "does NOT want")} companionship (chance={chance:P0}).");
        }

        /// <summary>
        /// Handle sending the visitor to a Companion Spot to wait, if they want companionship,
        /// a spot exists, and at least one available Companion is present.
        /// </summary>
        private void TryHandleWaitingBehavior()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            if (!desiresCompanionship)
            {
                return;
            }

            // Already flagged as waiting, nothing to do.
            if (isWaitingForCompanion)
            {
                return;
            }

            // If they've already received service and do NOT want more, never queue again.
            if (hasReceivedService && !wantsAdditionalService)
            {
                return;
            }

            // Don't override critical states.
            if (pawn.Drafted || pawn.InAggroMentalState)
            {
                return;
            }

            // If they're already on the waiting job, don't re-issue it.
            if (pawn.CurJobDef == CompanionshipDefOf.WaitForCompanionDate)
            {
                return;
            }

            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }

            // Find a Companion Spot.
            Building_CompanionSpot spot = CompanionshipUtility.FindNearestCompanionSpot(pawn);
            if (spot == null)
            {
                // DEBUG LOG
                Log.Message($"[Companionship] Visitor {pawn.LabelShort} WANTS companionship but there is NO Companion Spot on the map.");
                return;
            }

            // Only bother if there is at least one available Companion for this visitor.
            if (!CompanionshipUtility.HasAvailableCompanionFor(pawn))
            {
                // DEBUG LOG
                Log.Message($"[Companionship] Visitor {pawn.LabelShort} WANTS companionship but NO available Companion was found (work disabled, limits, or unreachable).");
                return;
            }

            Job waitJob = JobMaker.MakeJob(CompanionshipDefOf.WaitForCompanionDate, spot);
            waitJob.locomotionUrgency = LocomotionUrgency.Walk; // natural approach, no sprint
            pawn.jobs.TryTakeOrderedJob(waitJob);

            // DEBUG LOG
            Log.Message($"[Companionship] Visitor {pawn.LabelShort} is heading to Companion Spot {spot.LabelShort} to WAIT for a companion.");
        }

        /// <summary>
        /// Called when this visitor has completed a companion date.
        /// Marks that they have received service and, once, decides if they want more.
        /// </summary>
        public void Notify_ServiceReceived()
        {
            hasReceivedService = true;

            if (hasRolledForAdditionalService)
            {
                return;
            }

            hasRolledForAdditionalService = true;

            float chance = BaseAdditionalServiceChance;
            wantsAdditionalService = Rand.Value < chance;

            // DEBUG LOG
            Pawn pawn = Pawn;
            Log.Message($"[Companionship] Visitor {pawn?.LabelShort ?? "unknown"} completed a date and {(wantsAdditionalService ? "WANTS" : "does NOT want")} additional service (chance={chance:P0}).");
        }

        /// <summary>
        /// Clear "waiting" state when the visitor leaves the spot or finishes a date.
        /// </summary>
        public void ResetWaitingState()
        {
            isWaitingForCompanion = false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref hasEvaluatedInitialDesire, "compVisitor_hasEvaluatedInitialDesire", false);
            Scribe_Values.Look(ref desiresCompanionship, "compVisitor_desiresCompanionship", false);
            Scribe_Values.Look(ref isWaitingForCompanion, "compVisitor_isWaitingForCompanion", false);
            Scribe_Values.Look(ref hasReceivedService, "compVisitor_hasReceivedService", false);
            Scribe_Values.Look(ref hasRolledForAdditionalService, "compVisitor_hasRolledForAdditionalService", false);
            Scribe_Values.Look(ref wantsAdditionalService, "compVisitor_wantsAdditionalService", false);
        }
    }
}
