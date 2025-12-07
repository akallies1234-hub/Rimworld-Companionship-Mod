using RimWorld;
using Verse;

namespace Riot.Companionship
{
    public static class DateOutcomeUtility
    {
        // Placeholder until tiered outcome scoring is added
        public static void ApplyOutcomeThoughts(Pawn companion, Pawn client, bool wasGood)
        {
            if (wasGood)
            {
                client.needs?.mood?.thoughts?.memories?.TryGainMemory(CompanionshipDefOf.CompanionDate_Satisfied);
                companion.needs?.mood?.thoughts?.memories?.TryGainMemory(CompanionshipDefOf.CompanionDate_Satisfied);
            }
            else
            {
                client.needs?.mood?.thoughts?.memories?.TryGainMemory(CompanionshipDefOf.CompanionDate_Dissatisfied);
                companion.needs?.mood?.thoughts?.memories?.TryGainMemory(CompanionshipDefOf.CompanionDate_Dissatisfied);
            }
        }
    }
}
