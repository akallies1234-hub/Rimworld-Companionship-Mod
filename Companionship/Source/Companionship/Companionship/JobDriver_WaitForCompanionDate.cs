using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    /// <summary>
    /// Visitor job: wait at the Companion Spot until a Companion comes.
    /// This job DOES NOT move the pawn; they must already be at the spot.
    /// </summary>
    public class JobDriver_WaitForCompanionDate : JobDriver
    {
        private TargetIndex SpotIndex = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservation needed for a Companion Spot
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(SpotIndex);

            // 1) Begin waiting
            Toil wait = new Toil
            {
                initAction = () =>
                {
                    Pawn pawn = this.pawn;

                    CompVisitorCompanionship comp = pawn.TryGetComp<CompVisitorCompanionship>();
                    if (comp != null)
                    {
                        comp.IsWaitingForCompanion = true;
                    }
                },

                // Never auto-complete — this job ends only when interrupted by a Companion.
                defaultCompleteMode = ToilCompleteMode.Never
            };

            // Add finish action to clear the waiting flag
            wait.AddFinishAction(() =>
            {
                Pawn pawn = this.pawn;

                CompVisitorCompanionship comp = pawn.TryGetComp<CompVisitorCompanionship>();
                if (comp != null)
                {
                    comp.IsWaitingForCompanion = false;
                }
            });

            // Social mode makes the pawn animate naturally instead of freezing
            wait.socialMode = RandomSocialMode.SuperActive;

            // Random facing makes the pawn look alive instead of rigid
            wait.handlingFacing = true;

            yield return wait;
        }
    }
}
