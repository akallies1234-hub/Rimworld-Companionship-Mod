using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Riot.Companionship
{
    /// <summary>
    /// Companion-side job driver for performing a Tier 1 date.
    ///
    /// Flow:
    /// 1) Companion walks to client at the Companion Spot
    /// 2) Pre-lovin conversation (500 ticks, face each other, progress bar)
    /// 3) Client receives their client-side job and walks to the bed
    /// 4) Companion walks to the bed (no sprinting)
    /// 5) Lovin stage (600 ticks with heart motes)
    /// 6) Outcome + payment + XP
    /// 7) Clean termination of both jobs
    /// </summary>
    public class JobDriver_CompanionDate : JobDriver
    {
        private const int TicksConversation = 500;
        private const int TicksLovin = 600;

        private DateScriptDef selectedScript;

        protected Pawn Client => TargetA.Pawn;
        protected Building_Bed Bed => TargetB.Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn client = Client;
            Building_Bed bed = Bed;

            if (client == null || bed == null)
                return false;

            bool r1 = pawn.Reserve(client, job, 1, -1, null, errorOnFailed);
            bool r2 = pawn.Reserve(bed, job, 1, -1, null, errorOnFailed);
            return r1 && r2;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            selectedScript = DateScriptUtility.SelectScriptFor(pawn, Client);

            if (job != null)
                job.locomotionUrgency = LocomotionUrgency.Walk;

            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => Client?.Map != pawn.Map);

            // 1) Walk to the client
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2) Conversation stage
            yield return MakeConversationToil();

            // 3) Start client-side date job
            yield return StartClientJobToil();

            // 4) Companion walks to the bed
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell);

            // 5) Lovin stage
            yield return MakeLovinToil();

            // 6) Finish
            Toil finish = new Toil();
            finish.initAction = FinishDate;
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        // ------------------------------
        //   CONVERSATION STAGE
        // ------------------------------
        private Toil MakeConversationToil()
        {
            Toil toil = new Toil();
            toil.defaultDuration = TicksConversation;
            toil.defaultCompleteMode = ToilCompleteMode.Delay;

            toil.initAction = () =>
            {
                Pawn companion = pawn;
                Pawn client = Client;

                if (client != null && client.Spawned)
                {
                    companion.rotationTracker.FaceTarget(client);
                    client.rotationTracker.FaceTarget(companion);
                }
            };

            toil.tickAction = () =>
            {
                Pawn companion = pawn;
                Pawn client = Client;

                if (client != null && client.Spawned)
                {
                    companion.rotationTracker.FaceTarget(client);
                    client.rotationTracker.FaceTarget(companion);
                }
            };

            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.socialMode = RandomSocialMode.SuperActive;

            return toil;
        }

        // ------------------------------
        //   START CLIENT-SIDE JOB
        // ------------------------------
        private Toil StartClientJobToil()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Pawn client = Client;
                Building_Bed bed = Bed;

                if (client == null || bed == null || !client.Spawned)
                    return;

                Job clientJob = JobMaker.MakeJob(CompanionshipDefOf.DoCompanionDate_Client, pawn, bed);
                clientJob.locomotionUrgency = LocomotionUrgency.Walk;
                clientJob.ignoreJoyTimeAssignment = true;

                client.jobs.TryTakeOrderedJob(clientJob);
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        // ------------------------------
        //   LOVIN STAGE
        // ------------------------------
        private Toil MakeLovinToil()
        {
            Toil toil = new Toil();
            toil.defaultDuration = TicksLovin;
            toil.defaultCompleteMode = ToilCompleteMode.Delay;

            toil.initAction = () =>
            {
                Pawn companion = pawn;
                Pawn client = Client;

                if (client != null && client.Spawned)
                {
                    companion.rotationTracker.FaceTarget(client);
                    client.rotationTracker.FaceTarget(companion);
                }
            };

            toil.tickAction = () =>
            {
                Pawn companion = pawn;
                Pawn client = Client;
                Building_Bed bed = Bed;

                if (client != null && client.Spawned)
                {
                    companion.rotationTracker.FaceTarget(client);
                    client.rotationTracker.FaceTarget(companion);
                }

                // Throw heart text motes
                if (bed != null && bed.Spawned)
                {
                    if (Find.TickManager.TicksGame % 120 == 0)
                    {
                        MoteMaker.ThrowText(
                            bed.Position.ToVector3Shifted(),
                            bed.Map,
                            "♥",
                            Color.red
                        );
                    }
                }
            };

            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.socialMode = RandomSocialMode.SuperActive;

            return toil;
        }

        // ------------------------------
        //   FINISH STAGE
        // ------------------------------
        private void FinishDate()
        {
            Pawn companion = pawn;
            Pawn client = Client;
            Building_Bed bed = Bed;

            if (client == null || bed == null)
                return;

            // Outcome + thoughts
            DateOutcome outcome = DateOutcomeUtility.CalculateOutcome(companion, client, bed);
            DateOutcomeUtility.ApplyDateOutcome(companion, client, outcome);

            // Payment
            PaymentUtility.PayForDate(companion, client, outcome, bed);

            // Companion XP / tracking
            CompCompanionship comp = companion.TryGetComp<CompCompanionship>();
            comp?.Notify_DateFinished(outcome);

            // Visitor tracking
            CompVisitorCompanionship visitorComp = client.TryGetComp<CompVisitorCompanionship>();
            visitorComp?.Notify_ServiceReceived();

            // End client job
            client.jobs?.EndCurrentJob(JobCondition.Succeeded);

            // End companion job
            companion.jobs?.EndCurrentJob(JobCondition.Succeeded);
        }
    }
}
