using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Companion beds are special-purpose and should not expose the standard bed/medical/ownership toggles.
    ///
    /// NOTE: Gizmo filtering is handled via Harmony (see Patch_CompanionBed_Gizmos) so the policy lives in one place.
    /// This class is kept for future bed-specific behavior.
    /// </summary>
    public class Building_CompanionBed : Building_Bed
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            return base.GetGizmos();
        }
    }
}
