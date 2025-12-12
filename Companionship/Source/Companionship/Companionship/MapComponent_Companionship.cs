using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Companionship
{
    /// <summary>
    /// Tracks visitors and (prototype) decides who wants companionship.
    /// Assigns the waiting job; WorkGiver is gated so colonists only react once visitor is near the spot.
    /// </summary>
    public class MapComponent_Companionship : MapComponent
    {
        private List<Pawn> waitingVisitors = new List<Pawn>();
        private HashSet<int> rolledVisitorIds = new HashSet<int>();

        // Track when we first saw each visitor on the map (thingIDNumber -> firstSeenTick)
        private Dictionary<int, int> firstSeenTickByVisitorId = new Dictionary<int, int>();

        private const int ScanIntervalTicks = 250;

        // RimWorld: 60,000 ticks/day => 2,500 ticks/hour
        private const int DesireDelayTicks = 10000; // 4 in-game hours

        private const float DesireChance = 0.35f;

        public MapComponent_Companionship(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref waitingVisitors, "waitingVisitors", LookMode.Reference);
            Scribe_Collections.Look(ref rolledVisitorIds, "rolledVisitorIds", LookMode.Value);

            Scribe_Collections.Look(ref firstSeenTickByVisitorId, "firstSeenTickByVisitorId",
                LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
                if (rolledVisitorIds == null) rolledVisitorIds = new HashSet<int>();
                if (firstSeenTickByVisitorId == null) firstSeenTickByVisitorId = new Dictionary<int, int>();

                waitingVisitors.RemoveAll(p => p == null || p.DestroyedOrNull());
            }
        }

        public IEnumerable<Pawn> WaitingVisitors
        {
            get
            {
                if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
                waitingVisitors.RemoveAll(p => p == null || p.DestroyedOrNull() || !p.Spawned || p.Map != map);
                return waitingVisitors;
            }
        }

        public bool IsWaiting(Pawn visitor)
        {
            if (visitor == null) return false;
            if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
            return waitingVisitors.Contains(visitor);
        }

        public void ClearWaiting(Pawn visitor)
        {
            if (visitor == null) return;
            if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
            waitingVisitors.Remove(visitor);
        }

        public bool TryGetCompanionSpot(out Thing spot)
        {
            spot = null;
            var spots = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            if (spots == null || spots.Count == 0) return false;
            spot = spots[0];
            return spot != null;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % ScanIntervalTicks != 0)
                return;

            TryAssignWaitingJobs();
            CleanupWaitingListAndTracking();
        }

        private void TryAssignWaitingJobs()
        {
            Thing spot;
            if (!TryGetCompanionSpot(out spot)) return;

            int now = Find.TickManager.TicksGame;

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsEligibleVisitor(p))
                    continue;

                int id = p.thingIDNumber;

                // Record first-seen time
                if (!firstSeenTickByVisitorId.ContainsKey(id))
                    firstSeenTickByVisitorId[id] = now;

                // Delay desire roll
                int firstSeen = firstSeenTickByVisitorId[id];
                if (now - firstSeen < DesireDelayTicks)
                    continue;

                // Only roll once
                if (rolledVisitorIds.Contains(id))
                    continue;

                rolledVisitorIds.Add(id);

                if (!Rand.Chance(DesireChance))
                    continue;

                TryStartWaiting(p, spot);
            }
        }

        private void TryStartWaiting(Pawn visitor, Thing spot)
        {
            if (visitor == null || spot == null) return;
            if (IsWaiting(visitor)) return;
            if (visitor.Downed || visitor.Dead || visitor.InMentalState) return;

            Job waitJob = JobMaker.MakeJob(CompanionshipDefOf.Companionship_WaitAtCompanionSpot, spot);
            waitJob.expiryInterval = 60000; // ~1 day

            bool started = visitor.jobs != null && visitor.jobs.TryTakeOrderedJob(waitJob, JobTag.Misc);
            if (started)
            {
                if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
                waitingVisitors.Add(visitor);
            }
        }

        private bool IsEligibleVisitor(Pawn p)
        {
            if (p == null) return false;
            if (!p.Spawned || p.Map != map) return false;
            if (p.Dead) return false;
            if (!p.RaceProps.Humanlike) return false;

            if (p.Faction == Faction.OfPlayer) return false;
            if (p.IsPrisoner) return false;
            if (p.IsSlave) return false;

            if (p.HostileTo(Faction.OfPlayer)) return false;

            return true;
        }

        private void CleanupWaitingListAndTracking()
        {
            if (waitingVisitors == null) waitingVisitors = new List<Pawn>();
            if (firstSeenTickByVisitorId == null) firstSeenTickByVisitorId = new Dictionary<int, int>();

            waitingVisitors.RemoveAll(v =>
                v == null ||
                v.DestroyedOrNull() ||
                !v.Spawned ||
                v.Map != map ||
                v.Dead ||
                v.HostileTo(Faction.OfPlayer) ||
                v.CurJobDef != CompanionshipDefOf.Companionship_WaitAtCompanionSpot);

            var keysToRemove = new List<int>();
            foreach (var kvp in firstSeenTickByVisitorId)
            {
                int id = kvp.Key;
                bool stillHere = false;

                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p != null && p.thingIDNumber == id)
                    {
                        stillHere = true;
                        break;
                    }
                }

                if (!stillHere)
                    keysToRemove.Add(id);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                int id = keysToRemove[i];
                firstSeenTickByVisitorId.Remove(id);
                rolledVisitorIds.Remove(id);
            }
        }
    }
}
