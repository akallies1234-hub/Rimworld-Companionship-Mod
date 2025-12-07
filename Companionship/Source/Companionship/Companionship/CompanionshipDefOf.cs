using RimWorld;
using Verse;

namespace Riot.Companionship
{
    [DefOf]
    public static class CompanionshipDefOf
    {
        static CompanionshipDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipDefOf));
        }

        // Jobs
        public static JobDef CompanionDate;
        public static JobDef WaitForCompanionDate;
        public static JobDef GoToCompanionSpot;

        // Buildings
        public static ThingDef CompanionSpot;

        // Thoughts (temporary — will be upgraded in Phase 3)
        public static ThoughtDef CompanionDate_Satisfied;
        public static ThoughtDef CompanionDate_Dissatisfied;
    }
}
