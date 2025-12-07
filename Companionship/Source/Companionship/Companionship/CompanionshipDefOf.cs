using RimWorld;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// Central DefOf class for the Companionship mod.
    /// 
    /// IMPORTANT:
    /// - Every field here must match a defName in XML.
    /// </summary>
    [DefOf]
    public static class CompanionshipDefOf
    {
        static CompanionshipDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipDefOf));
        }

        // Work type for Companions.
        public static WorkTypeDef Companion;

        // Main Companion date jobs.
        public static JobDef DoCompanionDate;
        public static JobDef DoCompanionDate_Client;

        // Visitor waiting job at the Companion Spot.
        public static JobDef WaitForCompanionDate;

        // WorkGiver that assigns Companion date jobs.
        public static WorkGiverDef DoCompanionDate_WorkGiver;

        // Bed type used for Companion dates.
        public static ThingDef CompanionBed;

        // Spot where visitors wait for Companions.
        public static ThingDef CompanionSpot;

        // Thoughts given based on date outcome.
        public static ThoughtDef CompanionDate_Excellent;
        public static ThoughtDef CompanionDate_Good;
        public static ThoughtDef CompanionDate_Bad;
        public static ThoughtDef CompanionDate_Terrible;
    }
}
