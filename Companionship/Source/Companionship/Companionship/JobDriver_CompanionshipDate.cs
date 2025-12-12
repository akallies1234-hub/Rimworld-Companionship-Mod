using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_CompanionshipDate : JobDriver
    {
        // A = visitor
        // B = companion spot
        // C = companion bed
        private Pawn Visitor
        {
            get { return job.GetTarget(TargetIndex.A).Pawn; }
        }

        private Thing CompanionSpot
        {
            get { return job.GetTarget(TargetIndex.B).Thing; }
        }

        private Building_Bed CompanionBed
        {
            get { return job.GetTarget(TargetIndex.C).Thing as Building_Bed; }
        }

        private const int PaymentSilver = 50;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn visitor = Visitor;
            if (visitor != null)
            {
                if (!pawn.Reserve(visitor, job, 1, -1, null, errorOnFailed))
                    return false;
            }

            Building_Bed bed = CompanionBed;
            if (bed != null)
            {
                // reserve all sleeping slots (double bed = 2)
                if (!pawn.Reserve(bed, job, bed.SleepingSlotsCount, -1, null, errorOnFailed))
                    return false;
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.C);

            // ---- Step 1: Get to know you (1 in-game hour) ----
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil talk = Toils_General.Wait(GenDate.TicksPerHour, TargetIndex.A);
            talk.WithProgressBarToilDelay(TargetIndex.A);
            talk.handlingFacing = true;
            talk.socialMode = RandomSocialMode.SuperActive;

            talk.tickAction = delegate
            {
                Pawn v = Visitor;
                if (v == null) return;

                // occasional chitchat to make it feel alive
                if (pawn.IsHashIntervalTick(120))
                {
                    pawn.interactions.TryInteractWith(v, InteractionDefOf.Chitchat);
                }
            };

            yield return talk;

            // ---- Step 2: Move to bed, then trigger authentic vanilla lovin ----
            // If this toil isn't available in your references, swap for Toils_Goto.GotoThing(TargetIndex.C,...)
            yield return Toils_Bed.GotoBed(TargetIndex.C);

            Toil startLovin = new Toil();
            startLovin.initAction = delegate
            {
                Pawn v = Visitor;
                Building_Bed bed = CompanionBed;

                if (v == null || bed == null || bed.Map == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                int bedPhaseTicks = Rand.RangeInclusive(1, 3) * GenDate.TicksPerHour;
                int expireTick = Find.TickManager.TicksGame + bedPhaseTicks + 1200;

                GameComponent_Companionship gc = GameComponent_Companionship.GetOrCreate();
                if (gc != null)
                {
                    gc.AllowLovin(pawn, v, bed.Position, bed.Map.uniqueID, PaymentSilver, expireTick);
                }

                // Force visitor to lie down at the bed for the duration window
                Job layDown = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                layDown.expiryInterval = bedPhaseTicks;
                layDown.checkOverrideOnExpire = true;
                layDown.playerForced = true;

                v.jobs.StartJob(layDown, JobCondition.InterruptForced, null, true, true);

                // Queue the authentic vanilla lovin job for the companion pawn.
                // Our Harmony patch temporarily makes LovePartnerRelationExists return true for this pair.
                Job lovinJob = JobMaker.MakeJob(JobDefOf.Lovin, v, bed);
                lovinJob.playerForced = true;

                pawn.jobs.jobQueue.EnqueueFirst(lovinJob);

                // End this driver; the queued lovin job takes over.
                EndJobWith(JobCondition.Succeeded);
            };

            startLovin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return startLovin;
        }
    }
}
