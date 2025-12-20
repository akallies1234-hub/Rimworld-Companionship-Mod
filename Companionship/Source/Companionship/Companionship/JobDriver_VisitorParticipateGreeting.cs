using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_VisitorParticipateGreeting : JobDriver
    {
        private Pawn Companion => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            Toil wait = ToilMaker.MakeToil("Companionship_VisitorGreetingStandStill");
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = GenDate.TicksPerHour;

            wait.initAction = () =>
            {
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.Standing;
            };

            wait.tickAction = () =>
            {
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.Standing;

                if (Companion != null && Companion.Spawned)
                    pawn.rotationTracker.FaceTarget(Companion);
            };

            yield return wait;
        }
    }
}
