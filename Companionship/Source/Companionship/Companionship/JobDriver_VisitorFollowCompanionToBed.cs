using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor-side job during the "escort to bed" stage.
    ///
    /// Goals:
    /// - Go directly to the visitor slot (slot 1) to avoid blocking the companion (slot 0)
    /// - Stay put until the companion hands off to CustomLovin
    /// - Present as "in bed" (under covers) rather than standing on top of the bed
    /// - Holster weapons for immersion/safety
    /// </summary>
    public class JobDriver_VisitorFollowCompanionToBed : JobDriver
    {
        private Pawn Companion => job?.targetA.Pawn;
        private Building_Bed Bed => job?.targetB.Thing as Building_Bed;

        // Weapon holstering (visitor)
        private ThingWithComps holsteredPrimary;
        private bool didHolster;
        private bool endHandled;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holsteredPrimary, "holsteredPrimary");
            Scribe_Values.Look(ref didHolster, "didHolster");
            Scribe_Values.Look(ref endHandled, "endHandled");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn == null || pawn.Map == null);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            AddFinishAction(HandleJobEnd);

            // Go to slot 1 (visitor slot). Do NOT "GotoThing(bed)" first, as that can end on slot 0 and block the companion.
            Toil gotoSlot1 = ToilMaker.MakeToil("Companionship_VisitorGotoSlot1");
            gotoSlot1.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            gotoSlot1.initAction = () =>
            {
                if (Bed == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                if (!slot1.IsValid) slot1 = Bed.Position;

                pawn.pather?.StartPath(slot1, PathEndMode.OnCell);
            };
            yield return gotoSlot1;

            // Wait in bed until the companion transitions to the next stage.
            // We keep posture as LayingInBed to get the under-covers rendering.
            Toil wait = ToilMaker.MakeToil("Companionship_VisitorWaitInBed");
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = CompanionshipTuning.VisitorWaitAtBedDurationTicks;

            wait.initAction = () =>
            {
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;
                TryHolsterPrimary(pawn);
            };

            wait.tickAction = () =>
            {
                pawn.pather?.StopDead();

                // Only "lie" when actually on the sleeping slot cell.
                bool onSlot = false;
                if (Bed != null)
                {
                    IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                    onSlot = slot1.IsValid && pawn.Position == slot1;
                }

                pawn.jobs.posture = onSlot ? PawnPosture.LayingInBed : PawnPosture.Standing;

                if (Companion != null && Companion.Spawned)
                    pawn.rotationTracker?.FaceTarget(Companion);

                // If bumped/teleported off the slot, re-path back.
                if (Bed != null)
                {
                    IntVec3 slot1 = GetSlotCellSafe(Bed, 1);
                    if (slot1.IsValid && pawn.Position != slot1 && pawn.pather != null && !pawn.pather.Moving)
                    {
                        pawn.jobs.posture = PawnPosture.Standing;
                        pawn.pather.StartPath(slot1, PathEndMode.OnCell);
                    }
                }
            };

            yield return wait;
        }

        private void HandleJobEnd(JobCondition condition)
        {
            if (endHandled) return;
            endHandled = true;

            TryReequipPrimary(pawn);
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
                    p.equipment.AddEquipment(holsteredPrimary);
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
