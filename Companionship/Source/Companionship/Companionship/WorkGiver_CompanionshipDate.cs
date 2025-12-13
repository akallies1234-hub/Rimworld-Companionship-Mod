using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class WorkGiver_CompanionshipDate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        // CRITICAL: we are scanning pawns (the guests), not buildings.
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return true;
            if (!pawn.IsColonist) return true;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            JobDef dateDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_Date");
            if (waitDef == null || dateDef == null) return true;

            // No beds? no point scanning guests.
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

            // In 1.6 this may be IEnumerable<Pawn> (not List / IReadOnlyList). Just foreach it.
            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == null) continue;
                if (p.Faction == Faction.OfPlayer) continue;
                if (!p.Spawned) continue;
                if (p.Dead || p.Downed) continue;

                if (p.CurJobDef == waitDef)
                    yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (guest == null) return false;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            JobDef dateDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_Date");
            if (waitDef == null || dateDef == null) return false;

            // Guest must still be in the waiting phase
            if (guest.CurJobDef != waitDef) return false;
            if (!CompanionshipDateUtility.IsValidDateGuest(guest)) return false;

            // Optional: prevent repeated rapid attempts if something is failing
            if (CompanionshipDateUtility.IsOnDateCooldown(pawn)) return false;

            // Worker must be able to reserve the guest
            if (!pawn.CanReserve(guest, 1, -1, null, forced)) return false;

            // We need the companion spot the guest is waiting at (TargetIndex.A of their wait job)
            if (guest.CurJob == null) return false;
            Thing spotThing = guest.CurJob.GetTarget(TargetIndex.A).Thing;
            if (spotThing == null) return false;

            // Must be able to find and reserve a bed (2+ slots, empty, reachable, reservable)
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out Building_Bed bed)) return false;
            if (bed == null) return false;

            // Reserve ALL sleeping slots so nobody else jumps in
            int slots = bed.SleepingSlotsCount;
            if (slots < 2) return false;

            if (!pawn.CanReserve(bed, slots, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (guest == null) return null;

            JobDef dateDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_Date");
            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            if (dateDef == null || waitDef == null) return null;

            if (guest.CurJobDef != waitDef) return null;
            if (guest.CurJob == null) return null;

            Thing spotThing = guest.CurJob.GetTarget(TargetIndex.A).Thing;
            if (spotThing == null) return null;

            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out Building_Bed bed)) return null;
            if (bed == null) return null;

            // IMPORTANT: set targets to match JobDriver_CompanionshipDate expectations:
            // A = guest, B = companion spot, C = bed
            Job job = JobMaker.MakeJob(dateDef);
            job.SetTarget(TargetIndex.A, guest);
            job.SetTarget(TargetIndex.B, spotThing);
            job.SetTarget(TargetIndex.C, bed);

            // Short expiry prevents stale looping if conditions change fast
            job.expiryInterval = 600; // ~10 seconds
            return job;
        }
    }
}
