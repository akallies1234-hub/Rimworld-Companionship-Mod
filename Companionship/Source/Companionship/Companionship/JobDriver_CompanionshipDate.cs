using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_CompanionshipDate : JobDriver
    {
        // A = guest (visitor)
        // B = companion bed
        private Pawn Guest => job.GetTarget(TargetIndex.A).Pawn;
        private Building_Bed CompanionBed => job.GetTarget(TargetIndex.B).Thing as Building_Bed;

        private const int PaymentSilver = 50;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn guest = Guest;
            if (guest == null) return false;

            // Only reserve the guest. Bed reservation is a common source of instant-fail loops with visitors.
            if (!pawn.Reserve(guest, job, 1, -1, null, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            this.FailOn(() =>
            {
                Pawn g = Guest;
                Building_Bed bed = CompanionBed;
                if (g == null || bed == null) return true;
                if (!CompanionshipDateUtility.IsValidDateGuest(g)) return true;
                if (!bed.Spawned || bed.DestroyedOrNull()) return true;
                if (bed.SleepingSlotsCount < 2) return true;
                return false;
            });

            // Extend cooldown so even if something blows up later, we don't spam jobs.
            yield return Toils_General.DoAtomic(() =>
            {
                CompanionshipDateUtility.SetDateCooldown(pawn, GenDate.TicksPerHour);  // 1 in-game hour
                CompanionshipDateUtility.SetDateCooldown(Guest, GenDate.TicksPerHour); // 1 in-game hour
            });

            // Step 1: Go meet guest
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Step 2: Talk for 1 hour
            Toil talk = Toils_General.Wait(GenDate.TicksPerHour, TargetIndex.A);
            talk.WithProgressBarToilDelay(TargetIndex.A);
            talk.handlingFacing = true;
            talk.socialMode = RandomSocialMode.SuperActive;
            talk.tickAction = delegate
            {
                Pawn g = Guest;
                if (g == null) return;

                if (pawn.IsHashIntervalTick(120))
                    pawn.interactions.TryInteractWith(g, InteractionDefOf.Chitchat);
            };
            yield return talk;

            // Step 3: Go to bed
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            // Step 4: Start guest LayDown and queue Lovin
            Toil start = new Toil();
            start.initAction = delegate
            {
                try
                {
                    Pawn g = Guest;
                    Building_Bed bed = CompanionBed;

                    if (g == null || bed == null || bed.Map == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (bed.SleepingSlotsCount < 2 || bed.AnyOccupants)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    int bedPhaseTicks = Rand.RangeInclusive(1, 3) * GenDate.TicksPerHour;
                    int expireTick = Find.TickManager.TicksGame + bedPhaseTicks + 1200;

                    // Optional: your temporary lovin context (if your component exists)
                    GameComponent_Companionship gc = GameComponent_Companionship.GetOrCreate();
                    if (gc != null)
                    {
                        gc.AllowLovin(pawn, g, bed.Position, bed.Map.uniqueID, PaymentSilver, expireTick);
                    }

                    // Force guest to lie down
                    Job layDown = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                    layDown.expiryInterval = bedPhaseTicks;
                    layDown.checkOverrideOnExpire = true;
                    layDown.playerForced = true;

                    g.jobs.StartJob(layDown, JobCondition.InterruptForced, null, true, true);

                    // Queue authentic vanilla lovin for the worker.
                    Job lovinJob = JobMaker.MakeJob(JobDefOf.Lovin, g, bed);
                    lovinJob.playerForced = true;

                    pawn.jobs.jobQueue.EnqueueFirst(lovinJob);

                    EndJobWith(JobCondition.Succeeded);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Companionship] Exception starting date job: {ex}");
                    // prevent immediate retry spam even on exception
                    CompanionshipDateUtility.SetDateCooldown(pawn, 2000);
                    CompanionshipDateUtility.SetDateCooldown(Guest, 2000);
                    EndJobWith(JobCondition.Errored);
                }
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
        }
    }
}
