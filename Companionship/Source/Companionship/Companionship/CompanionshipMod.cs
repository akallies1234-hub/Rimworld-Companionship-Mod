using Verse;

namespace Companionship
{
    public class CompanionshipMod : Mod
    {
        // IMPORTANT: keep this identical to About.xml <packageId>
        public const string ModId = "Riot.Companionship";

        public CompanionshipMod(ModContentPack content) : base(content)
        {
            HarmonyInit.EnsureInitialized();
            // No logging here (keeps player logs clean).
        }
    }
}
