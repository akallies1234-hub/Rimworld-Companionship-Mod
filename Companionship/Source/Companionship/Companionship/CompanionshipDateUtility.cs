using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    public static class CompanionshipDateUtility
    {
        // Simple in-memory cooldown to prevent job spam loops.
        // Key: pawn.thingIDNumber, Value: tick until which pawn is blocked from dating.
        private static readonly Dictionary<int, int> _dateCooldownUntilTick = new Dictionary<int, int>();

        public static bool IsOnDateCooldown(Pawn p)
        {
            if (p == null) return false;
            if (Find.TickManager == null) return false;

            int until;
            if (_dateCooldownUntilTick.TryGetValue(p.thingIDNumber, out until))
            {
                return Find.TickManager.TicksGame < until;
            }
            return false;
        }

        public static void SetDateCooldown(Pawn p, int ticksFromNow)
        {
            if (p == null) return;
            if (Find.TickManager == null) return;

            int now = Find.TickManager.TicksGame;
            int until = now + ticksFromNow;

            int existing;
            if (_dateCooldownUntilTick.TryGetValue(p.thingIDNumber, out existing))
            {
                if (until > existing)
                    _dateCooldownUntilTick[p.thingIDNumber] = until;
            }
            else
            {
                _dateCooldownUntilTick[p.thingIDNumber] = until;
            }
        }

        public static bool IsValidDateGuest(Pawn guest)
        {
            if (guest == null) return false;
            if (!guest.Spawned) return false;
            if (guest.Dead || guest.Downed) return false;
            if (guest.Faction == null) return false;
            if (guest.Faction == Faction.OfPlayer) return false;
            if (guest.HostileTo(Faction.OfPlayer)) return false;
            if (!guest.RaceProps.Humanlike) return false;
            if (guest.jobs == null) return false;
            if (IsOnDateCooldown(guest)) return false;
            return true;
        }

        public static bool TryFindAvailableCompanionBed(Pawn worker, Pawn guest, out Building_Bed bed)
        {
            bed = null;

            if (worker == null || worker.Map == null) return false;
            if (IsOnDateCooldown(worker)) return false;

            ThingDef bedDef = DefDatabase<ThingDef>.GetNamedSilentFail("Companionship_CompanionBed");
            if (bedDef == null) return false;

            List<Thing> beds = worker.Map.listerThings.ThingsOfDef(bedDef);
            if (beds == null || beds.Count == 0) return false;

            float bestDist = float.MaxValue;
            Building_Bed best = null;

            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed b = beds[i] as Building_Bed;
                if (b == null) continue;
                if (b.DestroyedOrNull()) continue;
                if (!b.Spawned) continue;

                // Need at least 2 slots for lovin.
                if (b.SleepingSlotsCount < 2) continue;

                // Must be empty.
                if (b.AnyOccupants) continue;

                // Reachable by the WORKER.
                if (!worker.CanReach(b, PathEndMode.Touch, Danger.Some)) continue;

                float d = worker.Position.DistanceToSquared(b.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }

            bed = best;
            return bed != null;
        }
    }
}
