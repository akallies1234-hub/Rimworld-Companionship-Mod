using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    /// <summary>
    /// Visitor movement job: walk to a Companion Spot.
    /// This job does NOT mark the visitor as "waiting."
    /// When they arrive, CompVisitorCompanionship will assign the actual waiting job.
    /// </summary>
    public class JobDriver_GoToCompanionSpot : JobDriver
    {
        private TargetIndex SpotIndex = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservation needed for a spot
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(SpotIndex);

            // 1) Go to Companion Spot
            yield return Toils_Goto.GotoThing(SpotIndex, PathEndMode.Touch);

            // 2) Once we arrive, end this job.
            // The CompVisitorCompanionship comp will detect arrival and assign WaitForCompanionDate.
            Toil finish = new Toil
            {
                initAction = () =>
                {
                    // End this job cleanly
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            yield return finish;
        }
    }
}
