using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class CompVisitorCompanionship : ThingComp
    {
        private const int DesireCheckDelay = 600;
        private bool hasEvaluatedDesire = false;
        private bool wantsService = false;
        private bool isWaiting = false;
        private int ticksOnMap = 0;

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!(parent is Pawn pawn)) return;
            if (!pawn.Spawned) return;
            if (pawn.Faction == null || pawn.Faction.IsPlayer) return;

            ticksOnMap += 250;

            if (!hasEvaluatedDesire && ticksOnMap >= DesireCheckDelay)
                EvaluateDesire(pawn);

            if (hasEvaluatedDesire && wantsService && !isWaiting)
                TryAssignGoToSpot(pawn);
        }

        private void EvaluateDesire(Pawn pawn)
        {
            hasEvaluatedDesire = true;
            wantsService = true; // Debug: 100%
            Log.Message($"[Companionship] Visitor {pawn.NameShortColored} desires companionship.");
        }

        private void TryAssignGoToSpot(Pawn pawn)
        {
            if (pawn.CurJob != null && pawn.CurJob.def == CompanionshipDefOf.GoToCompanionSpot)
                return;

            Thing spot = CompSpotUtility.GetClosestSpot(pawn);
            if (spot == null) return;

            var job = JobMaker.MakeJob(CompanionshipDefOf.GoToCompanionSpot, spot);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Log.Message($"[Companionship] Visitor {pawn.NameShortColored} walking to companion spot.");
        }

        public void Notify_ArrivedAtSpot(Pawn pawn)
        {
            isWaiting = true;

            Thing spot = CompSpotUtility.GetClosestSpot(pawn);
            if (spot == null) return;

            var waitJob = JobMaker.MakeJob(CompanionshipDefOf.WaitForCompanionDate, spot);
            pawn.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);

            Log.Message($"[Companionship] Visitor {pawn.NameShortColored} arrived and is now WAITING.");
        }

        public bool IsWaiting => isWaiting;

        public void Notify_ServiceReceived()
        {
            // Placeholder for future expansion.
        }
    }
}
