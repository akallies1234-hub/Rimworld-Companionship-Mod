using RimWorld;
using Verse;
using Verse.AI;
using System;
using UnityEngine;

namespace Riot.Companionship
{
    public class CompVisitorCompanionship : ThingComp
    {
        private const int DesireCheckDelay = 600; // 10 seconds after entering map
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

            // STEP 1 — Perform desire roll once
            if (!hasEvaluatedDesire && ticksOnMap >= DesireCheckDelay)
            {
                EvaluateDesire(pawn);
            }

            // STEP 2 — If they want service and haven't moved yet, push GoToCompanionSpot
            if (hasEvaluatedDesire && wantsService && !isWaiting)
            {
                TryAssignGoToSpot(pawn);
            }
        }

        private void EvaluateDesire(Pawn pawn)
        {
            hasEvaluatedDesire = true;

            // TEMP: Always 100% for debugging; later reintroduce 25-40% chance
            float chance = 1.0f;

            if (Rand.Value <= chance)
            {
                wantsService = true;
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} from {pawn.Faction.Name} desire roll: WANTS companionship (chance={chance * 100:F0} %).");
            }
            else
            {
                wantsService = false;
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} does not want companionship.");
            }
        }

        private void TryAssignGoToSpot(Pawn pawn)
        {
            // Already waiting or already have job? Skip.
            if (isWaiting) return;
            if (pawn.CurJob != null && pawn.CurJob.def == CompanionJobDefOf.GoToCompanionSpot) return;

            Thing spot = CompanionSpotUtility.GetClosestSpot(pawn);
            if (spot == null) return;

            Job job = JobMaker.MakeJob(CompanionJobDefOf.GoToCompanionSpot, spot);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Log.Message($"[Companionship] Visitor {pawn.NameShortColored} is heading to Companion Spot {spot.Label}.");
        }

        // Called by JobDriver_GoToCompanionSpot when arrival is complete
        public void Notify_ArrivedAtSpot(Pawn pawn)
        {
            isWaiting = true;

            // Queue the actual wait job automatically
            Thing spot = CompanionSpotUtility.GetClosestSpot(pawn);
            if (spot != null && pawn.Spawned)
            {
                Job waitJob = JobMaker.MakeJob(CompanionJobDefOf.WaitForCompanionDate, spot);
                pawn.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);
                Log.Message($"[Companionship] Visitor {pawn.NameShortColored} has arrived and is now WAITING for a companion.");
            }
        }

        public bool IsWaiting => isWaiting;
    }
}
