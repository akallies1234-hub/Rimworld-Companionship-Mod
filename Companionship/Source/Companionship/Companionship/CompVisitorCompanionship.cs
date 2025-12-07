using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class CompVisitorCompanionship : ThingComp
    {
        private const int DesireCheckDelay = 600; // 10 seconds
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

            // STEP 1: Do desire check once
            if (!hasEvaluatedDesire && ticksOnMap >= DesireCheckDelay)
            {
                EvaluateDesire(pawn);
            }

            // STEP 2: If they want service but are not waiting, assign GoToSpot
            if (hasEvaluatedDesire && wantsService && !isWaiting)
            {
                TryAssignGoToSpot(pawn);
            }
        }

        private void EvaluateDesire(Pawn pawn)
        {
            hasEvaluatedDesire = true;

            // Debug: Always yes for testing
            float chance = 1f;

            if (Rand.Value <= chance)
            {
                wantsService = true;
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} from {pawn.Faction.Name} desire roll: WANTS companionship (chance={chance * 100:F0} %).");
            }
            else
            {
                wantsService = false;
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} does NOT want companionship.");
            }
        }

        private void TryAssignGoToSpot(Pawn pawn)
        {
            if (isWaiting) return;

            if (pawn.CurJob != null &&
                pawn.CurJob.def == CompanionshipDefOf.GoToCompanionSpot)
                return;

            Thing spot = CompSpotUtility.GetClosestSpot(pawn);
            if (spot == null) return;

            Job job = JobMaker.MakeJob(CompanionshipDefOf.GoToCompanionSpot, spot);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Log.Message($"[Companionship] Visitor {pawn.NameShortColored} is heading to Companion Spot {spot.Label}.");
        }

        // Called by GoToSpot driver
        public void Notify_ArrivedAtSpot(Pawn pawn)
        {
            isWaiting = true;

            Thing spot = CompSpotUtility.GetClosestSpot(pawn);
            if (spot != null)
            {
                Job waitJob = JobMaker.MakeJob(CompanionshipDefOf.WaitForCompanionDate, spot);
                pawn.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} has ARRIVED and is now WAITING.");
            }
        }

        public bool IsWaiting => isWaiting;
    }
}
