using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Companionship
{
    public class CompanionshipVisitorTracker : MapComponent
    {
        public const int WaitRadius = 7;

        private const int DesireDelayTicks = 4 * GenDate.TicksPerHour;
        private const float DesireChance = 0.35f;

        private const int ClaimGraceTicks = 1000;

        private List<VisitorRecord> records = new List<VisitorRecord>();

        private bool loggedCreated;

        public CompanionshipVisitorTracker(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Prefs.DevMode && !loggedCreated)
            {
                loggedCreated = true;
                Log.Message($"[Companionship] VisitorTracker created for map id={map?.uniqueID.ToString() ?? "null"}");
            }

            if (Find.TickManager.TicksGame % 250 != 0)
                return;

            CleanupRecords();
            DiscoverNewVisitors();
            ProcessVisitors();
            ValidateClaims();
        }

        public Pawn FindEligibleWaitingVisitorNearSpot(Thing spot, Pawn companion)
        {
            if (spot == null || !spot.Spawned || spot.Map != map)
                return null;

            int radiusSq = WaitRadius * WaitRadius;

            Pawn best = null;
            int bestDistSq = int.MaxValue;

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r == null || !r.wantsDate) continue;
                if (r.state != DateState.WaitingNearSpot) continue;
                if (r.claimedBy != null) continue;

                Pawn p = r.pawn;
                if (!IsOnMapAndValid(p)) continue;

                if (p.CurJobDef != CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot)
                    continue;

                int dSpot = p.Position.DistanceToSquared(spot.Position);
                if (dSpot > radiusSq) continue;

                if (p.Downed || p.InMentalState) continue;
                if (p.Faction == null || p.Faction.HostileTo(Faction.OfPlayer)) continue;

                int distSqToCompanion = companion != null ? p.Position.DistanceToSquared(companion.Position) : dSpot;
                if (distSqToCompanion < bestDistSq)
                {
                    bestDistSq = distSqToCompanion;
                    best = p;
                }
            }

            return best;
        }

        public bool TryClaimVisitorForDate(Pawn visitor, Pawn companion)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return false;

            if (!r.wantsDate) return false;
            if (r.state != DateState.WaitingNearSpot) return false;
            if (r.claimedBy != null && r.claimedBy != companion) return false;

            r.claimedBy = companion;
            r.claimedAtTick = Find.TickManager.TicksGame;
            r.state = DateState.Greeting;
            return true;
        }

        public void SetVisitorState(Pawn visitor, DateState state)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;
            r.state = state;
        }

        public void ReleaseClaim(Pawn visitor, Pawn companion)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;

            if (r.claimedBy == companion)
            {
                r.claimedBy = null;
                r.claimedAtTick = 0;

                if (r.wantsDate && IsOnMapAndValid(visitor))
                    r.state = DateState.WaitingNearSpot;
                else
                    r.state = DateState.None;
            }
        }

        /// <summary>
        /// Called when the custom lovin finishes successfully.
        /// Marks date complete so visitor stops returning to the spot.
        /// </summary>
        public void CompleteDate(Pawn visitor, Pawn companion)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;

            // Clear desire so they don't re-enter the queue.
            r.wantsDate = false;
            r.rolled = true;

            // Clear claim
            if (r.claimedBy == companion)
                r.claimedBy = null;

            r.claimedAtTick = 0;
            r.state = DateState.None;

            if (Prefs.DevMode)
                Log.Message($"[Companionship] Date complete: {visitor?.LabelShortCap ?? "null"}");
        }

        private void CleanupRecords()
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                VisitorRecord r = records[i];
                if (r == null || r.pawn == null)
                {
                    records.RemoveAt(i);
                    continue;
                }

                Pawn p = r.pawn;
                if (!p.Spawned || p.Destroyed || p.Map != map || !IsValidVisitor(p))
                {
                    records.RemoveAt(i);
                }
            }
        }

        private void DiscoverNewVisitors()
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            int now = Find.TickManager.TicksGame;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (!IsValidVisitor(p)) continue;
                if (GetRecord(p) != null) continue;

                records.Add(new VisitorRecord
                {
                    pawn = p,
                    firstSeenTick = now,
                    rolled = false,
                    wantsDate = false,
                    lastForceJobTick = 0,
                    claimedBy = null,
                    claimedAtTick = 0,
                    state = DateState.None
                });

                if (Prefs.DevMode)
                    Log.Message($"[Companionship] Tracking new visitor: {p.LabelShortCap} (lord={p.GetLord()?.LordJob?.GetType().Name ?? "null"})");
            }
        }

        private void ProcessVisitors()
        {
            Thing spot = GetCompanionSpot();
            if (spot == null || !spot.Spawned)
                return;

            int now = Find.TickManager.TicksGame;

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                Pawn p = r.pawn;

                if (!IsOnMapAndValid(p))
                    continue;

                if (!r.rolled && now - r.firstSeenTick >= DesireDelayTicks)
                {
                    r.rolled = true;
                    r.wantsDate = Rand.ChanceSeeded(DesireChance, p.thingIDNumber ^ r.firstSeenTick);
                    r.state = r.wantsDate ? DateState.WaitingNearSpot : DateState.None;

                    if (Prefs.DevMode)
                        Log.Message($"[Companionship] Desire roll: {p.LabelShortCap} wantsDate={r.wantsDate}");
                }

                if (!r.wantsDate)
                    continue;

                if (r.state == DateState.WaitingNearSpot && r.claimedBy == null)
                {
                    if (p.CurJobDef != CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot &&
                        now - r.lastForceJobTick >= 500 &&
                        !p.Downed && !p.InMentalState)
                    {
                        Job job = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot, spot);
                        job.ignoreForbidden = true;
                        job.expiryInterval = 12 * GenDate.TicksPerHour;

                        bool taken = p.jobs.TryTakeOrderedJob(job);
                        if (!taken)
                            p.jobs.StartJob(job, JobCondition.InterruptForced);

                        r.lastForceJobTick = now;

                        if (Prefs.DevMode)
                            Log.Message($"[Companionship] Forcing loiter job: {p.LabelShortCap} -> CompanionSpot");
                    }
                }
            }
        }

        private void ValidateClaims()
        {
            int now = Find.TickManager.TicksGame;

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r == null) continue;
                if (r.claimedBy == null) continue;

                Pawn visitor = r.pawn;
                Pawn companion = r.claimedBy;

                if (!IsOnMapAndValid(visitor) || companion == null || companion.Destroyed || !companion.Spawned || companion.Map != map)
                {
                    r.claimedBy = null;
                    r.claimedAtTick = 0;
                    r.state = (r.wantsDate && IsOnMapAndValid(visitor)) ? DateState.WaitingNearSpot : DateState.None;
                    continue;
                }

                if (now - r.claimedAtTick > ClaimGraceTicks)
                {
                    if (companion.CurJobDef != CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed &&
                        companion.CurJobDef != CompanionshipDefOf.Companionship_CustomLovin)
                    {
                        r.claimedBy = null;
                        r.claimedAtTick = 0;
                        r.state = DateState.WaitingNearSpot;
                    }
                }
            }
        }

        private Thing GetCompanionSpot()
        {
            List<Thing> list = map.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            return (list != null && list.Count > 0) ? list[0] : null;
        }

        private VisitorRecord GetRecord(Pawn p)
        {
            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r != null && r.pawn == p)
                    return r;
            }
            return null;
        }

        private static bool IsOnMapAndValid(Pawn p)
        {
            return p != null && p.Spawned && !p.Destroyed && p.RaceProps != null && p.RaceProps.Humanlike;
        }

        private static bool IsValidVisitor(Pawn p)
        {
            if (!IsOnMapAndValid(p)) return false;

            if (p.Faction == null) return false;
            if (p.Faction == Faction.OfPlayer) return false;
            if (p.Faction.HostileTo(Faction.OfPlayer)) return false;

            if (p.guest != null && p.guest.HostFaction == Faction.OfPlayer)
            {
                if (p.guest.IsPrisoner) return false;
                if (p.IsSlave) return false;

                if (p.guest.GuestStatus == GuestStatus.Guest)
                    return true;
            }

            Lord lord = p.GetLord();
            if (lord?.LordJob is LordJob_VisitColony) return true;
            if (lord?.LordJob is LordJob_TradeWithColony) return true;

            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "companionshipVisitorRecords", LookMode.Deep);
        }

        public enum DateState : byte
        {
            None = 0,
            WaitingNearSpot = 1,
            Greeting = 2,
            EscortingToBed = 3,
            AtBed = 4
        }

        public class VisitorRecord : IExposable
        {
            public Pawn pawn;
            public int firstSeenTick;

            public bool rolled;
            public bool wantsDate;

            public int lastForceJobTick;

            public Pawn claimedBy;
            public int claimedAtTick;
            public DateState state;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref firstSeenTick, "firstSeenTick");

                Scribe_Values.Look(ref rolled, "rolled");
                Scribe_Values.Look(ref wantsDate, "wantsDate");

                Scribe_Values.Look(ref lastForceJobTick, "lastForceJobTick");

                Scribe_References.Look(ref claimedBy, "claimedBy");
                Scribe_Values.Look(ref claimedAtTick, "claimedAtTick");
                Scribe_Values.Look(ref state, "state");
            }
        }
    }
}
