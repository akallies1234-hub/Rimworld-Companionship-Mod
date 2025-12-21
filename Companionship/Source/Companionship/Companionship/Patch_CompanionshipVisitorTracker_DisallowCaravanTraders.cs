using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Companionship
{
    // Disallow the trade caravan "trader" pawn (yellow question mark) from being eligible
    // for Companionship desire/records. This prevents pack animals from being dragged indoors.
    [HarmonyPatch(typeof(CompanionshipVisitorTracker), "IsValidVisitor")]
    public static class Patch_CompanionshipVisitorTracker_DisallowCaravanTraders
    {
        private static readonly PropertyInfo TraderKindProp =
            AccessTools.Property(typeof(Pawn), "TraderKind");

        public static void Postfix(Pawn p, ref bool __result)
        {
            if (!__result) return;
            if (p == null) return;

            if (IsTradeCaravanTrader(p))
                __result = false;
        }

        private static bool IsTradeCaravanTrader(Pawn p)
        {
            try
            {
                if (p == null) return false;
                if (TraderKindProp == null) return false;

                object tk = TraderKindProp.GetValue(p, null);
                return tk != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
