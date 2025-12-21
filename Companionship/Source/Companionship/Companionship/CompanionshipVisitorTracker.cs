using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Companionship
{
    public class CompanionshipVisitorTracker : MapComponent
    {
        private const string LogTag = "[Companionship]";

        // Save versioning
        private const int CurrentSaveVersion = 2;
        private int saveVersion = CurrentSaveVersion;

        // Live tuning accessors
        private static int WaitRadius => CompanionshipTuning.WaitRadius;
        private static int DesireDelayTicks => CompanionshipTuning.VisitorDesireDelayTicks;
        private static float DesireChance => CompanionshipTuning.VisitorDesireChance;
        private static int ClaimGraceTicks => CompanionshipTuning.ClaimGraceTicks;
        private static int MaxClaimTicks => CompanionshipTuning.MaxClaimTicks;

        private List<VisitorRecord> records = new List<VisitorRecord>();
        private List<DateSession> activeSessions = new List<DateSession>();

        public IReadOnlyList<VisitorRecord> Records => records;
        public IReadOnlyList<DateSession> ActiveSessions => activeSessions;

        public CompanionshipVisitorTracker(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref saveVersion, "saveVersion", CurrentSaveVersion);
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            Scribe_Collections.Look(ref activeSessions, "activeSessions", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<VisitorRecord>();
                if (activeSessions == null) activeSessions = new List<DateSession>();

                if (saveVersion < 2)
                    saveVersion = CurrentSaveVersion;
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (map == null) return;
            if (Find.TickManager.TicksGame % CompanionshipTuning.TrackerTickIntervalTicks != 0) return;

            CleanupRecords();
            CleanupSessions();

            ProcessVisitors();
            ValidateSessions();
        }

        // ==================================
        // PUBLIC API (WorkGivers / JobDrivers / Gizmos)
        // ==================================

        public Pawn FindEligibleWaitingVisitorNearSpot(Thing spot, Pawn companion)
        {
            if (spot == null || !spot.Spawned) return null;
            if (companion == null || !companion.Spawned) return null;
            if (spot.Map != companion.Map) return null;

            // Only allow companions with the work type enabled to interact with the pool.
            if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(companion))
                return null;

            int now = Find.TickManager.TicksGame;

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r?.pawn == null) continue;

                Pawn v = r.pawn;

                if (!IsOnThisMapAndHumanlike(v)) continue;
                if (!IsValidVisitor(v)) continue;

                if (!r.wantsDate) continue;
                if (r.state != DateState.WaitingNearSpot) continue;

                // Must be close enough to the spot for companions to consider them.
                if (v.Position.DistanceTo(spot.Position) > WaitRadius)
                    continue;

                // Claimed by someone else (within grace) => skip.
                if (r.claimedBy != null && r.claimedBy != companion)
                {
                    if (r.claimedAtTick > 0 && now - r.claimedAtTick <= ClaimGraceTicks)
                        continue;
                }

                if (HasActiveSession(v))
                    continue;

                return v;
            }

            return null;
        }

        public bool TryClaimVisitorForDate(Pawn visitor, Pawn companion)
        {
            if (visitor == null || companion == null) return false;
            if (!IsValidVisitor(visitor)) return false;
            if (!IsValidCompanion(companion)) return false;

            // Hard gate on work toggle.
            if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(companion))
                return false;

            VisitorRecord r = GetOrMakeRecord(visitor);

            if (HasActiveSession(visitor))
                return false;

            int now = Find.TickManager.TicksGame;

            if (!r.wantsDate) return false;
            if (r.state != DateState.WaitingNearSpot) return false;

            if (r.claimedBy != null && r.claimedBy != companion)
            {
                if (r.claimedAtTick > 0 && now - r.claimedAtTick <= ClaimGraceTicks)
                    return false;
            }

            r.claimedBy = companion;
            r.claimedAtTick = now;
            r.state = DateState.Claimed;

            // DateSession ctor already sets startedTick, lastStateChangeTick.
            DateSession s = new DateSession(visitor, companion, GetCompanionSpot(visitor.Map), now);
            s.SetState(DateState.Claimed, now);

            activeSessions.Add(s);

            Dev($"Visitor claimed: {visitor.LabelShortCap} by {companion.LabelShortCap}");
            return true;
        }

        public void ReleaseClaim(Pawn visitor, Pawn companion)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;

            if (r.claimedBy != companion)
                return;

            // Prefer ending the session cleanly if one exists.
            if (TryEndSession(visitor, DateEndReason.Released))
            {
                Dev($"Claim released: {visitor?.LabelShortCap ?? "null"} by {companion?.LabelShortCap ?? "null"}");
                return;
            }

            r.claimedBy = null;
            r.claimedAtTick = 0;
            r.state = r.wantsDate ? DateState.WaitingNearSpot : DateState.None;

            Dev($"Claim released (no session found): {visitor?.LabelShortCap ?? "null"} by {companion?.LabelShortCap ?? "null"}");
        }

        public bool HasActiveSession(Pawn visitor)
        {
            return GetActiveSessionForVisitor(visitor) != null;
        }

        public bool TryEndSession(Pawn visitor, DateEndReason reason)
        {
            if (visitor == null) return false;

            DateSession s = GetActiveSessionForVisitor(visitor);
            if (s == null) return false;

            EndSessionInternal(s, visitor, reason);
            return true;
        }

        public void SetVisitorState(Pawn visitor, DateState newState)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;

            r.state = newState;

            DateSession s = GetActiveSessionForVisitor(visitor);
            if (s != null && !s.ended)
            {
                s.SetState(newState, Find.TickManager.TicksGame);
            }
        }

        public void BindSessionBed(Pawn visitor, Building_Bed bed)
        {
            DateSession s = GetActiveSessionForVisitor(visitor);
            if (s == null) return;

            s.bed = bed;
        }

        public bool TryGetSessionBed(Pawn visitor, out Building_Bed bed)
        {
            bed = null;

            DateSession s = GetActiveSessionForVisitor(visitor);
            if (s == null) return false;

            bed = s.bed;
            return bed != null;
        }

        public void CompleteDate(Pawn visitor, Pawn companion)
        {
            VisitorRecord r = GetRecord(visitor);
            if (r == null) return;

            TryEndSession(visitor, DateEndReason.Success);

            r.wantsDate = false;
            r.rolled = true;

            r.claimedBy = null;
            r.claimedAtTick = 0;
            r.state = DateState.None;

            Dev($"Date complete: {visitor?.LabelShortCap ?? "null"}");
        }

        // =========================
        // Update loop helpers
        // =========================

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
                if (p.DestroyedOrNull() || p.Dead || !p.Spawned || p.Map != map)
                {
                    records.RemoveAt(i);
                    continue;
                }

                if (!IsValidVisitor(p))
                {
                    records.RemoveAt(i);
                    continue;
                }
            }
        }

        private void CleanupSessions()
        {
            for (int i = activeSessions.Count - 1; i >= 0; i--)
            {
                DateSession s = activeSessions[i];
                if (s == null || s.ended)
                {
                    activeSessions.RemoveAt(i);
                    continue;
                }

                if (s.visitor == null || s.visitor.DestroyedOrNull() || !s.visitor.Spawned || s.visitor.Map != map)
                {
                    EndSessionInternal(s, s.visitor, DateEndReason.VisitorInvalid);
                    continue;
                }
            }
        }

        private void ProcessVisitors()
        {
            if (map == null) return;

            IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null) return;

            int now = Find.TickManager.TicksGame;
            Thing spot = GetCompanionSpot(map);

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (!IsValidVisitor(p)) continue;

                VisitorRecord r = GetRecord(p);
                if (r == null)
                {
                    r = new VisitorRecord
                    {
                        pawn = p,
                        spawnedAtTick = now
                    };
                    records.Add(r);
                }

                if (r.state == DateState.Cooldown && r.cooldownUntilTick > 0 && now >= r.cooldownUntilTick)
                {
                    r.cooldownUntilTick = 0;
                    r.state = r.wantsDate ? DateState.WaitingNearSpot : DateState.None;
                }

                // Roll desire once after delay
                if (!r.rolled)
                {
                    if (now - r.spawnedAtTick >= DesireDelayTicks)
                    {
                        r.rolled = true;

                        float chance = Mathf.Clamp01(DesireChance);
                        if (Rand.Value < chance)
                        {
                            r.wantsDate = true;
                            r.state = DateState.WaitingNearSpot;
                        }
                        else
                        {
                            r.wantsDate = false;
                            r.state = DateState.None;
                        }
                    }
                }

                // Pull wantsDate visitors toward the spot using the existing loiter-forcing behavior.
                if (r.rolled && r.wantsDate && r.state == DateState.WaitingNearSpot)
                {
                    EnsureVisitorLoitering(p, r, spot, now);
                }
            }
        }

        private void EnsureVisitorLoitering(Pawn visitor, VisitorRecord r, Thing spot, int now)
        {
            if (visitor == null || r == null) return;
            if (spot == null || !spot.Spawned) return;

            // Don’t fight a claimed visitor or an active session.
            if (r.claimedBy != null) return;
            if (HasActiveSession(visitor)) return;

            // If they’re already in a pipeline job other than loitering, don’t interfere.
            if (IsVisitorInDateJob(visitor) && visitor.CurJobDef != CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot)
                return;

            // Already loitering => done.
            if (visitor.CurJobDef == CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot)
                return;

            int cd = CompanionshipTuning.VisitorForceJobCooldownTicks;
            if (cd < 1) cd = 500;

            if (r.lastForceJobTick >= 0 && now - r.lastForceJobTick < cd)
                return;

            if (!visitor.Awake() || visitor.Downed || visitor.InMentalState)
                return;

            if (!visitor.CanReach(spot, PathEndMode.Touch, Danger.Some))
            {
                // Back off for a while if unreachable.
                r.cooldownUntilTick = now + CompanionshipTuning.VisitorRetryCooldownTicks;
                r.lastForceJobTick = now;
                return;
            }

            if (visitor.jobs == null)
                return;

            Job j = JobMaker.MakeJob(CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot, spot);
            j.ignoreForbidden = true;

            int expiry = CompanionshipTuning.VisitorLoiterExpiryTicks;
            if (expiry > 0) j.expiryInterval = expiry;

            visitor.jobs.StartJob(j, JobCondition.InterruptForced);

            r.lastForceJobTick = now;
        }

        private void ValidateSessions()
        {
            int now = Find.TickManager.TicksGame;

            for (int i = activeSessions.Count - 1; i >= 0; i--)
            {
                DateSession s = activeSessions[i];
                if (s == null)
                {
                    activeSessions.RemoveAt(i);
                    continue;
                }

                if (s.ended)
                {
                    activeSessions.RemoveAt(i);
                    continue;
                }

                Pawn visitor = s.visitor;
                Pawn companion = s.companion;

                if (!IsOnThisMapAndHumanlike(visitor) || !IsValidVisitor(visitor))
                {
                    EndSessionInternal(s, visitor, DateEndReason.VisitorInvalid);
                    continue;
                }

                if (!IsOnThisMapAndHumanlike(companion) || !IsValidCompanion(companion))
                {
                    EndSessionInternal(s, visitor, DateEndReason.CompanionInvalid);
                    continue;
                }

                // Step 9: end cleanly if work toggle was disabled mid-session.
                if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(companion))
                {
                    EndSessionInternal(s, visitor, DateEndReason.CompanionWorkDisabled);
                    continue;
                }

                VisitorRecord r = GetRecord(visitor);
                if (r == null)
                {
                    EndSessionInternal(s, visitor, DateEndReason.Unknown);
                    continue;
                }

                // Claim timeout
                if (r.state == DateState.Claimed && r.claimedAtTick > 0 && now - r.claimedAtTick > MaxClaimTicks)
                {
                    EndSessionInternal(s, visitor, DateEndReason.Timeout);
                    continue;
                }

                // Bed validation (if bound)
                if (s.bed != null)
                {
                    if (!IsBedStillValidForSession(s.bed, companion, visitor))
                    {
                        EndSessionInternal(s, visitor, DateEndReason.BedInvalid);
                        continue;
                    }
                }

                // Pipeline validation
                if (s.state == DateState.Greeting || s.state == DateState.EscortingToBed || s.state == DateState.AtBed)
                {
                    bool companionInPipeline =
                        companion.CurJobDef == CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed ||
                        companion.CurJobDef == CompanionshipDefOf.Companionship_CustomLovin;

                    if (!companionInPipeline)
                    {
                        EndSessionInternal(s, visitor, DateEndReason.CompanionNotInPipeline);
                        continue;
                    }

                    if (!IsVisitorInDateJob(visitor))
                    {
                        EndSessionInternal(s, visitor, DateEndReason.VisitorPulledFromPipeline);
                        continue;
                    }
                }
            }
        }

        // =========================
        // Session end + cleanup
        // =========================

        private static bool IsVisitorInDateJob(Pawn visitor)
        {
            JobDef jd = visitor?.CurJobDef;
            return jd == CompanionshipDefOf.Companionship_VisitorLoiterAtCompanionSpot ||
                   jd == CompanionshipDefOf.Companionship_VisitorParticipateGreeting ||
                   jd == CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed ||
                   jd == CompanionshipDefOf.Companionship_CustomLovinPartner;
        }

        private void EndSessionInternal(DateSession session, Pawn visitor, DateEndReason reason)
        {
            if (session == null) return;

            int now = Find.TickManager.TicksGame;

            session.End(reason, now);

            // Rewards only for success
            if (reason == DateEndReason.Success || reason == DateEndReason.Completed)
            {
                CompanionshipRewardsUtility.OnSuccessfulDate(session.visitor, session.companion, session);
            }

            VisitorRecord r = (visitor != null) ? GetRecord(visitor) : null;
            if (r != null)
            {
                r.lastSessionEndTick = now;
                r.lastSessionEndReason = reason;

                r.claimedBy = null;
                r.claimedAtTick = 0;

                if (reason != DateEndReason.Success && r.wantsDate && IsOnThisMapAndHumanlike(visitor))
                {
                    r.cooldownUntilTick = now + CompanionshipTuning.VisitorRetryCooldownTicks;
                    r.state = DateState.Cooldown;
                }
                else
                {
                    if (reason != DateEndReason.Success)
                        r.state = (r.wantsDate && IsOnThisMapAndHumanlike(visitor)) ? DateState.WaitingNearSpot : DateState.None;
                }
            }

            CleanupPawnsAfterSessionEnd(session, visitor, reason);

            Dev($"Session ended: visitor={(visitor != null ? visitor.LabelShortCap : "null")} reason={reason}");

            for (int i = activeSessions.Count - 1; i >= 0; i--)
            {
                if (activeSessions[i] == session)
                {
                    activeSessions.RemoveAt(i);
                    break;
                }
            }
        }

        private void CleanupPawnsAfterSessionEnd(DateSession session, Pawn visitor, DateEndReason reason)
        {
            if (session == null) return;
            if (reason == DateEndReason.Success) return;

            Pawn v = visitor ?? session.visitor;
            Pawn c = session.companion;

            if (v != null && v.Spawned && v.Map == map && v.jobs != null)
            {
                JobDef jd = v.CurJobDef;
                if (jd == CompanionshipDefOf.Companionship_VisitorParticipateGreeting ||
                    jd == CompanionshipDefOf.Companionship_VisitorFollowCompanionToBed ||
                    jd == CompanionshipDefOf.Companionship_CustomLovinPartner)
                {
                    InterruptPawnWithShortWait(v);
                }
            }

            if (c != null && c.Spawned && c.Map == map && c.jobs != null)
            {
                JobDef jd = c.CurJobDef;
                if (jd == CompanionshipDefOf.Companionship_CompanionGreetAndEscortToBed ||
                    jd == CompanionshipDefOf.Companionship_CustomLovin)
                {
                    InterruptPawnWithShortWait(c);
                }
            }
        }

        private static void InterruptPawnWithShortWait(Pawn p)
        {
            try
            {
                if (p?.jobs == null) return;

                Job j = JobMaker.MakeJob(JobDefOf.Wait, p.Position);
                j.expiryInterval = 60;
                j.checkOverrideOnExpire = true;

                p.jobs.StartJob(j, JobCondition.InterruptForced);
            }
            catch
            {
                // Intentionally ignore
            }
        }

        // =========================
        // Debug helpers used by gizmos
        // =========================

        public string GetDebugSummary()
        {
            int now = Find.TickManager.TicksGame;

            int waiting = 0;
            int wants = 0;
            int claimed = 0;
            int cooldown = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null) continue;

                if (r.wantsDate) wants++;
                if (r.state == DateState.WaitingNearSpot) waiting++;
                if (r.state == DateState.Claimed) claimed++;
                if (r.state == DateState.Cooldown) cooldown++;
            }

            return
                $"{LogTag} Map={map}\n" +
                $"Records={records.Count} Wants={wants} Waiting={waiting} Claimed={claimed} Cooldown={cooldown}\n" +
                $"ActiveSessions={activeSessions.Count}\n" +
                $"Tick={now}";
        }

        public string GetDebugReport()
        {
            int now = Find.TickManager.TicksGame;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{LogTag} VisitorTracker report ({map}):");
            sb.AppendLine($"Records: {records.Count}");
            sb.AppendLine($"ActiveSessions: {activeSessions.Count}");
            sb.AppendLine();

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r == null) continue;

                Pawn p = r.pawn;
                sb.AppendLine(RecordDebugLine(r, p, now));
            }

            sb.AppendLine();
            sb.AppendLine("Sessions:");
            for (int i = 0; i < activeSessions.Count; i++)
            {
                DateSession s = activeSessions[i];
                if (s == null) continue;
                sb.AppendLine(SessionDebugLine(s, now));
            }

            return sb.ToString();
        }

        public string BuildOverlayLabel()
        {
            return GetDebugSummary();
        }

        // =========================
        // Internal helpers
        // =========================

        private static bool IsValidCompanion(Pawn p)
        {
            if (p == null || !p.Spawned || p.Destroyed || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            if (p.Faction != Faction.OfPlayer) return false;
            if (p.Downed || p.InMentalState) return false;
            return true;
        }

        /// <summary>
        /// IMPORTANT CHANGE:
        /// Previously required: p.guest != null AND p.guest.HostFaction == Faction.OfPlayer.
        /// In vanilla, trade caravans / visitors often don't satisfy HostFaction == player consistently,
        /// and dev-spawned groups can fail it entirely.
        ///
        /// Now:
        /// - Still requires non-hostile, humanlike, non-player pawn.
        /// - Accepts "guest-like" pawns (guest tracker present & not prisoner) WITHOUT requiring HostFaction.
        /// - Also accepts pawns belonging to a vanilla visiting/trading lord job even if guest is null.
        /// </summary>
        private static bool IsValidVisitor(Pawn p)
        {
            if (p == null || !p.Spawned || p.Destroyed || p.Dead) return false;
            if (p.RaceProps == null || !p.RaceProps.Humanlike) return false;

            if (p.Faction == null) return false;
            if (p.Faction == Faction.OfPlayer) return false;
            if (p.Faction.HostileTo(Faction.OfPlayer)) return false;

            // Primary path: true guests (but do NOT require HostFaction == player)
            bool guestLike = false;
            if (p.guest != null)
            {
                if (p.guest.IsPrisoner) return false;
                guestLike = true;
            }

            // Fallback path: trade/visit groups via LordJob name (no hard type dependency)
            if (!guestLike)
            {
                Lord lord = p.GetLord();
                if (lord?.LordJob != null)
                {
                    string lj = lord.LordJob.GetType().Name;

                    // Common vanilla lord jobs for caravans/visitors
                    if (lj == "LordJob_TradeWithColony" ||
                        lj == "LordJob_VisitColony" ||
                        lj == "LordJob_TradeWithColonyAndExit" ||
                        lj == "LordJob_VisitColonyAndExit")
                    {
                        guestLike = true;
                    }
                }
            }

            if (!guestLike) return false;

            if (p.IsSlave) return false;

            if (p.Downed) return false;
            if (p.InMentalState) return false;

            return true;
        }

        private bool IsOnThisMapAndHumanlike(Pawn p)
        {
            if (p == null) return false;
            if (p.DestroyedOrNull() || p.Dead) return false;
            if (!p.Spawned) return false;
            if (p.Map != map) return false;
            if (p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            return true;
        }

        private Thing GetCompanionSpot(Map m)
        {
            if (m == null) return null;

            List<Thing> list = m.listerThings.ThingsOfDef(CompanionshipDefOf.Companionship_CompanionSpot);
            return (list != null && list.Count > 0) ? list[0] : null;
        }

        private bool IsBedStillValidForSession(Building_Bed bed, Pawn companion, Pawn visitor)
        {
            if (bed == null) return false;
            if (bed.Destroyed || !bed.Spawned) return false;
            if (bed.Map != map) return false;
            if (bed.IsBurning()) return false;

            if (bed.SleepingSlotsCount < 2) return false;

            int slots = bed.SleepingSlotsCount;
            for (int i = 0; i < slots; i++)
            {
                Pawn occ = bed.GetCurOccupant(i);
                if (occ == null) continue;

                if (occ == companion || occ == visitor) continue;

                return false;
            }

            if (visitor == null || visitor.Destroyed || !visitor.Spawned || visitor.Map != map)
                return false;

            if (companion == null || companion.Destroyed || !companion.Spawned || companion.Map != map)
                return false;

            if (!companion.CanReserve(bed))
                return false;

            return true;
        }

        private string RecordDebugLine(VisitorRecord r, Pawn p, int now)
        {
            if (r == null) return "null record";
            string n = p != null ? p.LabelShortCap : "null";
            string claim = (r.claimedBy != null) ? r.claimedBy.LabelShortCap : "none";
            int claimAge = (r.claimedAtTick > 0) ? (now - r.claimedAtTick) : -1;
            int cdLeft = (r.cooldownUntilTick > 0) ? (r.cooldownUntilTick - now) : 0;

            return $"{n} state={r.state} wants={r.wantsDate} rolled={r.rolled} claimedBy={claim} claimAge={claimAge} cooldownLeft={cdLeft} lastEnd={r.lastSessionEndReason}";
        }

        private string SessionDebugLine(DateSession s, int now)
        {
            if (s == null) return "null session";
            string v = s.visitor != null ? s.visitor.LabelShortCap : "null";
            string c = s.companion != null ? s.companion.LabelShortCap : "null";
            string bed = s.bed != null ? s.bed.LabelShortCap : "none";
            return $"session visitor={v} companion={c} state={s.state} bed={bed} started={s.startedTick} age={(now - s.startedTick)} ended={s.ended} reason={s.endReason}";
        }

        private VisitorRecord GetRecord(Pawn p)
        {
            if (p == null) return null;

            for (int i = 0; i < records.Count; i++)
            {
                VisitorRecord r = records[i];
                if (r == null) continue;
                if (r.pawn == p) return r;
            }

            return null;
        }

        private VisitorRecord GetOrMakeRecord(Pawn p)
        {
            VisitorRecord r = GetRecord(p);
            if (r != null) return r;

            r = new VisitorRecord
            {
                pawn = p,
                spawnedAtTick = Find.TickManager.TicksGame
            };
            records.Add(r);
            return r;
        }

        private DateSession GetActiveSessionForVisitor(Pawn visitor)
        {
            if (visitor == null) return null;

            for (int i = 0; i < activeSessions.Count; i++)
            {
                DateSession s = activeSessions[i];
                if (s == null) continue;
                if (s.ended) continue;
                if (s.visitor == visitor) return s;
            }

            return null;
        }

        private static void Dev(string msg)
        {
            if (!CompanionshipDebug.VerboseLogging) return;
            Log.Message($"{LogTag} {msg}");
        }

        // =========================
        // Types
        // =========================

        public enum DateState : byte
        {
            None = 0,
            WaitingNearSpot = 1,
            Claimed = 2,

            Greeting = 3,

            // Keep both names for backward compat.
            Escorting = 4,
            EscortingToBed = 4,

            AtBed = 5,
            Cooldown = 6
        }

        public class VisitorRecord : IExposable
        {
            public Pawn pawn;

            public int spawnedAtTick;
            public bool rolled;
            public bool wantsDate;

            public DateState state;

            public Pawn claimedBy;
            public int claimedAtTick;

            public int cooldownUntilTick;

            public int lastForceJobTick;

            public int lastSessionEndTick;
            public DateEndReason lastSessionEndReason;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");

                Scribe_Values.Look(ref spawnedAtTick, "spawnedAtTick", 0);
                Scribe_Values.Look(ref rolled, "rolled", false);
                Scribe_Values.Look(ref wantsDate, "wantsDate", false);

                Scribe_Values.Look(ref state, "state", DateState.None);

                Scribe_References.Look(ref claimedBy, "claimedBy");
                Scribe_Values.Look(ref claimedAtTick, "claimedAtTick", 0);

                Scribe_Values.Look(ref cooldownUntilTick, "cooldownUntilTick", 0);

                Scribe_Values.Look(ref lastForceJobTick, "lastForceJobTick", -1);

                Scribe_Values.Look(ref lastSessionEndTick, "lastSessionEndTick", -1);
                Scribe_Values.Look(ref lastSessionEndReason, "lastSessionEndReason", DateEndReason.None);
            }
        }
    }
}
