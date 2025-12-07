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
        private const float BaseInitialDesireChance = 0.35f;           // ~35% of visitors want companionship
        private const float BaseAdditionalServiceChance = 0.25f;       // ~25% chance they want a second date

        private bool hasEvaluatedInitialDesire;
        private bool desiresCompanionship;
        private bool isWaitingForCompanion;

        private bool hasReceivedService;
        private bool hasRolledForAdditionalService;
        private bool wantsAdditionalService;

        private Pawn Pawn => parent as Pawn;

        /// <summary>
        /// Has this visitor decided whether they want a companion at all this visit?
        /// </summary>
        public bool HasEvaluatedInitialDesire => hasEvaluatedInitialDesire;

        /// <summary>
        /// Whether this visitor wants at least one companion date this visit.
        /// </summary>
        public bool DesiresCompanionship => desiresCompanionship;

        /// <summary>
        /// Whether this visitor is currently considered "in queue" / waiting for a companion.
        /// This is set while they are on the WaitForCompanionDate job.
        /// </summary>
        public bool IsWaitingForCompanion
        {
            get => isWaitingForCompanion;
            set => isWaitingForCompanion = value;
        }

        /// <summary>
        /// Whether this visitor has already received at least one service.
        /// </summary>
        public bool HasReceivedService => hasReceivedService;

        /// <summary>
        /// Whether they want additional services after the first one.
        /// This is decided once, after their first completed date.
        /// </summary>
        public bool WantsAdditionalService => wantsAdditionalService;

        public override void CompTickRare()
        {
            base.CompTickRare();

            EvaluateInitialDesireIfNeeded();
            TryHandleWaitingBehavior();
        }

        /// <summary>
        /// Decide once per visit whether this visitor wants companionship at all.
        /// For now this is pure RNG, but future versions will add modifiers.
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
            // TODO: Future: adjust by traits, needs, faction, storyteller, etc.
            desiresCompanionship = Rand.Value < chance;
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
                return;
            }

            // Only bother if there is at least one available Companion for this visitor.
            if (!CompanionshipUtility.HasAvailableCompanionFor(pawn))
            {
                return;
            }

            Job waitJob = JobMaker.MakeJob(CompanionshipDefOf.WaitForCompanionDate, spot);
            pawn.jobs.TryTakeOrderedJob(waitJob);
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
            // TODO: Future: adjust based on how good the date was, needs, traits, etc.
            wantsAdditionalService = Rand.Value < chance;
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
