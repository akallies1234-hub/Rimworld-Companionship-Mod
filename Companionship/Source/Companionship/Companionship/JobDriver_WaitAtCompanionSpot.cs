using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor job: go to the Companion Spot, then casually wander within a radius while waiting.
    /// Also ends itself automatically once the MapComponent clears the waiting state.
    /// </summary>
    public class JobDriver_WaitAtCompanionSpot : JobDriver
    {
        private const TargetIndex SpotInd = TargetIndex.A;

        private const int WanderRadius = 7;
        private const int MinMoveIntervalTicks = 120;  // ~2 seconds
        private const int MaxMoveIntervalTicks = 300;  // ~5 seconds

        private Thing SpotThing
        {
            get { return job.GetTarget(SpotInd).Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(SpotInd);

            yield return Toils_Goto.GotoThing(SpotInd, PathEndMode.Touch);

            Toil wander = new Toil();
            wander.defaultCompleteMode = ToilCompleteMode.Never;

            int nextMoveTick = 0;

            wander.initAction = () =>
            {
                nextMoveTick = Find.TickManager.TicksGame + Rand.RangeInclusive(MinMoveIntervalTicks, MaxMoveIntervalTicks);
            };

            wander.tickAction = () =>
            {
                var comp = pawn.Map != null ? pawn.Map.GetComponent<MapComponent_Companionship>() : null;
                if (comp == null || !comp.IsWaiting(pawn))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                Thing spot = SpotThing;
                if (spot == null || spot.Destroyed || !spot.Spawned)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!pawn.Position.InHorDistOf(spot.Position, WanderRadius))
                {
                    IntVec3 returnCell = CellFinder.RandomClosewalkCellNear(spot.Position, pawn.Map, WanderRadius);
                    pawn.pather.StartPath(returnCell, PathEndMode.OnCell);
                    nextMoveTick = Find.TickManager.TicksGame + Rand.RangeInclusive(MinMoveIntervalTicks, MaxMoveIntervalTicks);
                    return;
                }

                if (pawn.pather != null && pawn.pather.Moving)
                    return;

                int now = Find.TickManager.TicksGame;
                if (now >= nextMoveTick)
                {
                    IntVec3 dest = CellFinder.RandomClosewalkCellNear(spot.Position, pawn.Map, WanderRadius);
                    if (dest.IsValid)
                    {
                        pawn.pather.StartPath(dest, PathEndMode.OnCell);
                    }
                    nextMoveTick = now + Rand.RangeInclusive(MinMoveIntervalTicks, MaxMoveIntervalTicks);
                }
            };

            yield return wander;
        }
    }
}
