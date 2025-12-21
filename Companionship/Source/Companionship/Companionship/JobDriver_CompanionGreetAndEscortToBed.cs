using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Companion-side job:
    /// - Claims a waiting visitor for a date
    /// - Greets them
    /// - Escorts them to a companion bed
    /// - Hands off to the CustomLovin job (which starts the visitor partner job)
    ///
    /// This job is responsible for releasing the visitor claim if it fails before handoff.
    /// </summary>
    public class JobDriver_CompanionGreetAndEscortToBed : JobDriver
    {
        private const int SocialXpIntervalTicks = 200;
        private const float SocialXpPerInterval = 8f;

        private bool claimedVisitor;
        private bool handedOffToLovin;

        private Pawn Visitor => job.GetTarget(TargetIndex.A).Pawn;
        private Building_Bed Bed => job.GetTarget(TargetIndex.B).Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn?.Map == null) return false;
            if (Visitor == null || Bed == null) return false;

            CompanionshipVisitorTracker tracker = pawn.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return false;

            // Claim visitor first (prevents double-claiming / stuck visitors).
            claimedVisitor = tracker.TryClaimVisitorForDate(Visitor, pawn);
            if (!claimedVisitor) return false;

            // Reserve visitor.
            if (!pawn.Reserve(Visitor, job, 1, -1, null, errorOnFailed))
            {
                tracker.ReleaseClaim(Visitor, pawn);
                return false;
            }

            // Reserve ALL sleeping slots on the bed.
            // IMPORTANT:
            // stackCount must NOT be 0 or -1 ("All") when maxPawns > 1, or RimWorld logs the warning/error you saw.
            if (!pawn.Reserve(Bed, job, Bed.SleepingSlotsCount, 1, null, errorOnFailed))
            {
                tracker.ReleaseClaim(Visitor, pawn);
                return false;
            }

            // Bind the bed into the active DateSession so rewards/quality are correct.
            tracker.BindSessionBed(Visitor, Bed);

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn == null || pawn.Map == null);

            this.FailOn(() => !CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(pawn));

            this.FailOn(() =>
                Visitor == null || !Visitor.Spawned || Visitor.DestroyedOrNull() || Visitor.Downed || Visitor.InMentalState);

            this.FailOn(() =>
                Bed == null || !Bed.Spawned || Bed.Destroyed || Bed.IsBurning());

            CompanionshipVisitorTracker tracker = pawn.Map?.GetComponent<CompanionshipVisitorTracker>();

            // Safety: if we fail before handing off to lovin, release the claim so the visitor isn't stuck.
            AddFinishAction((JobCondition cond) =>
            {
                if (handedOffToLovin) return;
                if (!claimedVisitor) return;

                tracker?.ReleaseClaim(Visitor, pawn);
            });

            // ------------------------------------------------------------
            // Go to visitor
            // ------------------------------------------------------------
            Toil goVisitor = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            goVisitor.AddPreTickAction(() => GainSocialXp(pawn));
            yield return goVisitor;

            // ------------------------------------------------------------
            // Greeting phase
            // ------------------------------------------------------------
            Toil greeting = ToilMaker.MakeToil("Companionship_Greeting");
            greeting.defaultCompleteMode = ToilCompleteMode.Delay;
            greeting.defaultDuration = CompanionshipTuning.GreetingDurationTicks;

            greeting.initAction = () =>
            {
                tracker?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.Greeting);
                ForceVisitorGreetingJob();
            };

            greeting.tickAction = () =>
            {
                GainSocialXp(pawn);

                if (Visitor.jobs == null || Visitor.CurJobDef != CompanionshipDefOf.Companionship_VisitorParticipateGreeting)
                    ForceVisitorGreetingJob();

                if (Visitor.Spawned)
                {
                    pawn.rotationTracker?.FaceCell(Visitor.Position);
                    Visitor.rotationTracker?.FaceCell(pawn.Position);
                }
            };

            yield return greeting;

            // ------------------------------------------------------------
            // Escort phase -> go to bed slot 0
            // ------------------------------------------------------------
            Toil goBedSlot0 = ToilMaker.MakeToil("Companionship_GotoBedSlot0");
            goBedSlot0.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            goBedSlot0.initAction = () =>
            {
                if (pawn?.Map == null || Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                tracker?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.EscortingToBed);
                tracker?.BindSessionBed(Visitor, Bed);

                ForceVisitorGoToBedJob();

                IntVec3 slot0 = GetSlotCellSafe(Bed, 0);
                if (!slot0.IsValid) slot0 = Bed.Position;

                if (!pawn.CanReach(slot0, PathEndMode.OnCell, Danger.Some))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.pather?.StartPath(slot0, PathEndMode.OnCell);
            };

            goBedSlot0.tickAction = () =>
            {
                GainSocialXp(pawn);

                if (Bed != null)
                {
                    IntVec3 slot0 = GetSlotCellSafe(Bed, 0);
                    if (!slot0.IsValid) slot0 = Bed.Position;

                    if (pawn.Position != slot0 && pawn.pather != null && !pawn.pather.Moving)
                        pawn.pather.StartPath(slot0, PathEndMode.OnCell);
                }

                if (Visitor?.jobs == null || Visitor.CurJobDef != CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed)
                    ForceVisitorGoToBedJob();
            };

            yield return goBedSlot0;

            // ------------------------------------------------------------
            // Wait until visitor is actually on bed slot 1
            // ------------------------------------------------------------
            int waitStartTick = -1;
            Toil waitVisitorAtBed = ToilMaker.MakeToil("Companionship_WaitVisitorAtBed");
            waitVisitorAtBed.defaultCompleteMode = ToilCompleteMode.Never;

            waitVisitorAtBed.initAction = () =>
            {
                waitStartTick = Find.TickManager.TicksGame;
            };

            waitVisitorAtBed.tickAction = () =>
            {
                GainSocialXp(pawn);

                if (Visitor == null || Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                if (slot1.IsValid && Visitor.Position == slot1)
                {
                    ReadyForNextToil();
                    return;
                }

                IntVec3 slot0 = GetSlotCellSafe(Bed, 0);
                if (slot0.IsValid && pawn.Position != slot0)
                {
                    pawn.pather?.StartPath(slot0, PathEndMode.OnCell);
                }
                else
                {
                    pawn.pather?.StopDead();
                }

                ForceVisitorGoToBedJob();

                int timeout = CompanionshipTuning.WaitForVisitorAtBedTimeoutTicks;
                if (timeout < 250) timeout = 250;

                if (waitStartTick >= 0 && Find.TickManager.TicksGame - waitStartTick > timeout)
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };

            yield return waitVisitorAtBed;

            // ------------------------------------------------------------
            // Start custom lovin
            // ------------------------------------------------------------
            Toil startLovin = ToilMaker.MakeToil("Companionship_StartCustomLovin");
            startLovin.initAction = () =>
            {
                if (pawn?.Map == null || Visitor == null || Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                if (slot1.IsValid && Visitor.Position != slot1)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                tracker?.SetVisitorState(Visitor, CompanionshipVisitorTracker.DateState.AtBed);
                tracker?.BindSessionBed(Visitor, Bed);

                Job lovin = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CustomLovin, Visitor, Bed);
                lovin.ignoreForbidden = true;

                int expiry = CompanionshipTuning.LovinJobExpiryTicks;
                if (expiry <= 0)
                    expiry = CompanionshipTuning.LovinDurationTicks + 1200;

                lovin.expiryInterval = expiry;

                handedOffToLovin = true;
                pawn.jobs.StartJob(lovin, JobCondition.InterruptForced);
            };
            startLovin.defaultCompleteMode = ToilCompleteMode.Instant;

            yield return startLovin;
        }

        private void ForceVisitorGreetingJob()
        {
            if (Visitor?.jobs == null) return;
            if (pawn == null) return;

            Job vJob = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorParticipateGreeting, pawn);
            vJob.ignoreForbidden = true;

            int expiry = CompanionshipTuning.GreetingDurationTicks + CompanionshipTuning.VisitorGreetingExpiryPaddingTicks;
            if (expiry < 300) expiry = 300;
            vJob.expiryInterval = expiry;

            Visitor.jobs.StartJob(vJob, JobCondition.InterruptForced);
        }

        private void ForceVisitorGoToBedJob()
        {
            if (Visitor?.jobs == null) return;
            if (pawn == null || Bed == null) return;

            Job follow = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed, pawn, Bed);
            follow.ignoreForbidden = true;

            int expiry = CompanionshipTuning.VisitorFollowJobExpiryTicks;
            if (expiry < 600) expiry = 600;
            follow.expiryInterval = expiry;

            Visitor.jobs.StartJob(follow, JobCondition.InterruptForced);
        }

        private void GainSocialXp(Pawn p)
        {
            if (p?.skills == null) return;
            if (!p.IsHashIntervalTick(SocialXpIntervalTicks)) return;

            p.skills.Learn(SkillDefOf.Social, SocialXpPerInterval, true);
        }

        private static IntVec3 GetSlotCellSafe(Building_Bed bed, int slotIndex)
        {
            try
            {
                if (bed == null) return IntVec3.Invalid;

                int slots = bed.SleepingSlotsCount;
                if (slots <= 0) return bed.Position;

                if (slotIndex < 0) slotIndex = 0;
                if (slotIndex >= slots) slotIndex = slots - 1;

                return bed.GetSleepingSlotPos(slotIndex);
            }
            catch
            {
                return bed != null ? bed.Position : IntVec3.Invalid;
            }
        }
    }
}
