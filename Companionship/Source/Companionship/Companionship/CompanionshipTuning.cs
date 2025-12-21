using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Centralized tuning values for Companionship.
    ///
    /// IMPORTANT:
    /// - CompanionshipSetting.cs (file) references Default* values (compile-time constants) for UI defaults.
    /// - The rest of the code references the effective values (TrackerTickIntervalTicks, WaitRadius, etc.).
    ///
    /// This file therefore provides BOTH:
    /// - Default constants
    /// - Effective values that read from settings (if present) with fallback to defaults
    /// </summary>
    public static class CompanionshipTuning
    {
        // =========================
        // DEFAULTS (used by settings UI)
        // =========================

        public const int DefaultTrackerTickIntervalTicks = 60;

        public const int DefaultWaitRadius = 7;

        // Default: 4 in-game hours between desire rolls (felt more natural than 1 hour).
        public const int DefaultVisitorDesireDelayTicks = 4 * GenDate.TicksPerHour;
        public const float DefaultVisitorDesireChance = 0.35f;

        public const int DefaultVisitorRetryCooldownTicks = 2 * GenDate.TicksPerHour;

        public const int DefaultVisitorForceJobCooldownTicks = 500;

        public const int DefaultVisitorLoiterExpiryTicks = 8 * GenDate.TicksPerHour;

        public const int DefaultGreetingDurationTicks = 1 * GenDate.TicksPerHour;
        public const int DefaultGreetingChitchatIntervalTicks = 600;
        public const int DefaultVisitorGreetingExpiryPaddingTicks = 120;

        public const int DefaultClaimGraceTicks = 1 * GenDate.TicksPerHour;
        public const int DefaultMaxClaimTicks = 8 * GenDate.TicksPerHour;

        public const int DefaultVisitorFollowJobExpiryTicks = 6 * GenDate.TicksPerHour;
        public const int DefaultVisitorFollowWaitDurationTicks = 1 * GenDate.TicksPerHour;

        public const int DefaultCompanionIdleAtSpotDurationTicks = 1 * GenDate.TicksPerHour;
        public const int DefaultCompanionGreetAndEscortJobExpiryTicks = 8 * GenDate.TicksPerHour;

        public const int DefaultLovinDurationTicks = 2 * GenDate.TicksPerHour;
        public const int DefaultLovinJobExpiryTicks = 3 * GenDate.TicksPerHour;
        public const int DefaultLovinPartnerExpiryPaddingTicks = 600;
        public const int DefaultLovinHeartFleckIntervalTicks = 250;

        // Newer tuning knobs (not in settings yet, but centralized here)
        public const int DefaultVisitorLoiterMoveIntervalMinTicks = 180;
        public const int DefaultVisitorLoiterMoveIntervalMaxTicks = 420;

        // Companion waits at bed for visitor before aborting escort → lovin handoff.
        // (Not yet in settings UI, but used by JobDriver_CompanionGreetAndEscortToBed)
        public const int DefaultWaitForVisitorAtBedTimeoutTicks = 1 * GenDate.TicksPerHour;

        // =========================
        // EFFECTIVE VALUES (used by code)
        // =========================

        public static int TrackerTickIntervalTicks =>
            ReadInt(DefaultTrackerTickIntervalTicks,
                "TrackerTickIntervalTicks", "trackerTickIntervalTicks");

        public static int WaitRadius =>
            ReadInt(DefaultWaitRadius,
                "WaitRadius", "waitRadius");

        public static int VisitorDesireDelayTicks =>
            ReadInt(DefaultVisitorDesireDelayTicks,
                "VisitorDesireDelayTicks", "visitorDesireDelayTicks");

        public static float VisitorDesireChance =>
            ReadFloat(DefaultVisitorDesireChance,
                "VisitorDesireChance", "visitorDesireChance");

        public static int VisitorRetryCooldownTicks =>
            ReadInt(DefaultVisitorRetryCooldownTicks,
                "VisitorRetryCooldownTicks", "visitorRetryCooldownTicks");

        public static int VisitorForceJobCooldownTicks =>
            ReadInt(DefaultVisitorForceJobCooldownTicks,
                "VisitorForceJobCooldownTicks", "visitorForceJobCooldownTicks");

        public static int VisitorLoiterExpiryTicks =>
            ReadInt(DefaultVisitorLoiterExpiryTicks,
                "VisitorLoiterExpiryTicks", "visitorLoiterExpiryTicks");

        public static int GreetingDurationTicks =>
            ReadInt(DefaultGreetingDurationTicks,
                "GreetingDurationTicks", "greetingDurationTicks");

        public static int GreetingChitchatIntervalTicks =>
            ReadInt(DefaultGreetingChitchatIntervalTicks,
                "GreetingChitchatIntervalTicks", "greetingChitchatIntervalTicks");

        public static int VisitorGreetingExpiryPaddingTicks =>
            ReadInt(DefaultVisitorGreetingExpiryPaddingTicks,
                "VisitorGreetingExpiryPaddingTicks", "visitorGreetingExpiryPaddingTicks");

        public static int ClaimGraceTicks =>
            ReadInt(DefaultClaimGraceTicks,
                "ClaimGraceTicks", "claimGraceTicks");

        public static int MaxClaimTicks =>
            ReadInt(DefaultMaxClaimTicks,
                "MaxClaimTicks", "maxClaimTicks");

        public static int VisitorFollowJobExpiryTicks =>
            ReadInt(DefaultVisitorFollowJobExpiryTicks,
                "VisitorFollowJobExpiryTicks", "visitorFollowJobExpiryTicks");

        public static int VisitorFollowWaitDurationTicks =>
            ReadInt(DefaultVisitorFollowWaitDurationTicks,
                "VisitorFollowWaitDurationTicks", "visitorFollowWaitDurationTicks",
                "VisitorWaitAtBedDurationTicks", "visitorWaitAtBedDurationTicks");

        // Existing alias used in your project:
        public static int VisitorWaitAtBedDurationTicks => VisitorFollowWaitDurationTicks;

        public static int CompanionIdleAtSpotDurationTicks =>
            ReadInt(DefaultCompanionIdleAtSpotDurationTicks,
                "CompanionIdleAtSpotDurationTicks", "companionIdleAtSpotDurationTicks");

        public static int CompanionGreetAndEscortJobExpiryTicks =>
            ReadInt(DefaultCompanionGreetAndEscortJobExpiryTicks,
                "CompanionGreetAndEscortJobExpiryTicks", "companionGreetAndEscortJobExpiryTicks");

        public static int LovinDurationTicks =>
            ReadInt(DefaultLovinDurationTicks,
                "LovinDurationTicks", "lovinDurationTicks");

        public static int LovinJobExpiryTicks =>
            ReadInt(DefaultLovinJobExpiryTicks,
                "LovinJobExpiryTicks", "lovinJobExpiryTicks",
                "CustomLovinJobExpiryTicks", "customLovinJobExpiryTicks");

        // Existing alias used in your project:
        public static int CustomLovinJobExpiryTicks => LovinJobExpiryTicks;

        public static int LovinPartnerJobExpiryPaddingTicks =>
            ReadInt(DefaultLovinPartnerExpiryPaddingTicks,
                "LovinPartnerJobExpiryPaddingTicks", "lovinPartnerJobExpiryPaddingTicks",
                "LovinPartnerExpiryPaddingTicks", "lovinPartnerExpiryPaddingTicks");

        public static int LovinHeartFleckIntervalTicks =>
            ReadInt(DefaultLovinHeartFleckIntervalTicks,
                "LovinHeartFleckIntervalTicks", "lovinHeartFleckIntervalTicks");

        public static int VisitorLoiterMoveIntervalMinTicks =>
            ReadInt(DefaultVisitorLoiterMoveIntervalMinTicks,
                "VisitorLoiterMoveIntervalMinTicks", "visitorLoiterMoveIntervalMinTicks");

        public static int VisitorLoiterMoveIntervalMaxTicks =>
            ReadInt(DefaultVisitorLoiterMoveIntervalMaxTicks,
                "VisitorLoiterMoveIntervalMaxTicks", "visitorLoiterMoveIntervalMaxTicks");

        public static int WaitForVisitorAtBedTimeoutTicks =>
            ReadInt(DefaultWaitForVisitorAtBedTimeoutTicks,
                "WaitForVisitorAtBedTimeoutTicks", "waitForVisitorAtBedTimeoutTicks");

        // =========================
        // Reflection-backed Settings Reader
        // =========================

        private static object CachedSettingsObj;
        private static Type CachedSettingsType;
        private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();

        private static object SettingsObj
        {
            get
            {
                // CompanionshipMod.Settings exists in your project already.
                // If it is ever null, we just use defaults.
                return CompanionshipMod.Settings;
            }
        }

        private static void EnsureCache()
        {
            object s = SettingsObj;

            if (s == null)
            {
                CachedSettingsObj = null;
                CachedSettingsType = null;
                MemberCache.Clear();
                return;
            }

            if (!ReferenceEquals(CachedSettingsObj, s) || CachedSettingsType != s.GetType())
            {
                CachedSettingsObj = s;
                CachedSettingsType = s.GetType();
                MemberCache.Clear();
            }
        }

        private static bool TryGetMember(string name, out MemberInfo member)
        {
            member = null;

            EnsureCache();
            if (CachedSettingsObj == null || CachedSettingsType == null)
                return false;

            if (MemberCache.TryGetValue(name, out member))
                return member != null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo f = CachedSettingsType.GetField(name, flags);
            if (f != null)
            {
                member = f;
                MemberCache[name] = member;
                return true;
            }

            PropertyInfo p = CachedSettingsType.GetProperty(name, flags);
            if (p != null && p.CanRead)
            {
                member = p;
                MemberCache[name] = member;
                return true;
            }

            MemberCache[name] = null;
            return false;
        }

        private static int ReadInt(int fallback, params string[] names)
        {
            try
            {
                EnsureCache();
                if (CachedSettingsObj == null) return fallback;

                for (int i = 0; i < names.Length; i++)
                {
                    if (!TryGetMember(names[i], out MemberInfo m) || m == null)
                        continue;

                    object v = GetMemberValue(m, CachedSettingsObj);
                    if (v is int vi) return vi;
                    if (v is short vs) return vs;
                    if (v is long vl) return (int)vl;
                }
            }
            catch
            {
                // swallow and fallback
            }

            return fallback;
        }

        private static float ReadFloat(float fallback, params string[] names)
        {
            try
            {
                EnsureCache();
                if (CachedSettingsObj == null) return fallback;

                for (int i = 0; i < names.Length; i++)
                {
                    if (!TryGetMember(names[i], out MemberInfo m) || m == null)
                        continue;

                    object v = GetMemberValue(m, CachedSettingsObj);
                    if (v is float vf) return vf;
                    if (v is double vd) return (float)vd;
                }
            }
            catch
            {
                // swallow and fallback
            }

            return fallback;
        }

        private static object GetMemberValue(MemberInfo m, object obj)
        {
            if (m is FieldInfo f) return f.GetValue(obj);
            if (m is PropertyInfo p) return p.GetValue(obj, null);
            return null;
        }
    }
}
