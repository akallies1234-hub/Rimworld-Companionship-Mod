using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor job: go to the companion bed and "use" it for a duration (job.count ticks).
    /// Prototype bed use: force a lying posture and occasionally throw heart flecks.
    /// </summary>
    public class JobDriver_JoinCompanionshipDate : JobDriver
    {
        private const TargetIndex BedInd = TargetIndex.A;

        private Building_Bed Bed
        {
            get { return job.GetTarget(BedInd).Thing as Building_Bed; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Colonist reserves; visitor doesn't reserve.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(BedInd);
            this.FailOn(() => Bed == null || Bed.Destroyed);

            yield return Toils_Goto.GotoThing(BedInd, PathEndMode.Touch);

            int duration = job.count > 0 ? job.count : 2500;

            Toil bedTime = new Toil();
            bedTime.defaultCompleteMode = ToilCompleteMode.Delay;
            bedTime.defaultDuration = duration;

            bedTime.tickAction = () =>
            {
                // Visually suggest "in bed"
                if (pawn.jobs != null)
                {
                    pawn.jobs.posture = PawnPosture.LayingInBed;
                }

                // Occasionally throw hearts
                if (pawn.IsHashIntervalTick(250) && pawn.Map != null)
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }
            };

            yield return bedTime;
        }
    }
}
