using RimWorld;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// A simple marker building designating where visitors will wait
    /// if they desire companionship. Behavior is driven by jobs/logic,
    /// not by this building directly.
    /// </summary>
    public class Building_CompanionSpot : Building
    {
        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();

            string info = "Visitors who desire companionship will gather near this spot to wait for a Companion.";

            if (!string.IsNullOrEmpty(baseString))
            {
                return baseString + "\n" + info;
            }

            return info;
        }
    }
}
