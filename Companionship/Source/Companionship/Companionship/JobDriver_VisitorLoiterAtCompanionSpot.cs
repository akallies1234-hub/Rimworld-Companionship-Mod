using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor goes to the Companion Spot, then wanders within CompanionshipTuning.WaitRadius indefinitely.
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

                IntVec3 dest = root;

                // Try several samples for a reachable closewalk cell
                int radius = CompanionshipTuning.WaitRadius;
                if (radius < 1) radius = 1;

                for (int i = 0; i < 12; i++)
                {
                    IntVec3 c = CellFinder.RandomClosewalkCellNear(root, pawn.Map, radius);
                    if (pawn.CanReach(c, PathEndMode.OnCell, Danger.Some))
                    {
                        dest = c;
                        break;
                    }
                }

                pawn.pather.StartPath(dest, PathEndMode.OnCell);

                int min = CompanionshipTuning.VisitorLoiterMoveIntervalMinTicks;
                int max = CompanionshipTuning.VisitorLoiterMoveIntervalMaxTicks;
                if (min < 1) min = 180;
                if (max < min) max = min;

                nextMoveTick = now + Rand.RangeInclusive(min, max);
            };

            yield return wander;
        }
    }
}
