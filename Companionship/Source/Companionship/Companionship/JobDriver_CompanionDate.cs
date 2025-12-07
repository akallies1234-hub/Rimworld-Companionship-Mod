using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace Riot.Companionship
{
    public class JobDriver_CompanionDate : JobDriver
    {
        private Pawn Companion => pawn;                  // The worker doing the service
        private Pawn Client => (Pawn)job.targetA.Thing;  // The visitor requesting service
        private Building_Bed Bed => (Building_Bed)job.targetB.Thing;

        private DateScriptDef selectedScript;
        private int scriptStepIndex = 0;
        private int socialTicks = 0;
        private const int PreSocialDuration = 500;       // 500 ticks ≈ 8.3 seconds

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Client == null || Bed == null) return false;

            return pawn.Reserve(Client, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(Bed, job, 1, -1, null, errorOnFailed);
        }

        // Called by WorkGiver before job starts
        public void SetSelectedScript(DateScriptDef script)
        {
            selectedScript = script;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (selectedScript == null)
            {
                Log.Error("[Companionship] JobDriver_CompanionDate started with NO selected script!");
                yield break;
            }

            // Fail if client despawns or becomes downed
            this.FailOn(() => Client == null || Client.Dead || Client.Downed);

            // ---------------------------
            // 1. Move companion to client
            // ---------------------------
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // ---------------------------
            // 2. Pre-lovin social phase
            // ---------------------------
            yield return PreLovinConversationToil();

            // ---------------------------
            // 3. Walk companion + client to the bed
            // ---------------------------
            foreach (var t in WalkBothToBedToils())
                yield return t;

            // ---------------------------
            // 4. Perform lovin
            // ---------------------------
            yield return LovinToil();

            // ---------------------------
            // 5. Finish the date: payment + thoughts
            // ---------------------------
            yield return FinishDateToil();
        }


        // ============================================================
        //  PRE-LOVIN SOCIAL TOIL — they face each other + talk effect
        // ============================================================
        private Toil PreLovinConversationToil()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                socialTicks = 0;

                Companion.rotationTracker.FaceCell(Client.Position);
                Client.rotationTracker.FaceCell(Companion.Position);

                // Stop the client from wandering away during pre-social
                Client.pather.StopDead();
            };

            toil.tickAction = () =>
            {
                socialTicks++;

                // Continuously face each other
                Companion.rotationTracker.FaceCell(Client.Position);
                Client.rotationTracker.FaceCell(Companion.Position);

                // Show social interaction motes
                if (socialTicks % 150 == 0)
                {
                    MoteMaker.ThrowText(Client.DrawPos, Client.Map, "chat");
                    MoteMaker.ThrowText(Companion.DrawPos, Companion.Map, "chat");
                }

                if (socialTicks >= PreSocialDuration)
                    ReadyForNextToil();
            };

            toil.defaultDuration = PreSocialDuration;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }


        // ============================================================
        //  MOVE BOTH TO BED — true paired walk (non-sprinting)
        // ============================================================
        private IEnumerable<Toil> WalkBothToBedToils()
        {
            // Companion walks normally
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            // Client follows at same speed
            Toil clientFollow = new Toil();
            clientFollow.initAction = () =>
            {
                Client.jobs.StopAll();
                Client.pather.StartPath(Bed.InteractionCell, PathEndMode.Touch);
                Client.pather.moveSpeed = Pawn_MovementSpeed.Walk; // force walking
            };
            clientFollow.defaultCompleteMode = ToilCompleteMode.Delay;
            clientFollow.defaultDuration = 1;
            yield return clientFollow;

            // Ensure both are at the bedside
            Toil wait = new Toil();
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = 30;
            yield return wait;
        }


        // ============================================================
        //  LOVIN TOIL — use real vanilla-style lay-down lovin
        // ============================================================
        private Toil LovinToil()
        {
            Toil toil = new Toil();

            toil.initAction = () =>
            {
                if (!Bed.Owners.Contains(Companion))
                    Bed.CompAssignableToPawn.ForceAddPawn(Companion);

                if (!Bed.Owners.Contains(Client))
                    Bed.CompAssignableToPawn.ForceAddPawn(Client);

                Companion.pather.StopDead();
                Client.pather.StopDead();

                Companion.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Lovin, Client, Bed),
                    JobCondition.InterruptForced);

                Client.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Lovin, Companion, Bed),
                    JobCondition.InterruptForced);
            };

            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 600; // ≈ 10 seconds
            return toil;
        }


        // ============================================================
        //   FINISH THE DATE — payment + outcome + state reset
        // ============================================================
        private Toil FinishDateToil()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                // Payment
                PaymentUtility.HandlePayment(Companion, Client);

                // Outcome thoughts (placeholder)
                DateOutcomeUtility.ApplyOutcomeThoughts(Companion, Client, true);

                // Mark the client's comp state
                var vcomp = Client.GetComp<CompVisitorCompanionship>();
                vcomp?.Notify_ServiceReceived();

                // End their jobs
                Client.jobs.EndCurrentJob(JobCondition.Succeeded);
                Companion.jobs.EndCurrentJob(JobCondition.Succeeded);
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}
