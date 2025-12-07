using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    /// <summary>
    /// Work giver that assigns Companion pawns to perform dates with visiting clients.
    /// 
    /// Scanner logic:
    /// - Scans all humanlike pawns on the map as potential clients.
    /// - Filters to visitors/guests that:
    ///     * Have CompVisitorCompanionship,
    ///     * Desire companionship,
    ///     * Are actively waiting for a companion at a Companion Spot.
    /// - For each valid client, finds a Companion who:
    ///     * Is a valid worker (CompanionshipUtility.IsPotentialCompanion),
    ///     * Has CompCompanionship and can start a date now,
    ///     * Can reach and reserve the client and a CompanionBed.
    /// 
    /// This keeps the core "who works / who gets service" logic centralized
    /// while remaining modular and easy to extend later.
    /// </summary>
    public class WorkGiver_CompanionDate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        /// <summary>
        /// We scan pawns, not buildings.
        /// </summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null)
                yield break;

            // Iterate all spawned humanlike pawns on the map.
            List<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn other = allPawns[i];
                if (other == null || other == pawn)
                    continue;

                if (!other.RaceProps.Humanlike)
                    continue;

                yield return other;
            }
        }

        /// <summary>
        /// High-level "can we do a date with this specific client right now?"
        /// This is called per client from PotentialWorkThingsGlobal.
        /// </summary>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // Worker sanity.
            if (!CompanionshipUtility.IsPotentialCompanion(pawn))
                return false;

            Pawn client = t as Pawn;
            if (client == null)
                return false;

            // Client must be a valid non-player humanlike.
            if (!CompanionshipUtility.IsPotentialClient(client))
                return false;

            if (!CompanionshipUtility.HasActiveCompanionshipNeed(client))
                return false;

            // Visitor-side comp: are they actually interested and waiting?
            CompVisitorCompanionship visitorComp = client.TryGetComp<CompVisitorCompanionship>();
            if (visitorComp == null)
                return false;

            // Must actually desire companionship.
            if (!visitorComp.DesiresCompanionship)
                return false;

            // NEW: Only consider clients who are *actively waiting* at a Companion Spot.
            // This flag is set by JobDriver_WaitForCompanionDate once the pawn reaches the spot
            // and starts the actual waiting toil. While they are pathing across the map, this is false.
            if (!visitorComp.IsWaitingForCompanion && !forced)
                return false;

            // We still require a Companion Spot to exist and be reasonably close,
            // as a guard against weird edge cases (destroyed spot, teleports, etc.).
            Building_CompanionSpot spot = CompanionshipUtility.FindNearestCompanionSpot(client);
            if (spot == null)
                return false;

            // Require them to be within 2 tiles (distance^2 <= 4) of the spot.
            float distSqToSpot = (client.Position - spot.Position).LengthHorizontalSquared;
            if (distSqToSpot > 4f && !forced)
                return false;

            // Companion-side comp: respect daily limits, etc.
            CompCompanionship comp = pawn.TryGetComp<CompCompanionship>();
            if (comp == null)
                return false;

            if (!comp.CanStartDateNow(pawn) && !forced)
                return false;

            // Need a valid CompanionBed.
            Building_Bed bed = FindBestCompanionBed(pawn, client);
            if (bed == null)
                return false;

            // Final reservation checks.
            if (!pawn.CanReserve(client, 1, -1, null, false))
                return false;

            if (!pawn.CanReserve(bed, 1, -1, null, false))
                return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null)
                return null;

            // Double-check that the job is still valid.
            if (!HasJobOnThing(pawn, client, forced))
                return null;

            Building_Bed bed = FindBestCompanionBed(pawn, client);
            if (bed == null)
                return null;

            // Main Companion-side date job: targetA = client, targetB = bed.
            Job job = JobMaker.MakeJob(CompanionshipDefOf.DoCompanionDate, client, bed);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.ignoreJoyTimeAssignment = true;

            return job;
        }

        /// <summary>
        /// Find the best CompanionBed for this date, owned by the player, reachable,
        /// and either unowned or owned by this Companion.
        /// Picks the closest valid bed to the worker.
        /// </summary>
        private Building_Bed FindBestCompanionBed(Pawn companion, Pawn client)
        {
            if (companion == null || companion.Map == null)
                return null;

            Map map = companion.Map;
            Building_Bed bestBed = null;
            float bestDistSq = float.MaxValue;

            foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (bed == null || !bed.Spawned)
                    continue;

                // Must be our special CompanionBed.
                if (bed.def != CompanionshipDefOf.CompanionBed)
                    continue;

                // Must belong to the player and not be prisoner-only.
                if (bed.Faction != Faction.OfPlayerSilentFail)
                    continue;

                if (bed.ForPrisoners)
                    continue;

                // Ownership rules: unowned or owned by this companion.
                var owners = bed.OwnersForReading;
                if (owners != null && owners.Count > 0 && !owners.Contains(companion))
                    continue;

                // Bed must have a free sleeping slot.
                if (!bed.AnyUnoccupiedSleepingSlot)
                    continue;

                // Reachability and reservation checks for the worker.
                if (!companion.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                    continue;

                float distSq = (companion.Position - bed.Position).LengthHorizontalSquared;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestBed = bed;
                }
            }

            return bestBed;
        }
    }
}
