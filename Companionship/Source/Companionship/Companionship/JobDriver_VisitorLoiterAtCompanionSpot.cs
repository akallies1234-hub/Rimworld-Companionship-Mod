using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor goes to the Companion Spot, then wanders within 7 tiles indefinitely.
    /// </summary>
    public class JobDriver_VisitorLoiterAtCompanionSpot : JobDriver
    {
        private int nextMoveTick;

        private Thing Spot => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil wander = ToilMaker.MakeToil("Companionship_VisitorLoiterNearSpot");
            wander.defaultCompleteMode = ToilCompleteMode.Never;

            wander.initAction = () => nextMoveTick = Find.TickManager.TicksGame;

            wander.tickAction = () =>
            {
                if (Spot == null || !Spot.Spawned || pawn.Map == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                int now = Find.TickManager.TicksGame;

                if (pawn.pather != null && pawn.pather.Moving) return;
                if (now < nextMoveTick) return;

                IntVec3 root = Spot.Position;
                IntVec3 dest = CellFinder.RandomClosewalkCellNear(root, pawn.Map, CompanionshipVisitorTracker.WaitRadius);

                if (!pawn.CanReach(dest, PathEndMode.OnCell, Danger.Some))
                    dest = root;

                pawn.pather.StartPath(dest, PathEndMode.OnCell);
                nextMoveTick = now + Rand.RangeInclusive(180, 420);
            };

            yield return wander;
        }
    }
}
