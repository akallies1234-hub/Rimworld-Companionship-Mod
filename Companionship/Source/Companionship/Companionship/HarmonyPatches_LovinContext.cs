using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Scoped lovin support:
    /// - Allow lovin eligibility ONLY for our temporary date pair
    /// - Pay/moodlet when vanilla lovin job ends
    /// - Never affects normal couples because everything is gated behind our pair authorization
    /// </summary>
    [HarmonyPatch(typeof(LovePartnerRelationUtility), "LovePartnerRelationExists")]
    public static class HarmonyPatches_LovinContext_LovePartnerRelationExists
    {
        // RimWorld 1.6 signature: LovePartnerRelationExists(Pawn first, Pawn second)
        public static bool Prefix(Pawn first, Pawn second, ref bool __result)
        {
            GameComponent_Companionship gc = GameComponent_Companionship.GetOrCreate();
            if (gc != null && gc.IsLovinAllowed(first, second))
            {
                __result = true;
                return false; // skip vanilla
            }

            return true;
        }
    }

    /// <summary>
    /// We patch the BASE JobDriver.Cleanup (since JobDriver_Lovin may not override Cleanup in your build).
    /// Then we only act when the instance is actually JobDriver_Lovin.
    /// </summary>
    [HarmonyPatch]
    public static class HarmonyPatches_LovinContext_JobDriverCleanup
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            // Verse.AI.JobDriver.Cleanup(JobCondition)
            return AccessTools.Method(typeof(JobDriver), "Cleanup", new Type[] { typeof(JobCondition) });
        }

        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            // Only care about lovin cleanup
            JobDriver_Lovin lovin = __instance as JobDriver_Lovin;
            if (lovin == null) return;

            Pawn actor = lovin.pawn;
            if (actor == null) return;

            Job job = lovin.job;
            if (job == null) return;

            // In vanilla, targetA is partner pawn, targetB is usually the bed (Thing).
            Pawn partner = null;
            if (job.targetA.IsValid) partner = job.targetA.Pawn;
            if (partner == null && job.targetB.IsValid) partner = job.targetB.Pawn;
            if (partner == null) return;

            GameComponent_Companionship gc = GameComponent_Companionship.GetOrCreate();
            if (gc == null) return;

            GameComponent_Companionship.PairData data;
            if (!gc.TryConsumePayout(actor, partner, out data))
            {
                // Not our authorized pair (or already processed)
                return;
            }

            // Spawn payment near bed if possible
            Map map = FindMapById(data != null ? data.mapId : -1) ?? actor.Map;
            IntVec3 pos = data != null ? data.bedPos : actor.Position;

            if (map != null && data != null && data.paymentSilver > 0)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = data.paymentSilver;
                GenPlace.TryPlaceThing(silver, pos, map, ThingPlaceMode.Near);
            }

            // Moodlet to both
            TryGiveDateMoodlet(actor);
            TryGiveDateMoodlet(partner);
        }

        private static Map FindMapById(int mapId)
        {
            if (mapId < 0) return null;
            if (Find.Maps == null) return null;

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map m = Find.Maps[i];
                if (m != null && m.uniqueID == mapId) return m;
            }

            return null;
        }

        private static ThoughtDef cachedThought;

        private static void TryGiveDateMoodlet(Pawn p)
        {
            if (p == null) return;
            if (p.needs == null || p.needs.mood == null) return;

            if (cachedThought == null)
                cachedThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Companionship_CompanionDate");

            if (cachedThought != null)
                p.needs.mood.thoughts.memories.TryGainMemory(cachedThought);
        }
    }
}
