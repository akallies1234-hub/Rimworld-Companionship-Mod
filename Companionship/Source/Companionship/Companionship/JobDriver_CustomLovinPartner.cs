using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class JobDriver_CustomLovinPartner : JobDriver
    {
        private Pawn Initiator => job.targetA.Pawn;
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

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Initiator == null || !Initiator.Spawned);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            yield return Toils_Goto.GotoCell(GetSlotCellSafe(Bed, 1), PathEndMode.OnCell);

            Toil lovin = ToilMaker.MakeToil("Companionship_CustomLovinPartner");
            lovin.defaultCompleteMode = ToilCompleteMode.Delay;
            lovin.defaultDuration = LovinDuration;

            lovin.initAction = () =>
            {
                TryHolsterPrimary(pawn);

                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;
            };

            lovin.tickAction = () =>
            {
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                if (Initiator != null && Initiator.Spawned)
                    pawn.rotationTracker.FaceTarget(Initiator);
            };

            lovin.AddFinishAction(() =>
            {
                TryReequipPrimary(pawn);
            });

            yield return lovin;
        }

        private void TryHolsterPrimary(Pawn p)
        {
            if (didHolster) return;
            if (p?.equipment == null) return;

            ThingWithComps primary = p.equipment.Primary;
            if (primary == null) return;

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
