using RimWorld;
using Verse;

namespace Companionship
{
    [DefOf]
    public static class CompanionshipDefOf
    {
        // Jobs
        public static JobDef Companionship_Date;
        public static JobDef Companionship_WaitAtCompanionSpot;
        public static JobDef Companionship_JoinDate;
        public static JobDef Companionship_ChatAtCompanionSpot;

        // Things
        public static ThingDef Companionship_CompanionSpot;
        public static ThingDef Companionship_CompanionBed;

        // Thoughts
        public static ThoughtDef Companionship_CompanionDate;

        static CompanionshipDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipDefOf));
        }
    }
}
