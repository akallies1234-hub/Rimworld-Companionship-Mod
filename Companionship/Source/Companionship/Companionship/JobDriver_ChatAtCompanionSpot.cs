using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor job: go to the companion spot and wait for a set duration (job.count ticks).
    /// Used for the "get to know you" phase so they stop wandering and actually participate.
    /// </summary>
    public class JobDriver_ChatAtCompanionSpot : JobDriver
    {
        private const TargetIndex SpotInd = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservations on the spot.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(SpotInd);

            yield return Toils_Goto.GotoThing(SpotInd, PathEndMode.Touch);

            int duration = job.count > 0 ? job.count : 2500;

            Toil wait = Toils_General.Wait(duration);
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.handlingFacing = true;
            yield return wait;
        }
    }
}
