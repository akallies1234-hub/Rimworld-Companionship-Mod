using RimWorld;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// Possible outcomes for a date.
    /// </summary>
    public enum DateOutcome
    {
        Terrible = -2,
        Bad = -1,
        Neutral = 0,
        Good = 1,
        Excellent = 2
    }

    /// <summary>
    /// Utility for calculating and applying date outcomes.
    /// 
    /// - CalculateOutcome: turns companion/client/room info into a DateOutcome.
    /// - ApplyDateOutcome: gives both pawns appropriate memories.
    /// </summary>
    public static class DateOutcomeUtility
    {
        /// <summary>
        /// Calculate the outcome of a date based on:
        /// - Companion's Social skill
        /// - Companion's Beauty stat
        /// - Room impressiveness of the bed
        /// - A small random factor
        /// 
        /// This is intentionally simple but structured so we can extend it later
        /// (traits, mood, XP, etc.).
        /// </summary>
        public static DateOutcome CalculateOutcome(Pawn companion, Pawn client, Building_Bed bed)
        {
            if (companion == null || client == null || bed == null)
            {
                return DateOutcome.Neutral;
            }

            Map map = bed.Map;
            Room room = bed.GetRoom();
            float roomImpressiveness = 0f;

            if (room != null)
            {
                roomImpressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
            }

            // Companion's Social skill (0–20).
            float socialLevel = 0f;
            if (companion.skills != null)
            {
                SkillRecord socialSkill = companion.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null)
                {
                    socialLevel = socialSkill.Level;
                }
            }

            // Companion's Beauty stat (can be negative, zero, or positive).
            float beautyStat = companion.GetStatValue(StatDefOf.Beauty, true);

            // Base score: a weighted sum.
            float score = 0f;
            score += socialLevel * 1.5f;
            score += beautyStat * 2.0f;
            score += roomImpressiveness * 0.1f;

            // Add a small random swing so dates are not fully deterministic.
            score += Rand.Range(-5f, 5f);

            // Map score bands to outcomes.
            if (score < 10f)
            {
                return DateOutcome.Terrible;
            }
            if (score < 20f)
            {
                return DateOutcome.Bad;
            }
            if (score < 30f)
            {
                return DateOutcome.Neutral;
            }
            if (score < 40f)
            {
                return DateOutcome.Good;
            }

            return DateOutcome.Excellent;
        }

        /// <summary>
        /// Apply the given date outcome as memories to both initiator and recipient.
        /// Uses shared ThoughtDefs for now.
        /// </summary>
        public static void ApplyDateOutcome(Pawn initiator, Pawn recipient, DateOutcome outcome)
        {
            if (initiator == null || recipient == null)
            {
                return;
            }

            switch (outcome)
            {
                case DateOutcome.Excellent:
                    TryGiveThought(initiator, recipient, CompanionshipDefOf.CompanionDate_Excellent);
                    TryGiveThought(recipient, initiator, CompanionshipDefOf.CompanionDate_Excellent);
                    break;

                case DateOutcome.Good:
                    TryGiveThought(initiator, recipient, CompanionshipDefOf.CompanionDate_Good);
                    TryGiveThought(recipient, initiator, CompanionshipDefOf.CompanionDate_Good);
                    break;

                case DateOutcome.Neutral:
                    // No memory for now.
                    break;

                case DateOutcome.Bad:
                    TryGiveThought(initiator, recipient, CompanionshipDefOf.CompanionDate_Bad);
                    TryGiveThought(recipient, initiator, CompanionshipDefOf.CompanionDate_Bad);
                    break;

                case DateOutcome.Terrible:
                    TryGiveThought(initiator, recipient, CompanionshipDefOf.CompanionDate_Terrible);
                    TryGiveThought(recipient, initiator, CompanionshipDefOf.CompanionDate_Terrible);
                    break;
            }
        }

        private static void TryGiveThought(Pawn pawn, Pawn otherPawn, ThoughtDef thoughtDef)
        {
            if (pawn == null || thoughtDef == null)
            {
                return;
            }

            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return;
            }

            MemoryThoughtHandler memories = pawn.needs.mood.thoughts?.memories;
            if (memories == null)
            {
                return;
            }

            memories.TryGainMemory(thoughtDef, otherPawn);
        }
    }
}
