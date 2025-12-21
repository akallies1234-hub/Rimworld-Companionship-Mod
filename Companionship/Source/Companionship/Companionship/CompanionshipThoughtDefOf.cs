using RimWorld;
using Verse;

namespace Companionship
{
    [DefOf]
    public static class CompanionshipThoughtDefOf
    {
        public static ThoughtDef Companionship_DateTerrible;
        public static ThoughtDef Companionship_DateBad;
        public static ThoughtDef Companionship_DateNeutral;
        public static ThoughtDef Companionship_DateGood;
        public static ThoughtDef Companionship_DateExcellent;
        public static ThoughtDef Companionship_DateExceptional;

        static CompanionshipThoughtDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipThoughtDefOf));
        }
    }
}
