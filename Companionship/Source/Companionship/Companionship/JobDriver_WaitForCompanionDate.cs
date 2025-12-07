using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class JobDriver_WaitForCompanionDate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);

            Toil wait = new Toil();
            wait.initAction = () =>
            {
                pawn.pather.StopDead();
            };
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(TargetA);
            };
            wait.socialMode = RandomSocialMode.Off;
            yield return wait;
        }
    }
}
