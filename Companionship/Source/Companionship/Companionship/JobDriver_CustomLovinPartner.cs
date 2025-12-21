using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Visitor-side "partner" job during custom lovin.
    /// Purpose: keep the visitor at the bed slot while the companion runs the initiator job.
    /// The visitor does NOT gain Social XP here (only companions should).
    /// </summary>
    public class JobDriver_CustomLovinPartner : JobDriver
    {
        private Pawn Initiator => job?.targetA.Pawn;
        private Building_Bed Bed => job?.targetB.Thing as Building_Bed;

        // Weapon holstering (visitor safety/immersion; mirrors initiator logic but is independent)
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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Companion reserves bed + visitor; visitor doesn't reserve anything to avoid reservation deadlocks.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn == null || pawn.Map == null);
            this.FailOn(() => Initiator == null || Initiator.DestroyedOrNull());
            this.FailOnDestroyedOrNull(TargetIndex.B);

            AddFinishAction(HandleJobEnd);

            // Go to bed slot 1 (visitor side)
            yield return Toils_Goto.GotoCell(GetSlotCellSafe(Bed, 1), PathEndMode.OnCell);

            // Wait for the lovin duration.
            Toil wait = ToilMaker.MakeToil();
            wait.initAction = () =>
            {
                TryHolsterPrimary(pawn);

                // Present as "in bed" for rendering.
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;
            };

            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = CompanionshipTuning.LovinDurationTicks;

            // If the initiator stops doing the initiator job, stop waiting (avoid hanging).
            wait.tickAction = () =>
            {
                // Keep them visually in bed.
                pawn.pather?.StopDead();
                pawn.jobs.posture = PawnPosture.LayingInBed;

                if (Initiator == null || Initiator.DestroyedOrNull())
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (Initiator.CurJobDef != CompanionshipDefOf.Companionship_CustomLovin)
                {
                    EndJobWith(JobCondition.InterruptForced);
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
