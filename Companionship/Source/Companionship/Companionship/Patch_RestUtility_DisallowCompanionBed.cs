using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Hard-block the Companion Bed from being used by RimWorld's normal rest system.
    ///
    /// This prevents:
    /// - Colonists choosing it for sleeping (LayDown)
    /// - Guests/prisoners/slaves using it for sleeping
    /// - Vanilla lovin selecting it as a bed
    ///
    /// Companion "service" still works because our custom jobs do not rely on RestUtility
    /// to lay down in the bed; they use their own jobdrivers and reservations.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RestUtility_DisallowCompanionBed
    {
        static MethodBase TargetMethod()
        {
            // RimWorld 1.6 overload:
            // bool IsValidBedFor(Thing bedThing, Pawn sleeper, Pawn traveler,
            //                    bool checkSocialProperness, bool allowMedBedEvenIfSetToNoCare,
            //                    bool ignoreOtherReservations, GuestStatus? guestStatus)
            MethodBase method = AccessTools.Method(
                typeof(RestUtility),
                "IsValidBedFor",
                new[]
                {
                    typeof(Thing),
                    typeof(Pawn),
                    typeof(Pawn),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(GuestStatus?)
                });

            if (method == null)
                Log.Error("[Companionship] Patch_RestUtility_DisallowCompanionBed: Could not find RestUtility.IsValidBedFor overload for RimWorld 1.6.");

            return method;
        }

        public static bool Prefix(Thing bedThing, ref bool __result)
        {
            // C# 7.3 compatible (no "is not" pattern)
            Building_Bed bed = bedThing as Building_Bed;
            if (bed == null) return true;

            // Only override for our Companion Bed. Otherwise, let vanilla run.
            if (bed.def != CompanionshipDefOf.Companionship_CompanionBed)
                return true;

            // Hard block: never a valid bed for RestUtility purposes.
            __result = false;
            return false;
        }
    }
}
