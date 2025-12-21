using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Central utility for the "receipt moment" and companion-work related helpers.
    ///
    /// For now:
    /// - HandleSuccessfulDate(...) is the single choke point that will later award silver, moodlets, and companion XP.
    /// - AccrueCompanionWorkSocialSkill(...) grants Social skill XP while a pawn is actively doing companion work.
    /// </summary>
    public static class CompanionshipDatePayoutUtility
    {
        // Conservative defaults. We can tune later (or expose to mod settings).
        private const int SocialXpIntervalTicks = 250;   // ~4.16 seconds
        private const float SocialXpPerInterval = 0.33f; // small, steady learning

        /// <summary>
        /// Called exactly once when a date is successful (lovin runs full duration without interruption).
        /// </summary>
        public static void HandleSuccessfulDate(Pawn companion, Pawn visitor, Building_Bed bed)
        {
            // Intentionally empty for this step.
            // Next steps: moodlets -> xp -> tier -> silver payout spawn -> lifetime earnings.
            if (CompanionshipDebug.VerboseLogging)
            {
                Log.Message($"[Companionship] Successful date: companion={companion?.LabelShortCap ?? "null"}, visitor={visitor?.LabelShortCap ?? "null"}, bed={bed?.LabelShortCap ?? "null"}");
            }
        }

        /// <summary>
        /// Grants social skill XP while a pawn is actively doing companion work.
        /// Call this from tickActions inside companion-work JobDrivers.
        /// </summary>
        public static void AccrueCompanionWorkSocialSkill(Pawn companion)
        {
            if (companion?.skills == null) return;

            // Only learn on an interval to avoid per-tick overhead.
            if (Find.TickManager.TicksGame % SocialXpIntervalTicks != 0) return;

            companion.skills.Learn(SkillDefOf.Social, SocialXpPerInterval, true);
        }
    }
}
