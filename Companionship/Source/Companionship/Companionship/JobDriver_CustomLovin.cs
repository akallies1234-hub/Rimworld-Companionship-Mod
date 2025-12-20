using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Companionship
{
    public class JobDriver_CustomLovin : JobDriver
    {
        private Pawn Partner => job.targetA.Pawn;
        private Building_Bed Bed => job.targetB.Thing as Building_Bed;

        private const int LovinDuration = 2 * GenDate.TicksPerHour;

        // Weapon holstering
        private ThingWithComps holsteredPrimary;
        private bool didHolster;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holsteredPrimary, "holsteredPrimary");
            Scribe_Values.Look(ref didHolster, "didHolster");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Partner == null || Bed == null) return false;

            if (!pawn.Reserve(Partner, job, 1, -1, null, errorOnFailed))
                return false;

            if (!pawn.Reserve(Bed, job, 1, -1, null, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Partner == null || !Partner.Spawned || Partner.Downed || Partner.InMentalState);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            // Start partner job immediately (forced)
            Toil ensurePartner = ToilMaker.MakeToil("Companionship_EnsureLovinPartner");
            ensurePartner.initAction = () =>
            {
                if (Partner?.jobs == null || Bed == null) return;

                Job pJob = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CustomLovinPartner, pawn, Bed);
                pJob.ignoreForbidden = true;
                pJob.expiryInterval = LovinDuration + 600;

                Partner.jobs.StartJob(pJob, JobCondition.InterruptForced);
            };
            yield return ensurePartner;

            // Go to our bed slot (slot 0)
            yield return Toils_Goto.GotoCell(GetSlotCellSafe(Bed, 0), PathEndMode.OnCell);

            Toil lovin = ToilMaker.MakeToil("Companionship_CustomLovin");
            lovin.defaultCompleteMode = ToilCompleteMode.Delay;
            lovin.defaultDuration = LovinDuration;

            lovin.initAction = () =>
            {
                TryHolsterPrimary(pawn);

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                // Optional: play sound if it exists (safe lookup)
                SoundDef lovinSound = DefDatabase<SoundDef>.GetNamedSilentFail("Lovin");
                lovinSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            };

            lovin.tickAction = () =>
            {
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                if (Partner != null && Partner.Spawned)
                {
                    pawn.rotationTracker.FaceTarget(Partner);

                    if (Find.TickManager.TicksGame % 250 == 0)
                    {
                        FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                    }
                }
            };

            lovin.AddFinishAction(() =>
            {
                TryReequipPrimary(pawn);

                var tracker = pawn.Map?.GetComponent<CompanionshipVisitorTracker>();
                tracker?.CompleteDate(Partner, pawn);
            });

            yield return lovin;
        }

        private void TryHolsterPrimary(Pawn p)
        {
            if (didHolster) return;
            if (p?.equipment == null) return;

            ThingWithComps primary = p.equipment.Primary;
            if (primary == null) return;

            // Prefer moving into inventory so it persists safely
            if (p.inventory?.innerContainer != null)
            {
                bool moved = p.equipment.TryTransferEquipmentToContainer(primary, p.inventory.innerContainer);
                if (moved)
                {
                    holsteredPrimary = primary;
                    didHolster = true;
                }
            }
        }

        private void TryReequipPrimary(Pawn p)
        {
            if (!didHolster) return;
            if (holsteredPrimary == null) return;
            if (p == null || p.Destroyed || !p.Spawned) return;
            if (p.equipment == null) return;

            // If it's still in inventory, remove and re-add to equipment
            if (p.inventory?.innerContainer != null && p.inventory.innerContainer.Contains(holsteredPrimary))
            {
                p.inventory.innerContainer.Remove(holsteredPrimary);
                p.equipment.AddEquipment(holsteredPrimary);
            }

            holsteredPrimary = null;
            didHolster = false;
        }

        private static IntVec3 GetSlotCellSafe(Building_Bed bed, int slotIndex)
        {
            if (bed == null) return IntVec3.Invalid;

            try
            {
                int slots = bed.SleepingSlotsCount;
                if (slots <= 0) return bed.Position;
                if (slotIndex < 0) slotIndex = 0;
                if (slotIndex >= slots) slotIndex = slots - 1;

                return bed.GetSleepingSlotPos(slotIndex);
            }
            catch
            {
                return bed.Position;
            }
        }
    }
}
