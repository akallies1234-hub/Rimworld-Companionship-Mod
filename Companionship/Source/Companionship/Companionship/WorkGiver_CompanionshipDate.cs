using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class WorkGiver_CompanionshipDate : WorkGiver_Scanner
    {
        // IMPORTANT: This tells RimWorld we are scanning PAWNS, not buildings/items.
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // Optional: helps RimWorld prefer closer targets (and generally behave better).
        public override bool Prioritized => true;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return true;
            if (!pawn.IsColonist) return true;
            if (pawn.Dead || pawn.Downed) return true;
            if (pawn.workSettings == null) return true;

            // No companion beds placed? no point scanning guests.
            ThingDef bedDef = DefDatabase<ThingDef>.GetNamedSilentFail("Companionship_CompanionBed");
            if (bedDef == null) return true;

            List<Thing> beds = pawn.Map.listerThings.ThingsOfDef(bedDef);
            return beds == null || beds.Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) yield break;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            if (waitDef == null) yield break;

            // RimWorld 1.6: AllPawnsSpawned is IReadOnlyList<Pawn>
            var pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null) continue;
                if (p.Faction == null) continue;
                if (p.Faction == Faction.OfPlayer) continue;
                if (!p.Spawned) continue;
                if (p.Dead || p.Downed) continue;

                // Only guests waiting at our spot
                if (p.CurJobDef == waitDef)
                    yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (pawn == null || guest == null) return false;
            if (pawn.Map == null) return false;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            if (waitDef == null) return false;

            // Guest must still be in the waiting phase
            if (guest.CurJobDef != waitDef) return false;
            if (!CompanionshipDateUtility.IsValidDateGuest(guest)) return false;

            // Can reach + reserve guest
            if (!pawn.CanReserveAndReach(guest, PathEndMode.Touch, Danger.Some, 1, -1, null, forced)) return false;

            // Must find an available companion bed
            Building_Bed bed;
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out bed)) return false;
            if (bed == null) return false;

            // Reserve bed too
            if (!pawn.CanReserveAndReach(bed, PathEndMode.Touch, Danger.Some, 1, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (pawn == null || guest == null) return null;

            JobDef dateDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_Date");
            if (dateDef == null) return null;

            Building_Bed bed;
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out bed)) return null;
            if (bed == null) return null;

            // IMPORTANT: This job uses A=guest, B=bed
            Job job = JobMaker.MakeJob(dateDef, guest, bed);

            // Prevents stale spam if conditions change quickly
            job.expiryInterval = 600; // ~10 seconds
            job.checkOverrideOnExpire = true;

            return job;
        }
    }
}
