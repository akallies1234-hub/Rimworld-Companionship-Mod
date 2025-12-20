using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Companion work triggers only when a visitor is actually waiting near the Companion Spot.
    /// It assigns a job to greet a specific visitor and escort them to a Companion Bed.
    /// </summary>
    public class WorkGiver_CompanionWait : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(CompanionshipDefOf.Companionship_CompanionSpot);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) yield break;

            List<Thing> list = pawn.Map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            for (int i = 0; i < list.Count; i++)
                yield return list[i];
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return true;

            Thing spot = GetSpot(pawn.Map);
            if (spot == null) return true;

            var tracker = pawn.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return true;

            // Skip if no eligible visitor is currently waiting near spot.
            return tracker.FindEligibleWaitingVisitorNearSpot(spot, pawn) == null;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return false;
            if (t == null || t.def != CompanionshipDefOf.Companionship_CompanionSpot) return false;

            if (pawn.Faction != Faction.OfPlayer) return false;
            if (t.IsForbidden(pawn)) return false;

            var tracker = pawn.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return false;

            Pawn visitor = tracker.FindEligibleWaitingVisitorNearSpot(t, pawn);
            if (visitor == null) return false;

            Building_Bed bed = FindAvailableCompanionBed(pawn, visitor);
            if (bed == null) return false;

            // Soft pre-check
            if (!pawn.CanReserve(visitor, 1, -1, null, forced)) return false;
            if (!pawn.CanReserve(bed, 1, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var tracker = pawn.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return null;

            Pawn visitor = tracker.FindEligibleWaitingVisitorNearSpot(t, pawn);
            if (visitor == null) return null;

            Building_Bed bed = FindAvailableCompanionBed(pawn, visitor);
            if (bed == null) return null;

            // Claim at assignment time (NOT in HasJobOnThing).
            if (!tracker.TryClaimVisitorForDate(visitor, pawn))
                return null;

            Job job = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed, visitor, bed);
            job.ignoreForbidden = true;
            return job;
        }

        private static Thing GetSpot(Map map)
        {
            List<Thing> list = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            return (list != null && list.Count > 0) ? list[0] : null;
        }

        private static Building_Bed FindAvailableCompanionBed(Pawn companion, Pawn visitor)
        {
            Map map = companion.Map;
            if (map == null) return null;

            List<Thing> beds = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionBed);
            if (beds == null || beds.Count == 0) return null;

            Building_Bed best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i] as Building_Bed;
                if (bed == null || !bed.Spawned) continue;

                // For this early step, just require "unoccupied"
                if (bed.AnyOccupants) continue;

                if (bed.IsForbidden(companion)) continue;
                if (!companion.CanReserve(bed)) continue;

                int d = visitor != null ? bed.Position.DistanceToSquared(visitor.Position) : bed.Position.DistanceToSquared(companion.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = bed;
                }
            }

            return best;
        }
    }
}
