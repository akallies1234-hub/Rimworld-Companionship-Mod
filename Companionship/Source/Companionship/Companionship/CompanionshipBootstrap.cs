using System;
using HarmonyLib;
using Verse;

namespace Companionship
{
    [StaticConstructorOnStartup]
    public static class CompanionshipBootstrap
    {
        static CompanionshipBootstrap()
        {
            try
            {
                var harmony = new Harmony("Riot.Companionship");
                harmony.PatchAll();
                Log.Message("[Companionship] Loaded. Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[Companionship] Failed to apply Harmony patches.\n{ex}");
            }
        }
    }
}
