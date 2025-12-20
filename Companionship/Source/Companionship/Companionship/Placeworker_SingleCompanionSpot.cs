using Verse;

namespace Companionship
{
    public class PlaceWorker_SingleCompanionSpot : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef checkingDef,
            IntVec3 loc,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            ThingDef thingDef = checkingDef as ThingDef;
            if (thingDef == null || map == null)
                return true;

            // Enforce: only one of this def per map.
            var existing = map.listerThings.ThingsOfDef(thingDef);
            for (int i = 0; i < existing.Count; i++)
            {
                Thing t = existing[i];
                if (t == null) continue;
                if (t == thingToIgnore) continue;
                if (t == thing) continue;

                return "Only one Companion Spot can be placed per map.";
            }

            return true;
        }
    }
}
