using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class WorkGiver_CompanionDate : WorkGiver_Scanner
    {
        // How far from the Companion Spot a client can be and still count as "waiting".
        // 10 tiles is a generous radius that should feel good in-game.
        private const float MaxDistanceFromSpotSquared = 10f * 10f;

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override bool Prioritized => false;

        /// <summary>
        /// We scan all humanlike pawns on the map as potential clients.
        /// The heavy filtering happens in HasJobOnThing.
        /// </summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                yield break;
            }

            foreach (Pawn otherPawn in map.mapPawns.AllPawnsSpawned)
            {
                if (otherPawn != pawn && otherPawn.RaceProps.Humanlike)
                {
                    yield return otherPawn;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // Basic sanity checks
            if (pawn == null || pawn.Dead || pawn.Downed)
            {
                return false;
            }

            Pawn client = t as Pawn;
            if (client == null || client.Dead || client.Downed || client == pawn)
            {
                return false;
            }

            // Is this pawn even allowed to be a companion?
            if (!CompanionshipUtility.IsPotentialCompanion(pawn))
            {
                return false;
            }

            // Is this pawn even a valid client (visitor / non-player humanlike)?
            if (!CompanionshipUtility.IsPotentialClient(client))
            {
                return false;
            }

            // Future hook: needs/traits/etc.
            if (!CompanionshipUtility.HasActiveCompanionshipNeed(client))
            {
                return false;
            }

            // We must be able to reserve the client.
            if (!pawn.CanReserve(client, 1, -1, null, forced))
            {
                return false;
            }

            // Visitor companionship state
            CompVisitorCompanionship visitorComp = client.TryGetComp<CompVisitorCompanionship>();
            if (visitorComp == null)
            {
                return false;
            }

            // They must have rolled desire AND be in the explicit "waiting" state.
            if (!visitorComp.DesiresCompanionship || !visitorComp.IsWaitingForCompanion)
            {
                return false;
            }

            // Make sure they are reasonably close to a Companion Spot.
            Building_CompanionSpot spot = CompanionshipUtility.FindNearestCompanionSpot(client);
            if (spot == null)
            {
                return false;
            }

            float distanceSquared = (client.Position - spot.Position).LengthHorizontalSquared;
            if (!forced && distanceSquared > MaxDistanceFromSpotSquared)
            {
                // They might have wandered off; don't treat them as an active client anymore.
                return false;
            }

            // Companion progression / daily limit
            CompCompanionship comp = pawn.TryGetComp<CompCompanionship>();
            if (comp == null)
            {
                return false;
            }

            if (!forced && !comp.CanStartDateNow(pawn))
            {
                return false;
            }

            // Find a suitable companion bed for this pair.
            Building_Bed bed = FindBestCompanionBed(pawn, client);
            if (bed == null)
            {
                return false;
            }

            if (!pawn.CanReserve(bed, 1, -1, null, forced))
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null)
            {
                return null;
            }

            Building_Bed bed = FindBestCompanionBed(pawn, client);
            if (bed == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(CompanionshipDefOf.DoCompanionDate, client, bed);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.ignoreJoyTimeAssignment = true;

            return job;
        }

        /// <summary>
        /// Finds the best CompanionBed for this companion-client pair.
        /// Currently: any colonist-owned CompanionBed with a free slot that both can reach,
        /// preferring the closest to the companion.
        /// </summary>
        private Building_Bed FindBestCompanionBed(Pawn companion, Pawn client)
        {
            Map map = companion.Map;
            if (map == null)
            {
                return null;
            }

            IEnumerable<Building_Bed> candidateBeds = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b =>
                    b.def == CompanionshipDefOf.CompanionBed && // must be the special bed
                    !b.ForPrisoners &&                          // not a prisoner bed
                    b.Faction == companion.Faction &&           // owned by the colony
                    HasFreeSlotFor(b, companion) &&             // at least one free spot for companion
                    companion.CanReach(b, PathEndMode.OnCell, Danger.Unspecified) &&
                    client.CanReach(b, PathEndMode.OnCell, Danger.Unspecified));

            Building_Bed bestBed = null;
            float bestDist = float.MaxValue;

            foreach (Building_Bed bed in candidateBeds)
            {
                float dist = (bed.Position - companion.Position).LengthHorizontalSquared;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestBed = bed;
                }
            }

            return bestBed;
        }

        /// <summary>
        /// True if the bed has an unoccupied sleeping slot.
        /// For now we ignore ownership and just require a free slot.
        /// If we later want stricter ownership behavior, we can query CompAssignableToPawn.
        /// </summary>
        private bool HasFreeSlotFor(Building_Bed bed, Pawn pawn)
        {
            if (bed == null)
            {
                return false;
            }

            // Simple check: fewer occupants than sleeping slots.
            return bed.CurOccupants.Count() < bed.SleepingSlotsCount;
        }
    }
}
