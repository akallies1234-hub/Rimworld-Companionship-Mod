using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    /// <summary>
    /// Central place for high-level "who counts as what" logic:
    /// - Who can be a Companion (worker pawn)
    /// - Who can be a Client (visitor pawn)
    /// - Helper methods for finding Companion Spots and available Companions
    /// </summary>
    public static class CompanionshipUtility
    {
        /// <summary>
        /// Is this pawn allowed to work as a Companion?
        /// </summary>
        public static bool IsPotentialCompanion(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return false;

            if (!pawn.RaceProps.Humanlike)
                return false;

            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return false;

            if (pawn.IsPrisonerOfColony)
                return false;

            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
                return false;

            if (!pawn.workSettings.WorkIsActive(CompanionshipDefOf.Companion))
                return false;

            return true;
        }

        /// <summary>
        /// Is this pawn a valid visitor/client (non-colonist humanlike)?
        /// </summary>
        public static bool IsPotentialClient(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return false;

            if (!pawn.RaceProps.Humanlike)
                return false;

            if (pawn.Faction == null || pawn.Faction == Faction.OfPlayerSilentFail)
                return false;

            return true;
        }

        /// <summary>
        /// Placeholder hook for more complex "do they even want this service?" logic.
        /// Right now, any living humanlike is allowed.
        /// </summary>
        public static bool HasActiveCompanionshipNeed(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;

            if (!pawn.RaceProps.Humanlike)
                return false;

            // Placeholder: no additional gating yet.
            return true;
        }

        /// <summary>
        /// Find the nearest Companion Spot that this pawn can reach.
        /// Returns null if none exist.
        /// </summary>
        public static Building_CompanionSpot FindNearestCompanionSpot(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
                return null;

            Building_CompanionSpot bestSpot = null;
            float bestDistSq = float.MaxValue;

            // AllBuildingsColonistOfClass returns an IEnumerable; we just iterate it.
            foreach (Building_CompanionSpot spot in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_CompanionSpot>())
            {
                if (spot == null || !spot.Spawned)
                    continue;

                float distSq = (pawn.Position - spot.Position).LengthHorizontalSquared;
                if (distSq < bestDistSq && pawn.CanReach(spot, PathEndMode.Touch, Danger.Some))
                {
                    bestDistSq = distSq;
                    bestSpot = spot;
                }
            }

            return bestSpot;
        }

        /// <summary>
        /// Is there at least one Companion on the map who:
        /// - Is a valid Companion,
        /// - Is not downed or in an aggressive mental state,
        /// - Can start a date right now,
        /// - Can reach the client?
        /// 
        /// This is a simple availability check; future versions can add
        /// preference-based filtering (gender, traits, etc.).
        /// </summary>
        public static bool HasAvailableCompanionFor(Pawn client)
        {
            if (client == null || client.Map == null)
                return false;

            Map map = client.Map;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0)
                return false;

            foreach (Pawn pawn in colonists)
            {
                if (!IsPotentialCompanion(pawn))
                    continue;

                if (pawn.Downed || pawn.InAggroMentalState)
                    continue;

                CompCompanionship comp = pawn.TryGetComp<CompCompanionship>();
                if (comp == null)
                    continue;

                if (!comp.CanStartDateNow(pawn))
                    continue;

                if (!pawn.CanReach(client, PathEndMode.Touch, Danger.Some))
                    continue;

                return true;
            }

            return false;
        }
    }
}
