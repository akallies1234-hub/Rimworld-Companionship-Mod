using HarmonyLib;

namespace Companionship
{
    public static class HarmonyInit
    {
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            var harmony = new Harmony(CompanionshipMod.ModId);
            harmony.PatchAll();
        }
    }
}
