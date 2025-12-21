using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public static class CompanionshipBedUtility
    {
        /// <summary>
        /// Finds an available Companion Bed for a date session.
        /// This is NOT a general sleeping-bed finder.
        /// </summary>
        public static Building_Bed FindAvailableCompanionBed(Map map, Pawn companion, Pawn visitor)
        {
            if (map == null || companion == null || visitor == null) return null;
            if (companion.DestroyedOrNull() || visitor.DestroyedOrNull()) return null;
            if (!companion.Spawned || !visitor.Spawned) return null;
            if (companion.Map != map || visitor.Map != map) return null;

            List<Thing> beds = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionBed);
            if (beds == null || beds.Count == 0) return null;

            Building_Bed best = null;
            int bestDistSq = int.MaxValue;

            for (int i = 0; i < beds.Count; i++)
            {
                if (!(beds[i] is Building_Bed bed)) continue;

                if (!IsBedUsableForSession(bed, companion, visitor))
                    continue;

                int d = companion.Position.DistanceToSquared(bed.Position);
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = bed;
                }
            }

            return best;
        }

        private static bool IsBedUsableForSession(Building_Bed bed, Pawn companion, Pawn visitor)
        {
            if (bed == null) return false;
            if (bed.Destroyed || !bed.Spawned) return false;
            if (bed.IsBurning()) return false;

            if (bed.Map != companion.Map || bed.Map != visitor.Map) return false;

            if (bed.SleepingSlotsCount < 2) return false;

            if (!companion.CanReach(bed, PathEndMode.Touch, Danger.Some)) return false;
            if (!visitor.CanReach(bed, PathEndMode.Touch, Danger.Some)) return false;

            if (!companion.CanReserve(bed)) return false;
            if (!visitor.CanReserve(bed)) return false;

            for (int s = 0; s < bed.SleepingSlotsCount; s++)
            {
                Pawn occ = bed.GetCurOccupant(s);
                if (occ != null) return false;
            }

            return true;
        }
    }
}
