using Verse;

namespace Riot.Companionship
{
    // This class is automatically picked up by RimWorld because it derives from Verse.Mod
    public class CompanionshipMod : Mod
    {
        public CompanionshipMod(ModContentPack content) : base(content)
        {
            Log.Message("[Companionship] Mod initialized.");
        }
    }
}
