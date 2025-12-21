using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public class WorkGiver_CompanionWait : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            Map map = pawn?.Map;
            if (map == null) yield break;

            List<Thing> spots = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            if (spots == null) yield break;

            for (int i = 0; i < spots.Count; i++)
                yield return spots[i];
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.Map == null) return true;

            if (!CompanionshipPawnUtility.IsEligibleCompanionWorker(pawn))
                return true;

            CompanionshipVisitorTracker tracker = pawn.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return true;

            IReadOnlyList<CompanionshipVisitorTracker.VisitorRecord> records = tracker.Records;
            if (records == null || records.Count == 0) return true;

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null) continue;

                if (r.wantsDate && r.state == CompanionshipVisitorTracker.DateState.WaitingNearSpot && r.claimedBy == null)
                    return false;
            }

            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return TryFindAssignment(pawn, t, out _, out _, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!TryFindAssignment(pawn, t, out Building spot, out Pawn visitor, out Building_Bed bed))
                return null;

            // IMPORTANT:
            // JobDriver_CompanionGreetAndEscortToBed expects:
            //   TargetIndex.A = visitor
            //   TargetIndex.B = bed
            // Spot is optional; we store it in C for convenience/future use.
            Job job = JobMaker.MakeJob(CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed, visitor, bed);
            job.targetC = spot;
            job.count = 1;

            int expiry = CompanionshipTuning.CompanionGreetAndEscortJobExpiryTicks;
            if (expiry > 0) job.expiryInterval = expiry;

            job.checkOverrideOnExpire = true;
            return job;
        }

        private bool TryFindAssignment(Pawn companion, Thing t, out Building spot, out Pawn visitor, out Building_Bed bed)
        {
            spot = null;
            visitor = null;
            bed = null;

            if (companion == null || companion.Map == null) return false;

            spot = t as Building;
            if (spot == null || spot.Destroyed) return false;
            if (spot.def != CompanionshipDefOf.Companionship_CompanionSpot) return false;

            if (!CompanionshipPawnUtility.IsEligibleCompanionWorker(companion)) return false;

            CompanionshipVisitorTracker tracker = companion.Map.GetComponent<CompanionshipVisitorTracker>();
            if (tracker == null) return false;

            IReadOnlyList<CompanionshipVisitorTracker.VisitorRecord> records = tracker.Records;
            if (records == null || records.Count == 0) return false;

            float bestScore = float.MinValue;
            Pawn bestVisitor = null;
            Building_Bed bestBed = null;

            for (int i = 0; i < records.Count; i++)
            {
                CompanionshipVisitorTracker.VisitorRecord r = records[i];
                if (r == null) continue;

                if (!r.wantsDate) continue;
                if (r.state != CompanionshipVisitorTracker.DateState.WaitingNearSpot) continue;
                if (r.claimedBy != null) continue;

                Pawn v = r.pawn;
                if (v == null) continue;
                if (v.DestroyedOrNull() || v.Dead) continue;
                if (!v.Spawned) continue;
                if (v.Map != companion.Map) continue;

                if (!v.RaceProps.Humanlike) continue;
                if (v.Downed || v.InMentalState) continue;
                if (v.HostileTo(Faction.OfPlayer)) continue;

                // Don’t pick someone already in a session (belt + suspenders).
                if (tracker.HasActiveSession(v)) continue;

                // Must be romance-compatible per our existing utility logic.
                if (!CompanionshipPawnUtility.IsRomancePreferenceCompatible(companion, v))
                    continue;

                // Must have a bed that works for BOTH.
                Building_Bed candidateBed = CompanionshipBedUtility.FindAvailableCompanionBed(companion.Map, companion, v);
                if (candidateBed == null) continue;

                // Light pre-checks so we don't spam jobs that fail reservations instantly.
                if (!companion.CanReserve(v)) continue;
                if (!companion.CanReserve(candidateBed)) continue;

                // Prefer closer visitors; slight bias toward being near the spot.
                float score = -companion.Position.DistanceToSquared(v.Position);
                score += -0.10f * v.Position.DistanceToSquared(spot.Position);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVisitor = v;
                    bestBed = candidateBed;
                }
            }

            visitor = bestVisitor;
            bed = bestBed;
            return visitor != null && bed != null;
        }
    }
}
