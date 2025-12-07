using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// Helper for working with DateScriptDef:
    /// - Fetch all scripts
    /// - Get scripts available for a given companion tier
    /// - Select a script for a given companion + client
    /// </summary>
    public static class DateScriptUtility
    {
        // Default script to fall back to if selection fails.
        private const string DefaultSimpleScriptDefName = "SimpleWalkAndLovin_T1";

        /// <summary>
        /// All date scripts currently defined.
        /// </summary>
        public static List<DateScriptDef> AllScripts =>
            DefDatabase<DateScriptDef>.AllDefsListForReading;

        /// <summary>
        /// Try to get a default "simple" script. Falls back to any available script if needed.
        /// </summary>
        public static DateScriptDef GetDefaultScript()
        {
            DateScriptDef def = DefDatabase<DateScriptDef>.GetNamedSilentFail(DefaultSimpleScriptDefName);
            if (def != null)
            {
                return def;
            }

            // If named default not found, just pick the first available script.
            if (AllScripts != null && AllScripts.Count > 0)
            {
                return AllScripts[0];
            }

            return null;
        }

        /// <summary>
        /// Get all scripts whose tier is less than or equal to the companion's tier.
        /// For now we ignore per-tier daily usage; we'll add that constraint later.
        /// </summary>
        public static List<DateScriptDef> GetScriptsAvailableFor(Pawn companion)
        {
            List<DateScriptDef> result = new List<DateScriptDef>();

            if (companion == null)
            {
                return result;
            }

            CompCompanionship comp = companion.TryGetComp<CompCompanionship>();
            int companionTier = comp?.CurrentTier ?? 1;

            if (AllScripts == null || AllScripts.Count == 0)
            {
                return result;
            }

            foreach (DateScriptDef script in AllScripts)
            {
                if (script != null && script.tier <= companionTier)
                {
                    result.Add(script);
                }
            }

            return result;
        }

        /// <summary>
        /// Simple selection for now:
        /// - Filter scripts by companion tier
        /// - Prefer the highest-tier scripts
        /// - If multiple at highest tier, pick a random one
        /// - If none available, fall back to default script
        /// </summary>
        public static DateScriptDef SelectScriptFor(Pawn companion, Pawn client)
        {
            List<DateScriptDef> available = GetScriptsAvailableFor(companion);
            if (available == null || available.Count == 0)
            {
                return GetDefaultScript();
            }

            // Find the highest tier among the available scripts.
            int maxTier = available.Max(s => s.tier);

            // Get only scripts at that highest tier.
            List<DateScriptDef> highestTierScripts = available
                .Where(s => s.tier == maxTier)
                .ToList();

            if (highestTierScripts.Count == 0)
            {
                return GetDefaultScript();
            }

            // If only one, return it; otherwise pick a random one.
            if (highestTierScripts.Count == 1)
            {
                return highestTierScripts[0];
            }

            // RandomElement() is a Verse extension method.
            return highestTierScripts.RandomElement();
        }
    }
}
