using RimWorld;
using UnityEngine;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// Handles silver payment for completed Companion dates.
    /// 
    /// Pricing factors:
    /// - Tier base fee
    /// - Social skill (2% per level)
    /// - Beauty stat (± up to ~25% after clamping)
    /// - SocialImpact stat (± up to ~25% after clamping)
    /// - Health/pain factor (up to -30% at extreme pain)
    /// - Tip based on date outcome (Good/Excellent)
    /// 
    /// Also exposes an estimator used for UI (no tip included).
    /// </summary>
    public static class PaymentUtility
    {
        /// <summary>
        /// Called by JobDriver_CompanionDate when a date completes successfully.
        /// Spawns silver near the companion pawn.
        /// </summary>
        public static void PayForDate(Pawn companion, Pawn client, DateOutcome outcome, Building_Bed bed)
        {
            if (companion == null || companion.Map == null)
            {
                return;
            }

            int silverAmount = CalculatePayment(companion, client, outcome);
            if (silverAmount <= 0)
            {
                return;
            }

            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = silverAmount;

            IntVec3 dropCell = companion.PositionHeld;
            Map map = companion.MapHeld;

            GenPlace.TryPlaceThing(silver, dropCell, map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// Core pricing formula used when actually paying for a date.
        /// 
        /// Factors:
        /// - Tier base fee:
        ///     Tier 1: 50
        ///     Tier 2: 75
        ///     Tier 3: 100
        ///     Tier 4: 125
        ///     Tier 5: 150
        /// - Social skill: +2% per skill level.
        /// - Beauty stat: +5% per point of Beauty (clamped to [-3, +5]).
        /// - SocialImpact stat: scaled around 1.0, up to ±25% after clamping.
        /// - Health/pain: up to -30% at extreme pain.
        /// - Outcome tip:
        ///     Good: +5%
        ///     Excellent: +10%
        /// </summary>
        private static int CalculatePayment(Pawn companion, Pawn client, DateOutcome outcome)
        {
            if (companion == null)
            {
                return 0;
            }

            // 1) Base fee from tier.
            int tier = 1;
            CompCompanionship comp = companion.TryGetComp<CompCompanionship>();
            if (comp != null)
            {
                tier = comp.CurrentTier;
            }

            int baseFee = GetBaseFeeForTier(tier);

            // 2) Social skill factor: +2% per level.
            float socialLevel = 0f;
            if (companion.skills != null)
            {
                SkillRecord socialSkill = companion.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null)
                {
                    socialLevel = socialSkill.Level;
                }
            }
            float socialFactor = 1f + (socialLevel * 0.02f);

            // 3) Beauty factor: based on Beauty stat, clamped.
            float beauty = companion.GetStatValue(StatDefOf.Beauty, true);
            beauty = Mathf.Clamp(beauty, -3f, 5f);
            float beautyFactor = 1f + (beauty * 0.05f);

            // 4) SocialImpact factor:
            //    SocialImpact is centered around 1.0 in vanilla.
            //    We treat 1.0 as neutral.
            float socialImpact = companion.GetStatValue(StatDefOf.SocialImpact, true);
            float impactDelta = socialImpact - 1f;
            impactDelta = Mathf.Clamp(impactDelta, -0.5f, 0.5f); // clamp to [-0.5, +0.5]
            float socialImpactFactor = 1f + (impactDelta * 0.5f); // up to ±25%

            // 5) Health/pain factor:
            //    Higher pain reduces price, up to -30% at extreme pain.
            float pain = 0f;
            if (companion.health != null && companion.health.hediffSet != null)
            {
                pain = companion.health.hediffSet.PainTotal; // 0..1+
            }
            pain = Mathf.Clamp01(pain);
            float healthFactor = 1f - (pain * 0.3f);

            // 6) Outcome / tip factor.
            float outcomeFactor = 1f;
            switch (outcome)
            {
                case DateOutcome.Good:
                    outcomeFactor = 1.05f;
                    break;
                case DateOutcome.Excellent:
                    outcomeFactor = 1.10f;
                    break;
                default:
                    outcomeFactor = 1f;
                    break;
            }

            // Combine all factors.
            float total = baseFee;
            total *= socialFactor;
            total *= beautyFactor;
            total *= socialImpactFactor;
            total *= healthFactor;
            total *= outcomeFactor;

            // Safety: never pay less than 1 silver.
            int silver = GenMath.RoundRandom(Mathf.Max(total, 1f));
            return silver;
        }

        /// <summary>
        /// Estimated base payment for UI: same as CalculatePayment,
        /// but with no outcome tip (assumes neutral date quality).
        /// </summary>
        public static int EstimateBasePaymentWithoutTip(Pawn companion)
        {
            if (companion == null)
            {
                return 0;
            }

            int tier = 1;
            CompCompanionship comp = companion.TryGetComp<CompCompanionship>();
            if (comp != null)
            {
                tier = comp.CurrentTier;
            }

            int baseFee = GetBaseFeeForTier(tier);

            // Social factor
            float socialLevel = 0f;
            if (companion.skills != null)
            {
                SkillRecord socialSkill = companion.skills.GetSkill(SkillDefOf.Social);
                if (socialSkill != null)
                {
                    socialLevel = socialSkill.Level;
                }
            }
            float socialFactor = 1f + (socialLevel * 0.02f);

            // Beauty factor
            float beauty = companion.GetStatValue(StatDefOf.Beauty, true);
            beauty = Mathf.Clamp(beauty, -3f, 5f);
            float beautyFactor = 1f + (beauty * 0.05f);

            // SocialImpact factor
            float socialImpact = companion.GetStatValue(StatDefOf.SocialImpact, true);
            float impactDelta = socialImpact - 1f;
            impactDelta = Mathf.Clamp(impactDelta, -0.5f, 0.5f);
            float socialImpactFactor = 1f + (impactDelta * 0.5f);

            // Health/pain factor
            float pain = 0f;
            if (companion.health != null && companion.health.hediffSet != null)
            {
                pain = companion.health.hediffSet.PainTotal;
            }
            pain = Mathf.Clamp01(pain);
            float healthFactor = 1f - (pain * 0.3f);

            float total = baseFee;
            total *= socialFactor;
            total *= beautyFactor;
            total *= socialImpactFactor;
            total *= healthFactor;

            int silver = GenMath.RoundRandom(Mathf.Max(total, 1f));
            return silver;
        }

        /// <summary>
        /// Base fee by Companion tier.
        /// </summary>
        private static int GetBaseFeeForTier(int tier)
        {
            switch (tier)
            {
                case 1:
                    return 50;
                case 2:
                    return 75;
                case 3:
                    return 100;
                case 4:
                    return 125;
                case 5:
                default:
                    return 150;
            }
        }
    }
}
