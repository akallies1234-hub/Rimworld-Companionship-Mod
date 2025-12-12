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

            if (CompanionshipDateUtility.IsOnDateCooldown(pawn)) return true;

            ThingDef bedDef = DefDatabase<ThingDef>.GetNamedSilentFail("Companionship_CompanionBed");
            if (bedDef == null) return true;

            return pawn.Map.listerThings.ThingsOfDef(bedDef).Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) yield break;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            if (waitDef == null) yield break;

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null) continue;
                if (p.Faction == Faction.OfPlayer) continue;
                if (!p.Spawned) continue;
                if (p.Dead || p.Downed) continue;
                if (CompanionshipDateUtility.IsOnDateCooldown(p)) continue;

                if (p.CurJobDef == waitDef)
                    yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn guest = t as Pawn;
            if (guest == null) return false;

            if (CompanionshipDateUtility.IsOnDateCooldown(pawn)) return false;
            if (!CompanionshipDateUtility.IsValidDateGuest(guest)) return false;

            JobDef waitDef = DefDatabase<JobDef>.GetNamedSilentFail("Companionship_WaitAtSpot");
            if (waitDef == null) return false;

            // Guest must still be waiting.
            if (guest.CurJobDef != waitDef) return false;

            // Worker must be able to reserve the guest (this prevents multiple colonists racing).
            if (!pawn.CanReserve(guest, 1, -1, null, forced)) return false;

            // Must be able to find an empty bed (do NOT reserve it here; lovin will handle it).
            Building_Bed bed;
            if (!CompanionshipDateUtility.TryFindAvailableCompanionBed(pawn, guest, out bed)) return false;
            if (bed == null) return false;

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

            // Set a short cooldown immediately to prevent same-tick thrash if the job fails instantly.
            // (If it succeeds, the JobDriver will extend cooldown again.)
            CompanionshipDateUtility.SetDateCooldown(pawn, 600);  // 10 seconds
            CompanionshipDateUtility.SetDateCooldown(guest, 600); // 10 seconds

            Job job = JobMaker.MakeJob(dateDef, guest, bed);

            // Keep this from going stale instantly; but it shouldn't loop now due to cooldown + simpler reservations.
            job.expiryInterval = 2000;
            return job;
        }
    }
}
