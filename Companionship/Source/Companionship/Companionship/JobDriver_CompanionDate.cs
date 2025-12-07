using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    /// <summary>
    /// Main Companion-side job driver for performing a date with a client.
    ///
    /// Targets:
    /// - TargetA: client Pawn
    /// - TargetB: CompanionBed building
    ///
    /// Tier 1 baseline flow:
    /// 1) Companion goes to the client (who should be waiting at the Companion Spot).
    /// 2) Companion and client stand and face each other for a short "social conversation"
    ///    (200 ticks with a progress bar).
    /// 3) We start the client-side date job so the visitor will walk to the bed.
    /// 4) Companion walks to the bed as well (no sprinting).
    /// 5) Both use the bed for a "lovin" stage (600 ticks with a progress bar).
    /// 6) Outcome is calculated, payment is made, comps are notified, and both jobs end.
    /// </summary>
    public class JobDriver_CompanionDate : JobDriver
    {
        // Durations for each stage of the Tier 1 script.
        private const int TicksConversation = 200;
        private const int TicksLovin = 600;

        // The script selected for this particular date. We still select a script so
        // we can expand this later, but for now the flow is fixed to the Tier 1 pattern.
        private DateScriptDef selectedScript;

        protected Pawn Client => TargetA.Pawn;
        protected Building_Bed Bed => TargetB.Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn client = Client;
            Building_Bed bed = Bed;

            if (client == null || bed == null)
            {
                return false;
            }

            bool reservedClient = pawn.Reserve(client, job, 1, -1, null, errorOnFailed);
            bool reservedBed = pawn.Reserve(bed, job, 1, -1, null, errorOnFailed);

            return reservedClient && reservedBed;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Choose a script so we have it available for future expansion.
            // For now, the Tier 1 flow is hard-coded below.
            selectedScript = DateScriptUtility.SelectScriptFor(pawn, Client);

            // Ensure movement for this job is a walk, not a sprint.
            if (job != null)
            {
                job.locomotionUrgency = LocomotionUrgency.Walk;
            }

            // Basic failure conditions.
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => Client?.Map != pawn.Map);

            // 1) Companion goes to the client (who should be waiting at the Companion Spot).
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2) Short "social conversation" where they stand and face each other.
            yield return MakeConversationToil();

            // 3) Once the conversation finishes, start the client-side date job so the
            // visitor will walk to the bed.
            Toil startClientJob = new Toil
            {
                initAction = () =>
                {
                    Pawn companion = pawn;
                    Pawn client = Client;
                    Building_Bed bed = Bed;

                    if (client == null || bed == null || !client.Spawned)
                    {
                        return;
                    }

                    Job clientJob = JobMaker.MakeJob(CompanionshipDefOf.DoCompanionDate_Client, companion, bed);
                    clientJob.locomotionUrgency = LocomotionUrgency.Walk;
                    clientJob.ignoreJoyTimeAssignment = true;

                    client.jobs.TryTakeOrderedJob(clientJob);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return startClientJob;

            // 4) Companion walks to the bed's interaction cell (no sprint).
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            // 5) Lovin stage at the bed (600 ticks with progress bar).
            yield return MakeLovinToil();

            // 6) Finish: calculate outcome, handle payment, notify comps, end both jobs.
            Toil finish = new Toil
            {
                initAction = FinishDate,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return finish;
        }

        /// <summary>
        /// Companion + client stand facing each other for a short "social conversation"
        /// with a 200-tick progress bar. This is our Tier 1 opener.
        /// </summary>
        private Toil MakeConversationToil()
        {
            Toil toil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = TicksConversation
            };

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

            // Show progress for the "conversation" so the player can see the stage.
            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.socialMode = RandomSocialMode.SuperActive;

            return toil;
        }

        /// <summary>
        /// Main "lovin" stage at the bed: 600 ticks with a progress bar.
        /// This represents the actual intimate part of the date.
        /// </summary>
        private Toil MakeLovinToil()
        {
            Toil toil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = TicksLovin
            };

            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.socialMode = RandomSocialMode.SuperActive;

            return toil;
        }

        /// <summary>
        /// Called at the end of the date sequence:
        /// - Calculates outcome and applies thoughts.
        /// - Pays the Companion.
        /// - Updates XP / stats via CompCompanionship.
        /// - Notifies the visitor comp that service was received.
        /// - Explicitly ends the client's job so they don't get stuck.
        ///
        /// Reaching this point counts as a "completed date" for progression purposes;
        /// the outcome (Terrible/Bad/Good/Excellent) still determines success tracking.
        /// </summary>
        private void FinishDate()
        {
            Pawn companion = pawn;
            Pawn client = Client;
            Building_Bed bed = Bed;

            if (client == null || bed == null)
            {
                return;
            }

            // Calculate the outcome and apply thoughts to both pawns.
            DateOutcome outcome = DateOutcomeUtility.CalculateOutcome(companion, client, bed);
            DateOutcomeUtility.ApplyDateOutcome(companion, client, outcome);

            // Handle payment (spawns silver near the bed / companion).
            PaymentUtility.PayForDate(companion, client, outcome, bed);

            // Companion XP / tracking. This should increment their "date completed" count
            // and flag success based on the outcome, same as before.
            CompCompanionship comp = companion.TryGetComp<CompCompanionship>();
            if (comp != null)
            {
                comp.Notify_DateFinished(outcome);
            }

            // Visitor-side tracking.
            CompVisitorCompanionship visitorComp = client.TryGetComp<CompVisitorCompanionship>();
            if (visitorComp != null)
            {
                visitorComp.Notify_ServiceReceived();
            }

            // Explicitly end the client's current job (their client-side date job).
            client.jobs?.EndCurrentJob(JobCondition.Succeeded);

            // End the companion's job as well.
            companion.jobs?.EndCurrentJob(JobCondition.Succeeded);
        }
    }
}
