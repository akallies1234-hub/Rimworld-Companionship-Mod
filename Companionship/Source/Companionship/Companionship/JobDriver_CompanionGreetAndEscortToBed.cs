using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_CompanionGreetAndEscortToBed : JobDriver
    {
        private Pawn Visitor => job.targetA.Pawn;
        private Building_Bed Bed => job.targetB.Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Visitor == null || Bed == null) return false;

            if (!pawn.Reserve(Visitor, job, 1, -1, null, errorOnFailed))
                return false;

            if (!pawn.Reserve(Bed, job, 1, -1, null, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => Visitor == null || !Visitor.Spawned || Visitor.Downed || Visitor.InMentalState);

            // --- Greeting phase ---
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil greeting = ToilMaker.MakeToil("Companionship_Greeting");
            greeting.defaultCompleteMode = ToilCompleteMode.Delay;
            greeting.defaultDuration = GenDate.TicksPerHour;

            greeting.initAction = () =>
            {
                pawn.Map?.GetComponent<CompanionshipVisitorTracker>()
                    ?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.Greeting);

                ForceVisitorGreetingJob();
            };

            greeting.tickAction = () =>
            {
                if (Visitor == null || !Visitor.Spawned)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceTarget(Visitor);

                // Ensure visitor stays on our greeting job (visitors can get overridden)
                if (Visitor.CurJobDef != CompanionshipDefOf.Companionship_VisitorParticipateGreeting)
                    ForceVisitorGreetingJob();

                // Make it feel social without spamming
                int now = Find.TickManager.TicksGame;
                if (now % 600 == 0)
                    pawn.interactions?.TryInteractWith(Visitor, InteractionDefOf.Chitchat);
            };

            yield return greeting;

            // --- Escort phase ---
            Toil startEscort = ToilMaker.MakeToil("Companionship_StartEscort");
            startEscort.initAction = () =>
            {
                pawn.Map?.GetComponent<CompanionshipVisitorTracker>()
                    ?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.EscortingToBed);

                ForceVisitorGoToBedJob();
            };
            yield return startEscort;

            // While we walk to the bed, keep the visitor on the "go to bed" job
            Toil goBed = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            goBed.tickAction = () =>
            {
                if (Visitor != null && Visitor.Spawned && Visitor.CurJobDef != CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed)
                {
                    // If visitor isn't already basically at the bed, re-enforce
                    if (Visitor.Position.DistanceToSquared(Bed.Position) > 4)
                        ForceVisitorGoToBedJob();
                }
            };
            yield return goBed;

            // At bed: kick off custom lovin (this starts partner job too)
            Toil startLovin = ToilMaker.MakeToil("Companionship_StartCustomLovin");
            startLovin.initAction = () =>
            {
                if (Visitor == null || Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.Map?.GetComponent<CompanionshipVisitorTracker>()
                    ?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.AtBed);

                Job lovin = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CustomLovin, Visitor, Bed);
                lovin.ignoreForbidden = true;
                lovin.expiryInterval = 3 * GenDate.TicksPerHour;

                // Start immediately
                pawn.jobs.StartJob(lovin, JobCondition.InterruptForced);
            };
            yield return startLovin;
        }

        private void ForceVisitorGreetingJob()
        {
            if (Visitor?.jobs == null || !Visitor.Spawned) return;

            Job vJob = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorParticipateGreeting, pawn);
            vJob.ignoreForbidden = true;
            vJob.expiryInterval = GenDate.TicksPerHour + 120;

            Visitor.jobs.StartJob(vJob, JobCondition.InterruptForced);
        }

        private void ForceVisitorGoToBedJob()
        {
            if (Visitor?.jobs == null || !Visitor.Spawned || Bed == null) return;

            Job follow = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed, pawn, Bed);
            follow.ignoreForbidden = true;
            follow.expiryInterval = 6 * GenDate.TicksPerHour;

            Visitor.jobs.StartJob(follow, JobCondition.InterruptForced);
        }
    }
}
