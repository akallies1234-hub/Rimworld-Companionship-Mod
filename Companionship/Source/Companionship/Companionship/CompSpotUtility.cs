using Verse;
using System.Linq;
using System.Collections.Generic;

namespace Riot.Companionship
{
    public static class CompSpotUtility
    {
        public static Thing GetClosestSpot(Pawn pawn)
        {
            var spots = pawn.Map.listerThings.ThingsOfDef(CompanionshipDefOf.CompanionSpot);
            if (spots.NullOrEmpty()) return null;

            return spots.OrderBy(s => s.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
        }
    }
}
