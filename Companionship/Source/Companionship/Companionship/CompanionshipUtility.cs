using Verse;
using RimWorld;

namespace Riot.Companionship
{
    public static class CompanionshipUtility
    {
        public static bool IsCompanion(Pawn pawn)
        {
            return pawn.WorkTypeIsActive(WorkTypeDefOf.Social);
        }
    }
}
