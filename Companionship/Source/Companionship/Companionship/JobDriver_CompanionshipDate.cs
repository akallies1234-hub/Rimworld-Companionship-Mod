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
        private Pawn Visitor => job.GetTarget(TargetIndex.A).Pawn;

        private Thing CompanionSpot => job.GetTarget(TargetIndex.B).Thing;

        private Building_Bed CompanionBed => job.GetTarget(TargetIndex.C).Thing as Building_Bed;

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
                int slots = bed.SleepingSlotsCount;
                if (slots < 1) slots = 1;

                // reserve all sleeping slots (double bed = 2)
                if (!pawn.Reserve(bed, job, slots, -1, null, errorOnFailed))
                    return false;
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.C);

            // Must still have a bed
            this.FailOn(() => CompanionBed == null);

            // ---- Step 1: Walk to visitor ----
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // ---- Step 2: Get to know you (1 in-game hour) ----
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

            // ---- Step 3: Go to the bed ----
            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);

            // ---- Step 4: Trigger vanilla lovin ----
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

                // Cooldowns to prevent job spam loops if anything interrupts
                CompanionshipDateUtility.SetDateCooldown(pawn, 1200); // 20s
                CompanionshipDateUtility.SetDateCooldown(v, 1200);    // 20s

                int bedPhaseTicks = Rand.RangeInclusive(1, 3) * GenDate.TicksPerHour;
                int expireTick = Find.TickManager.TicksGame + bedPhaseTicks + 1200;

                GameComponent_Companionship gc = GameComponent_Companionship.GetOrCreate();
                if (gc != null)
                {
                    gc.AllowLovin(pawn, v, bed.Position, bed.Map.uniqueID, PaymentSilver, expireTick);
                }

                // Force visitor to lie down for the window (helps ensure they’re in position)
                Job layDown = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                layDown.expiryInterval = bedPhaseTicks;
                layDown.checkOverrideOnExpire = true;
                layDown.playerForced = true;
                v.jobs.StartJob(layDown, JobCondition.InterruptForced, null, true, true);

                // Start the authentic vanilla lovin job for the companion pawn immediately.
                // Harmony patch should temporarily make LovePartnerRelationExists true for this pair.
                Job lovinJob = JobMaker.MakeJob(JobDefOf.Lovin, v, bed);
                lovinJob.playerForced = true;

                pawn.jobs.StartJob(lovinJob, JobCondition.InterruptForced, null, true, true);
            };

            startLovin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return startLovin;
        }
    }
}
