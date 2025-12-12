using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Scans for visitors currently waiting at the Companion Spot and assigns a date job.
    /// IMPORTANT: only triggers when the visitor is actually "in the waiting phase"
    /// (within radius of the companion spot).
    /// </summary>
    public class WorkGiver_CompanionshipDate : WorkGiver_Scanner
    {
        private const float ReadyRadius = 7f;

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null) return Enumerable.Empty<Thing>();

            var comp = pawn.Map.GetComponent<MapComponent_Companionship>();
            if (comp == null) return Enumerable.Empty<Thing>();

            return comp.WaitingVisitors.Cast<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn == null || t == null) return false;
            if (pawn.Map == null) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            if (!pawn.IsColonistPlayerControlled) return false;

            Pawn visitor = t as Pawn;
            if (visitor == null) return false;

            var comp = pawn.Map.GetComponent<MapComponent_Companionship>();
            if (comp == null || !comp.IsWaiting(visitor)) return false;

            if (visitor.Dead || visitor.Downed) return false;
            if (visitor.HostileTo(Faction.OfPlayer)) return false;

            // MUST be on the waiting job
            if (visitor.CurJobDef != CompanionshipDefOf.Companionship_WaitAtCompanionSpot) return false;

            // MUST be close enough to the spot (this prevents early triggering while they’re still walking in)
            if (!comp.TryGetCompanionSpot(out Thing spot)) return false;
            if (!visitor.Position.InHorDistOf(spot.Position, ReadyRadius)) return false;

            // Optional but recommended: don't trigger while the visitor is still actively pathing
            if (visitor.pather != null && visitor.pather.Moving) return false;

            if (!pawn.CanReserve(visitor, 1, -1, null, forced)) return false;

            if (!TryFindAvailableCompanionBed(pawn, out Building_Bed bed)) return false;
            if (!pawn.CanReserve(bed, 1, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn visitor = t as Pawn;
            if (visitor == null) return null;

            if (!TryFindAvailableCompanionBed(pawn, out Building_Bed bed)) return null;

            // TargetA = visitor, TargetB = bed
            Job job = JobMaker.MakeJob(CompanionshipDefOf.Companionship_Date, visitor, bed);

            // Store lovin duration (1–3 hours) in job.count so it persists
            job.count = Rand.RangeInclusive(2500, 7500);

            return job;
        }


        private bool TryFindAvailableCompanionBed(Pawn pawn, out Building_Bed bed)
        {
            bed = null;
            if (pawn?.Map == null) return false;

            List<Thing> beds = pawn.Map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionBed);
            if (beds == null || beds.Count == 0) return false;

            foreach (Thing t in beds)
            {
                var b = t as Building_Bed;
                if (b == null) continue;
                if (b.Destroyed) continue;
                if (b.IsForbidden(pawn)) continue;

                if (b.CurOccupants != null && b.CurOccupants.Any()) continue;
                if (!pawn.CanReach(b, PathEndMode.Touch, Danger.Some)) continue;

                bed = b;
                return true;
            }

            return false;
        }
    }
}
