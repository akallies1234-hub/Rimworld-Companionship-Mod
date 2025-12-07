using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    /// <summary>
    /// Main Companion-side job driver for performing a date with a client.
    /// 
    /// Targets:
    ///  - TargetA: client Pawn
    ///  - TargetB: CompanionBed building
    /// 
    /// Flow:
    /// 1) Companion goes to the client (who should be waiting at a Companion Spot).
    /// 2) When the Companion reaches the client, we start the client-side
    ///    JobDriver_CompanionDateClient so the visitor walks to the bed too.
    /// 3) Both move to the bed and the "date" sequence plays out.
    /// 4) Outcome is calculated, payment is made, comps are notified, and both jobs end.
    /// </summary>
    public class JobDriver_CompanionDate : JobDriver
    {
        private const int TicksDurationLovin = 400; // Placeholder duration; can be scripted later.

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
            // Basic failure conditions.
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => Client?.Map != pawn.Map);

            // 1) Go to the client (who should be waiting at the Companion Spot).
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2) Once we've reached the client, start the client-side date job so they
            //    will head to the bed as well.
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

                    // Build the client-side date job:
                    //  - TargetA: the Companion pawn
                    //  - TargetB: the same bed
                    Job clientJob = JobMaker.MakeJob(CompanionshipDefOf.DoCompanionDate_Client, companion, bed);
                    clientJob.locomotionUrgency = LocomotionUrgency.Walk;
                    clientJob.ignoreJoyTimeAssignment = true;

                    // Give the job to the visitor. Using TryTakeOrderedJob keeps this
                    // consistent with how we assign the waiting job.
                    client.jobs.TryTakeOrderedJob(clientJob);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return startClientJob;

            // 3) Companion goes to the bed's interaction cell.
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);

            // 4) Main "date" sequence (placeholder: just a timed wait).
            Toil lovinToil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = TicksDurationLovin
            };
            lovinToil.WithProgressBarToilDelay(TargetIndex.A);
            yield return lovinToil;

            // 5) Finish: calculate outcome, handle payment, notify comps, end both jobs.
            Toil finish = new Toil
            {
                initAction = FinishDate,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return finish;
        }

        /// <summary>
        /// Called at the end of the date sequence:
        /// - Calculates outcome and applies thoughts.
        /// - Pays the Companion (if outcome warrants it).
        /// - Updates XP / stats via CompCompanionship.
        /// - Notifies the visitor comp that service was received.
        /// - Explicitly ends the client's job so they don't get stuck.
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

            Map map = companion.Map;

            // Calculate the outcome and apply thoughts.
            DateOutcome outcome = DateOutcomeUtility.CalculateOutcome(companion, client, bed);
            DateOutcomeUtility.ApplyOutcomeThoughts(client, outcome);

            // Handle payment.
            PaymentUtility.HandlePayment(companion, client, outcome, map);

            // Companion XP / tracking.
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
