using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Companion-side lovin job (initiator). Handles:
    /// - Ensuring the visitor is in position / has the partner job
    /// - Holstering + re-equipping the companion's primary weapon
    /// - Ending the date session as Success ONLY if the full lovin duration completes
    /// - Ending the date session as a failure if interrupted mid-lovin
    /// - Heart flecks during lovin for feedback/immersion
    /// </summary>
    public class JobDriver_CustomLovin : JobDriver
    {
        private Pawn Partner => job?.targetA.Pawn;
        private Building_Bed Bed => job?.targetB.Thing as Building_Bed;

        // Weapon holstering
        private ThingWithComps holsteredPrimary;
        private bool didHolster;

        // Date/session completion guard
        private bool endHandled;
        private bool lovinStarted;

        // NOTE: do NOT name this "startTick" (JobDriver already has startTick)
        private int lovinStartTick = -1;

        private bool dateEnded;

        // Partner enforcement (prevents stalling / early timeouts)
        private int lastPartnerEnforceTick = -999999;

        // Social XP
        private const int SocialXpIntervalTicks = 150;
        private const float SocialXpPerInterval = 0.11f;

        // Safety timeouts / padding
        private const int WaitForPartnerTimeoutTicks = 2500;
        private const int PartnerJobExpiryPaddingTicks = 600;
        private const int PartnerEnforceCooldownTicks = 200;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holsteredPrimary, "holsteredPrimary");
            Scribe_Values.Look(ref didHolster, "didHolster");
            Scribe_Values.Look(ref endHandled, "endHandled");
            Scribe_Values.Look(ref lovinStarted, "lovinStarted");

            // Keep the old save key name to avoid breaking in-progress saves.
            Scribe_Values.Look(ref lovinStartTick, "startTick", -1);

            Scribe_Values.Look(ref dateEnded, "dateEnded");
            Scribe_Values.Look(ref lastPartnerEnforceTick, "lastPartnerEnforceTick", -999999);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Partner == null || Bed == null) return false;

            // Reserve the visitor.
            if (!pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed))
                return false;

            // Reserve both sleeping slots on the bed.
            // IMPORTANT: stackCount must not be 0 or -1 ("All") when maxPawns > 1.
            if (!pawn.Reserve(Bed, job, Bed.SleepingSlotsCount, 1, null, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn == null || pawn.Map == null);
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);

            this.FailOn(() => !CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(pawn));

            AddFinishAction(HandleJobEnd);

            // Go to our bed slot (slot 0)
            Toil goToBed = Toils_Goto.GotoCell(GetSlotCellSafe(Bed, 0), PathEndMode.OnCell);
            goToBed.tickAction = () => GainSocialXp(pawn);
            yield return goToBed;

            // Wait until the visitor is at slot 1
            int waitStartTick = -1;
            Toil waitForPartnerAtBed = ToilMaker.MakeToil();
            waitForPartnerAtBed.initAction = () =>
            {
                waitStartTick = Find.TickManager.TicksGame;

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                EnsurePartnerJob(force: true);
            };

            waitForPartnerAtBed.defaultCompleteMode = ToilCompleteMode.Never;
            waitForPartnerAtBed.tickAction = () =>
            {
                GainSocialXp(pawn);

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                if (Partner == null || Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                if (slot1.IsValid && Partner.Position == slot1)
                {
                    ReadyForNextToil();
                    return;
                }

                EnsurePartnerJob();

                if (waitStartTick >= 0 && Find.TickManager.TicksGame - waitStartTick > WaitForPartnerTimeoutTicks)
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            yield return waitForPartnerAtBed;

            // Lovin stage
            Toil lovin = ToilMaker.MakeToil();
            lovin.initAction = () =>
            {
                lovinStarted = true;
                lovinStartTick = Find.TickManager.TicksGame;

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                TryHolsterPrimary(pawn);

                EnsurePartnerJob(force: true);
            };

            lovin.defaultCompleteMode = ToilCompleteMode.Delay;
            lovin.defaultDuration = CompanionshipTuning.LovinDurationTicks;

            lovin.tickAction = () =>
            {
                GainSocialXp(pawn);

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                EnsurePartnerJob();

                // Heart flecks (visual feedback)
                if (pawn.Map != null && pawn.IsHashIntervalTick(CompanionshipTuning.LovinHeartFleckIntervalTicks))
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }
            };

            // End date success only if full duration completed
            lovin.AddFinishAction(() =>
            {
                if (dateEnded) return;

                bool completedFullDuration = false;
                if (lovinStartTick >= 0)
                {
                    int elapsed = Find.TickManager.TicksGame - lovinStartTick;
                    completedFullDuration = elapsed >= CompanionshipTuning.LovinDurationTicks - 1;
                }

                CompanionshipVisitorTracker tracker = pawn.Map?.GetComponent<CompanionshipVisitorTracker>();

                if (completedFullDuration)
                {
                    tracker?.CompleteDate(Partner, pawn);
                }
                else
                {
                    tracker?.TryEndSession(Partner, DetermineFailureReason(JobCondition.InterruptForced));
                }

                dateEnded = true;
            });

            yield return lovin;
        }

        private void EnsurePartnerJob(bool force = false)
        {
            if (Partner?.jobs == null) return;
            if (Bed == null) return;

            int now = Find.TickManager.TicksGame;

            if (!force && now - lastPartnerEnforceTick < PartnerEnforceCooldownTicks)
                return;

            if (!force && Partner.CurJobDef == CompanionshipDefOf.Companionship_CustomLovinPartner)
            {
                lastPartnerEnforceTick = now;
                return;
            }

            Job partnerJob = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CustomLovinPartner, pawn, Bed);
            partnerJob.ignoreForbidden = true;
            partnerJob.expiryInterval = CompanionshipTuning.LovinDurationTicks + PartnerJobExpiryPaddingTicks;

            Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
            lastPartnerEnforceTick = now;
        }

        private void HandleJobEnd(JobCondition condition)
        {
            if (endHandled) return;
            endHandled = true;

            TryReequipPrimary(pawn);

            // Stop partner job so visitor doesn't hang.
            if (Partner?.jobs != null &&
                Partner.CurJobDef == CompanionshipDefOf.Companionship_CustomLovinPartner &&
                Partner.CurJob != null &&
                Partner.CurJob.targetA.Pawn == pawn)
            {
                Partner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // If lovin started but we didn't end the date already, end the session now.
            if (lovinStarted && !dateEnded)
            {
                CompanionshipVisitorTracker tracker = pawn.Map?.GetComponent<CompanionshipVisitorTracker>();
                tracker?.TryEndSession(Partner, DetermineFailureReason(condition));
                dateEnded = true;
            }
        }

        private void GainSocialXp(Pawn p)
        {
            if (p?.skills == null) return;
            if (p.IsHashIntervalTick(SocialXpIntervalTicks))
                p.skills.Learn(SkillDefOf.Social, SocialXpPerInterval);
        }

        private DateEndReason DetermineFailureReason(JobCondition condition)
        {
            if (Bed == null || Bed.DestroyedOrNull() || !Bed.Spawned || Bed.IsBurning())
                return DateEndReason.BedInvalid;

            if (Partner == null || Partner.DestroyedOrNull() || !Partner.Spawned || Partner.Downed || Partner.InMentalState)
                return DateEndReason.VisitorInvalid;

            if (pawn == null || pawn.DestroyedOrNull() || !pawn.Spawned || pawn.Downed || pawn.InMentalState)
                return DateEndReason.CompanionInvalid;

            if (condition == JobCondition.InterruptForced || condition == JobCondition.InterruptOptional)
                return DateEndReason.VisitorPulledFromPipeline;

            return DateEndReason.Timeout;
        }

        private void TryHolsterPrimary(Pawn p)
        {
            if (didHolster) return;
            if (p?.equipment == null) return;
            if (p.inventory?.innerContainer == null) return;

            ThingWithComps primary = p.equipment.Primary;
            if (primary == null) return;

            bool moved = p.equipment.TryTransferEquipmentToContainer(primary, p.inventory.innerContainer);
            if (moved)
            {
                holsteredPrimary = primary;
                didHolster = true;
            }
        }

        private void TryReequipPrimary(Pawn p)
        {
            if (!didHolster) return;
            if (holsteredPrimary == null)
            {
                didHolster = false;
                return;
            }

            if (p?.equipment == null || p.inventory?.innerContainer == null)
                return;

            if (p.inventory.innerContainer.Contains(holsteredPrimary))
            {
                int movedCount = p.inventory.innerContainer.TryTransferToContainer(
                    holsteredPrimary,
                    p.equipment.GetDirectlyHeldThings(),
                    1);

                if (movedCount <= 0)
                {
                    p.equipment.AddEquipment(holsteredPrimary);
                }
            }
            else
            {
                p.equipment.AddEquipment(holsteredPrimary);
            }

            holsteredPrimary = null;
            didHolster = false;
        }

        private static IntVec3 GetSlotCellSafe(Building_Bed bed, int slotIndex)
        {
            if (bed == null) return IntVec3.Invalid;

            int slots = bed.SleepingSlotsCount;
            if (slots <= 0) return bed.Position;

            if (slotIndex < 0) slotIndex = 0;
            if (slotIndex >= slots) slotIndex = slots - 1;

            return bed.GetSleepingSlotPos(slotIndex);
        }
    }
}
