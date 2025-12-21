using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_WaitAtCompanionSpot : JobDriver
    {
        private Thing Spot => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Spot, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // Go to the spot (Touch = adjacent, since the spot occupies its cell).
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Wait (tuned)
            Toil wait = Toils_General.Wait(CompanionshipTuning.CompanionIdleAtSpotDurationTicks, TargetIndex.A);
            wait.handlingFacing = true;
            wait.socialMode = RandomSocialMode.Off;

            yield return wait;
        }
    }
}
