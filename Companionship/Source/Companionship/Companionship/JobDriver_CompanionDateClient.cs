using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    /// <summary>
    /// Client-side job for participating in a Companion date.
    /// 
    /// This is the job run by the visitor (client). The Companion's
    /// JobDriver_CompanionDate is in charge; this job simply:
    /// 1) Moves the client to the Companion bed.
    /// 2) Waits there indefinitely with social mode active, until the
    ///    Companion ends the date and forcibly ends this job.
    /// </summary>
    public class JobDriver_CompanionDateClient : JobDriver
    {
        protected Pawn Companion
        {
            get { return job.GetTarget(TargetIndex.A).Thing as Pawn; }
        }

        protected Building_Bed Bed
        {
            get { return job.GetTarget(TargetIndex.B).Thing as Building_Bed; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // The Companion's job reserves the bed and client.
            // The client does not reserve anything; they just cooperate.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => Companion == null || Companion.Dead);
            this.FailOn(() => Bed == null || !Bed.Spawned);

            // 1) Go to the bed.
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell);

            // 2) Wait at the bed until the Companion ends the date.
            Toil wait = new Toil();
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.socialMode = RandomSocialMode.SuperActive;
            yield return wait;
        }
    }
}
