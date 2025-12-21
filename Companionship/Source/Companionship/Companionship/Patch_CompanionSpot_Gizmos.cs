using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace Companionship
{
    // Dev-mode gizmos for the Companion Spot:
    //  - Dump Status (writes tracker report to Player.log)
    //  - Reroll Unclaimed Visitors (forces next-tick desire reroll)
    //  - Toggle Overlay (toggles Settings.enableDebugOverlay)
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Patch_CompanionSpot_Gizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            foreach (var g in __result)
                yield return g;

            if (__instance == null) yield break;
            if (__instance.def != CompanionshipDefOf.Companionship_CompanionSpot) yield break;

            // Only show these utilities in Dev Mode (matches your expectation).
            if (!Prefs.DevMode) yield break;

            yield return MakeDumpStatusGizmo(__instance);
            yield return MakeRerollUnclaimedVisitorsGizmo(__instance);
            yield return MakeOverlayToggleGizmo();
        }

        private static Gizmo MakeDumpStatusGizmo(ThingWithComps spot)
        {
            return new Command_Action
            {
                defaultLabel = "Companionship: Dump status",
                defaultDesc = "Writes the current Companionship visitor/session report to the Player.log.",
                icon = TexCommand.ForbidOff,
                action = () =>
                {
                    Map map = spot.Map;
                    if (map == null)
                    {
                        Messages.Message("No map available for Companion Spot.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    CompanionshipVisitorTracker tracker = map.GetComponent<CompanionshipVisitorTracker>();
                    if (tracker == null)
                    {
                        Messages.Message("CompanionshipVisitorTracker component not found on this map.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    Log.Message(tracker.GetDebugReport());
                    Messages.Message("Companionship status dumped to log.", MessageTypeDefOf.TaskCompletion);
                }
            };
        }

        private static Gizmo MakeRerollUnclaimedVisitorsGizmo(ThingWithComps spot)
        {
            return new Command_Action
            {
                defaultLabel = "Companionship: Reroll unclaimed visitors",
                defaultDesc = "Forces all unclaimed visitors to reroll desire on the next tick (useful for debugging the loop).",
                icon = TexCommand.ForbidOff,
                action = () =>
                {
                    Map map = spot.Map;
                    if (map == null)
                    {
                        Messages.Message("No map available for Companion Spot.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    CompanionshipVisitorTracker tracker = map.GetComponent<CompanionshipVisitorTracker>();
                    if (tracker == null)
                    {
                        Messages.Message("CompanionshipVisitorTracker component not found on this map.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    int now = Find.TickManager.TicksGame;
                    int delay = CompanionshipTuning.VisitorDesireDelayTicks;
                    int changed = 0;

                    IReadOnlyList<CompanionshipVisitorTracker.VisitorRecord> records = tracker.Records;
                    if (records == null || records.Count == 0)
                    {
                        Messages.Message("No visitor records to reroll.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    for (int i = 0; i < records.Count; i++)
                    {
                        var r = records[i];
                        if (r == null) continue;

                        Pawn v = r.pawn;
                        if (v == null) continue;

                        // Only touch unclaimed / not-in-session visitors.
                        if (r.claimedBy != null) continue;
                        if (tracker.HasActiveSession(v)) continue;

                        // Reset so Tick() will reroll immediately next time it processes.
                        r.wantsDate = false;
                        r.state = CompanionshipVisitorTracker.DateState.None;

                        r.rolled = false;
                        r.spawnedAtTick = now - delay;

                        // Clear backoff so they can be considered right away.
                        r.cooldownUntilTick = -1;

                        changed++;
                    }

                    Messages.Message($"Rerolled desire for {changed} unclaimed visitor(s).", MessageTypeDefOf.TaskCompletion);
                }
            };
        }

        private static Gizmo MakeOverlayToggleGizmo()
        {
            return new Command_Toggle
            {
                defaultLabel = "Companionship: Overlay",
                defaultDesc = "Toggles the Companionship debug overlay (Dev Mode).",
                icon = TexCommand.ForbidOff,
                isActive = () => CompanionshipDebug.ShowOverlay,
                toggleAction = () =>
                {
                    if (CompanionshipMod.Settings == null) return;

                    CompanionshipMod.Settings.enableDebugOverlay = !CompanionshipMod.Settings.enableDebugOverlay;
                    CompanionshipMod.Instance?.WriteSettings();
                }
            };
        }
    }
}
