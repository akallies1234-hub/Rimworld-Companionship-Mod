using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_VisitorFollowCompanionToBed : JobDriver
    {
        private Pawn Companion => job.targetA.Pawn;
        private Building_Bed Bed => job.targetB.Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            Toil gotoSlot = ToilMaker.MakeToil("Companionship_VisitorGoToBedSlot");
            gotoSlot.initAction = () =>
            {
                if (Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 slot = GetSlotCellSafe(Bed, 1);
                pawn.pather?.StartPath(slot, PathEndMode.OnCell);
            };
            gotoSlot.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return gotoSlot;

            Toil wait = ToilMaker.MakeToil("Companionship_VisitorWaitAtBed");
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

        private static IntVec3 GetSlotCellSafe(Building_Bed bed, int slotIndex)
        {
            try
            {
                int slots = bed.SleepingSlotsCount;
                if (slots <= 0) return bed.Position;
                if (slotIndex < 0) slotIndex = 0;
                if (slotIndex >= slots) slotIndex = slots - 1;

                return bed.GetSleepingSlotPos(slotIndex);
            }
            catch
            {
                return bed.Position;
            }
        }
    }
}
