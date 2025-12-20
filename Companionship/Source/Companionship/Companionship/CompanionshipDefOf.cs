using RimWorld;
using Verse;

namespace Companionship
{
    [DefOf]
    public static class CompanionshipDefOf
    {
        public static ThingDef Companionship_CompanionSpot;
        public static ThingDef Companionship_CompanionBed;

        // Visitor-side jobs
        public static JobDef Companionship_VisitorLoiterAtCompanionSpot;
        public static JobDef Companionship_VisitorParticipateGreeting;
        public static JobDef Companionship_VisitorFollowCompanionToBed;

        // Colonist companion job (work type)
        public static JobDef Companionship_CompanionGreetAndEscortToBed;

        // Custom lovin (initiator + partner)
        public static JobDef Companionship_CustomLovin;
        public static JobDef Companionship_CustomLovinPartner;

        static CompanionshipDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipDefOf));
        }
    }
}
