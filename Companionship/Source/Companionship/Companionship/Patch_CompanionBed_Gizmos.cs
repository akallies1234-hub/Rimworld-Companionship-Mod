using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Companionship
{
    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
    public static class Patch_CompanionBed_Gizmos
    {
        static void Postfix(Building_Bed __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || __instance.def != CompanionshipDefOf.Companionship_CompanionBed)
                return;

            __result = Filter(__result);
        }

        private static IEnumerable<Gizmo> Filter(IEnumerable<Gizmo> source)
        {
            foreach (Gizmo g in source)
            {
                if (ShouldSuppressGizmo(g))
                    continue;

                yield return g;
            }
        }

        private static bool ShouldSuppressGizmo(Gizmo g)
        {
            Command cmd = g as Command;
            if (cmd == null)
                return false;

            string label = cmd.defaultLabel ?? string.Empty;
            string typeName = cmd.GetType().Name ?? string.Empty;

            // Owner/medical/bed-type controls (the ones you want gone)
            if (ContainsIgnoreCase(label, "Set owner")) return true;
            if (ContainsIgnoreCase(label, "Medical")) return true;

            if (ContainsIgnoreCase(label, "For colonists")) return true;
            if (ContainsIgnoreCase(label, "For prisoners")) return true;
            if (ContainsIgnoreCase(label, "For slaves")) return true;

            // Fallback by type name (covers label changes / translations / modded labels)
            if (ContainsIgnoreCase(typeName, "Owner")) return true;
            if (ContainsIgnoreCase(typeName, "Medical")) return true;
            if (ContainsIgnoreCase(typeName, "BedType")) return true;

            return false;
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
