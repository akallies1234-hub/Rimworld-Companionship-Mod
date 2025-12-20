using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Companionship
{
    [HarmonyPatch(typeof(Designator_Build), "DesignateSingleCell")]
    public static class DesignatorBuild_InstantBuild
    {
        public static bool Prefix(Designator_Build __instance, IntVec3 c)
        {
            var placingDef = GetPlacingThingDef(__instance);
            if (placingDef == null)
                return true;

            var ext = placingDef.GetModExtension<InstantBuildExtension>();
            if (ext == null || !ext.instantBuild)
                return true;

            Map map = Find.CurrentMap;
            if (map == null || !c.InBounds(map))
                return true;

            // If it's already here, do nothing.
            if (map.thingGrid.ThingsListAt(c).Any(t => t != null && t.def == placingDef))
                return false;

            // Handle stuff-based things safely (even if you don't use it yet).
            ThingDef stuffDef = GetSelectedStuffDef(__instance);
            Thing thing = placingDef.MadeFromStuff
                ? ThingMaker.MakeThing(placingDef, stuffDef ?? GenStuff.DefaultStuffFor(placingDef))
                : ThingMaker.MakeThing(placingDef);

            if (thing is Building b)
                b.SetFactionDirect(Faction.OfPlayer);

            Rot4 rot = GetPlacingRotation(__instance) ?? Rot4.North;
            GenSpawn.Spawn(thing, c, map, rot);

            // Skip vanilla (blueprint placement).
            return false;
        }

        private static ThingDef GetPlacingThingDef(Designator_Build inst)
        {
            // Try property "PlacingDef"
            var prop = AccessTools.Property(inst.GetType(), "PlacingDef");
            if (prop != null)
                return prop.GetValue(inst) as ThingDef;

            // Try property "EntDef"
            prop = AccessTools.Property(inst.GetType(), "EntDef");
            if (prop != null)
                return prop.GetValue(inst) as ThingDef;

            // Try field "entDef" (older pattern)
            var field = AccessTools.Field(inst.GetType(), "entDef");
            if (field != null)
                return field.GetValue(inst) as ThingDef;

            return null;
        }

        private static ThingDef GetSelectedStuffDef(Designator_Build inst)
        {
            // Try property "StuffDef"
            var prop = AccessTools.Property(inst.GetType(), "StuffDef");
            if (prop != null)
                return prop.GetValue(inst) as ThingDef;

            // Try field "stuffDef"
            var field = AccessTools.Field(inst.GetType(), "stuffDef");
            if (field != null)
                return field.GetValue(inst) as ThingDef;

            return null;
        }

        private static Rot4? GetPlacingRotation(Designator_Build inst)
        {
            // Try field "placingRot" (common internal field on designators)
            var field = AccessTools.Field(inst.GetType(), "placingRot");
            if (field != null)
            {
                object val = field.GetValue(inst);
                if (val is Rot4 r) return r;
            }

            // Some versions expose a "PlacingRot" property
            var prop = AccessTools.Property(inst.GetType(), "PlacingRot");
            if (prop != null)
            {
                object val = prop.GetValue(inst);
                if (val is Rot4 r) return r;
            }

            return null;
        }
    }
}
