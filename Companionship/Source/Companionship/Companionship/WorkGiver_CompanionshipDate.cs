using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class WorkGiver_CompanionshipDate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return true;
            if (!pawn.IsColonist) return true;

            MapComponent_Companionship mc = pawn.Map.GetComponent<MapComponent_Companionship>();
            if (mc == null) return true;

            // WaitingVisitors is IEnumerable<Pawn> in this project.
            IEnumerable<Pawn> waiting = mc.WaitingVisitors;
            bool anyWaiting = false;
            if (waiting != null)
            {
                foreach (Pawn p in waiting)
                {
                    if (p != null)
                    {
                        anyWaiting = true;
                        break;
                    }
                }
            }
            if (!anyWaiting) return true;

            // No beds? no point scanning guests.
            ThingDef bedDef = DefDatabase<ThingDef>.GetNamedSilentFail("Companionship_CompanionBed");
            if (bedDef == null) return true;

            return pawn.Map.listerThings.ThingsOfDef(bedDef).Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) yield break;

            MapComponent_Companionship mc = pawn.Map.GetComponent<MapComponent_Companionship>();
            if (mc == null) yield break;

            IEnumerable<Pawn> waiting = mc.WaitingVisitors;
            if (waiting == null) yield break;

            // IMPORTANT: this must match what the MapComponent assigns.
            JobDef waitDef = CompanionshipDefOf.Companionship_WaitAtCompanionSpot;

            foreach (Pawn guest in waiting)
            {
                if (guest == null) continue;
                if (!guest.Spawned) continue;
                if (guest.Dead || guest.Downed) continue;

                if (guest.CurJobDef == waitDef)
                    yield return guest;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (guest == null) return false;

            // Must still be waiting at the companion spot.
            if (guest.CurJobDef != CompanionshipDefOf.Companionship_WaitAtCompanionSpot) return false;
            if (!CompanionshipDateUtility.IsValidDateGuest(guest)) return false;

            // Worker must be able to reserve the guest.
            if (!pawn.CanReserve(guest, 1, -1, null, forced)) return false;

            // Must be able to find and reserve a bed.
            Building_Bed bed;
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out bed)) return false;
            if (bed == null) return false;

            if (!pawn.CanReserve(bed, 1, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (guest == null) return null;

            JobDef dateDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_Date");
            if (dateDef == null) return null;

            Building_Bed bed;
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out bed)) return null;
            if (bed == null) return null;

            // A = guest, B = bed
            Job job = JobMaker.MakeJob(dateDef, guest, bed);

            // Short expiry prevents stale jobs from looping if conditions change quickly.
            job.expiryInterval = 300; // ~5 seconds
            return job;
        }
    }
}
