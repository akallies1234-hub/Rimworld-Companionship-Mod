using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    /// <summary>
    /// Visitor job: go to a Companion Spot and wait there for a Companion.
    /// 
    /// Job target A: the Companion Spot (Building_CompanionSpot).
    /// 
    /// While this job is active, CompVisitorCompanionship.IsWaitingForCompanion is true,
    /// which prevents us from re-queueing the wait job over and over.
    /// </summary>
    public class JobDriver_WaitForCompanionDate : JobDriver
    {
        private Building_CompanionSpot Spot => TargetA.Thing as Building_CompanionSpot;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // We don't need to reserve the spot (it's like a party spot).
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // 1) Go stand on (or adjacent to) the Companion Spot.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2) Wait there until a Companion comes to start the date.
            Toil wait = new Toil();

            wait.initAction = delegate
            {
                Pawn pawn = this.pawn;
                if (pawn == null)
                    return;

                CompVisitorCompanionship comp = pawn.TryGetComp<CompVisitorCompanionship>();
                if (comp != null)
                {
                    comp.IsWaitingForCompanion = true;
                }
            };

            wait.AddFinishAction(delegate
            {
                Pawn pawn = this.pawn;
                if (pawn == null)
                    return;

                CompVisitorCompanionship comp = pawn.TryGetComp<CompVisitorCompanionship>();
                if (comp != null)
                {
                    comp.ResetWaitingState();
                }
            });

            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.socialMode = RandomSocialMode.SuperActive;
            wait.handlingFacing = true;

            yield return wait;
        }
    }
}
