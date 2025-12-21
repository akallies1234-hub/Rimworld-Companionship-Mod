using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    // -------------------------------------------------------------------------
    // Inspect string: show companion progression lines (hidden until xp > 0)
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString_CompanionshipProgress
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            try
            {
                Pawn pawn = __instance;
                if (pawn == null) return;

                // Only player-side humanlikes.
                if (pawn.Faction != Faction.OfPlayer) return;
                if (!(pawn.RaceProps?.Humanlike ?? true)) return;

                // Must have companion work type enabled to matter.
                if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(pawn)) return;

                // Only show after they have any progress.
                CompanionProgressRecord rec;
                if (!TryGetExistingProgressRecord(pawn, out rec)) return;
                if (rec == null || rec.xp <= 0) return;

                int tier = CompanionshipTierUtility.GetTierForXp(rec.xp);
                int nextThreshold = CompanionshipTierUtility.GetNextTierThresholdForXp(rec.xp);
                string title = CompanionshipTierUtility.GetTierTitle(tier);

                string line1 = $"Companion Title: {title}";
                string line2 = $"Tier {tier}: XP {rec.xp}/{nextThreshold}";
                string line3 = $"Lifetime Earnings: {rec.lifetimeEarningsSilver}";

                if (!__result.NullOrEmpty())
                    __result += "\n";

                __result += line1 + "\n" + line2 + "\n" + line3;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Companionship] Inspect progress postfix failed: {ex}");
            }
        }

        /// <summary>
        /// Read an existing progress record WITHOUT creating one.
        /// Preserves the "hidden until they earn it" behavior.
        /// </summary>
        private static bool TryGetExistingProgressRecord(Pawn pawn, out CompanionProgressRecord rec)
        {
            rec = null;

            CompanionshipProgressionWorldComponent wc = Find.World?.GetComponent<CompanionshipProgressionWorldComponent>();
            if (wc == null) return false;

            return wc.TryGet(pawn, out rec) && rec != null;
        }
    }

    // -------------------------------------------------------------------------
    // Social XP for ALL companion pipeline jobs (global patch)
    // -------------------------------------------------------------------------
    [HarmonyPatch(typeof(Pawn_JobTracker), "JobTrackerTick")]
    public static class Patch_Pawn_JobTrackerTick_CompanionshipSocialXp
    {
        // Tuning:
        private const int IntervalTicks = 60;      // once per second
        private const float XpPerInterval = 0.5f;  // modest, steady

        // Pawn_JobTracker.pawn is not publicly accessible in 1.6, so we FieldRef it.
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static void Postfix(Pawn_JobTracker __instance)
        {
            try
            {
                if (__instance == null) return;

                Pawn pawn = PawnRef(__instance);
                if (pawn == null) return;

                // Only player pawns as companions.
                if (pawn.Faction != Faction.OfPlayer) return;
                if (!(pawn.RaceProps?.Humanlike ?? true)) return;

                // Must have companion work type enabled.
                if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(pawn)) return;

                // Only while in the companionship pipeline.
                if (!CompanionshipPawnUtility.IsInCompanionshipPipelineJob(pawn)) return;

                if (pawn.skills == null) return;

                int now = Find.TickManager.TicksGame;
                if (now % IntervalTicks != 0) return;

                pawn.skills.Learn(SkillDefOf.Social, XpPerInterval, false);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Companionship] Social XP postfix failed: {ex}");
            }
        }
    }
}
