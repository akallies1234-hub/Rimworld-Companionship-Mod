using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Single source of truth for "companion eligibility" rules.
    /// Use this from WorkGivers, bed validation, future pricing/XP, etc.
    /// </summary>
    public static class CompanionshipPawnUtility
    {
        private static WorkTypeDef cachedCompanionWorkType;
        private static bool cachedCompanionWorkTypeSearched;

        private static TraitDef cachedBisexualTrait;
        private static TraitDef cachedAsexualTrait;
        private static bool cachedOptionalTraitsSearched;

        /// <summary>
        /// WorkTypeDef from XML: defName="Companionship_Companion"
        /// </summary>
        public static WorkTypeDef CompanionWorkTypeDef
        {
            get
            {
                if (cachedCompanionWorkTypeSearched) return cachedCompanionWorkType;

                cachedCompanionWorkTypeSearched = true;
                cachedCompanionWorkType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Companionship_Companion");
                return cachedCompanionWorkType;
            }
        }

        /// <summary>
        /// True if this pawn has the Companion work type enabled.
        /// Inclusive: any pawn that has work settings and the type is active.
        /// </summary>
        public static bool HasCompanionWorkTypeEnabled(Pawn p)
        {
            if (p == null) return false;
            if (p.workSettings == null) return false;
            if (!p.workSettings.EverWork) return false;

            WorkTypeDef wt = CompanionWorkTypeDef;
            if (wt == null) return false;

            return p.workSettings.WorkIsActive(wt);
        }

        /// <summary>
        /// True if this pawn is currently executing one of our "date pipeline" jobs.
        /// This is used to allow visitors (who don't have work settings) to use the Companion Bed during the pipeline.
        /// </summary>
        public static bool IsInCompanionshipPipelineJob(Pawn p)
        {
            if (p == null) return false;

            JobDef jd = p.CurJobDef;
            return jd == CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed ||
                   jd == CompanionshipDefOf.Companionship_CustomLovin ||
                   jd == CompanionshipDefOf.Companionship_CustomLovinPartner ||
                   jd == CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed ||
                   jd == CompanionshipDefOf.Companionship_VisitorParticipateGreeting;
        }

        /// <summary>
        /// "Can this pawn act as a companion worker?"
        /// This is the rule WorkGivers should use.
        /// </summary>
        public static bool IsEligibleCompanionWorker(Pawn p)
        {
            if (p == null) return false;
            if (p.Dead || p.Destroyed) return false;
            if (!p.Spawned) return false;
            if (p.Downed) return false;
            if (p.InMentalState) return false;
            if (p.Map == null) return false;

            return HasCompanionWorkTypeEnabled(p);
        }

        /// <summary>
        /// "Can this pawn treat the Companion Bed as valid right now?"
        /// Inclusive rule:
        /// - Allowed if they are an eligible companion worker (Companion work type enabled),
        ///   OR they are currently in the companionship pipeline (visitor/partner jobs).
        /// </summary>
        public static bool CanUseCompanionBed(Pawn p)
        {
            if (p == null) return false;

            if (IsInCompanionshipPipelineJob(p))
                return true;

            return HasCompanionWorkTypeEnabled(p);
        }

        /// <summary>
        /// Romance-preference gating for companionship services.
        ///
        /// IMPORTANT: This checks ONLY the companion's gender preference/orientation, not "romance chance".
        /// Using romance-chance style helpers can return 0 for guests for reasons unrelated to preference,
        /// which can accidentally block all visitors.
        ///
        /// Rules:
        /// - If companion is Asexual (if trait exists): never compatible
        /// - If companion is Bisexual (if trait exists): compatible with any gender (except Gender.None)
        /// - If companion is Gay: same gender only
        /// - Otherwise: opposite gender only
        /// </summary>
        public static bool IsRomancePreferenceCompatible(Pawn companion, Pawn visitor)
        {
            if (companion == null || visitor == null) return false;

            // We only handle Male/Female pairing logic; Gender.None should never pass.
            if (companion.gender == Gender.None || visitor.gender == Gender.None)
                return false;

            EnsureOptionalTraitsCached();

            // If we can read traits, apply orientation rules.
            if (companion.story?.traits != null)
            {
                // Asexual: nobody.
                if (cachedAsexualTrait != null && companion.story.traits.HasTrait(cachedAsexualTrait))
                    return false;

                // Bisexual: any gender.
                if (cachedBisexualTrait != null && companion.story.traits.HasTrait(cachedBisexualTrait))
                    return true;

                // Gay: same gender.
                if (companion.story.traits.HasTrait(TraitDefOf.Gay))
                    return companion.gender == visitor.gender;

                // Default (vanilla): opposite gender.
                return companion.gender != visitor.gender;
            }

            // If no traits/story, assume vanilla default: opposite gender.
            return companion.gender != visitor.gender;
        }

        private static void EnsureOptionalTraitsCached()
        {
            if (cachedOptionalTraitsSearched) return;
            cachedOptionalTraitsSearched = true;

            // These are not vanilla traits, but some mods add them.
            cachedBisexualTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Bisexual");
            cachedAsexualTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Asexual");
        }
    }
}
