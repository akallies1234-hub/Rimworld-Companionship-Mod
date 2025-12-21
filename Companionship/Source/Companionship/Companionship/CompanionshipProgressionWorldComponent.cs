using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Stores companion progression (XP / lifetime earnings / etc.) in a save-safe place.
    /// WorldComponent is available across maps and persists with the save.
    /// </summary>
    public class CompanionshipProgressionWorldComponent : WorldComponent
    {
        private List<CompanionProgressRecord> records = new List<CompanionProgressRecord>();

        public CompanionshipProgressionWorldComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "companionProgressRecords", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<CompanionProgressRecord>();
                records.RemoveAll(r => r == null || r.pawn == null);
            }
        }

        /// <summary>
        /// Non-creating lookup.
        /// </summary>
        public bool TryGet(Pawn pawn, out CompanionProgressRecord record)
        {
            record = null;
            if (pawn == null || records == null) return false;

            for (int i = 0; i < records.Count; i++)
            {
                CompanionProgressRecord r = records[i];
                if (r?.pawn == pawn)
                {
                    record = r;
                    return true;
                }
            }
            return false;
        }

        public CompanionProgressRecord GetOrCreate(Pawn pawn)
        {
            if (pawn == null) return null;

            if (TryGet(pawn, out CompanionProgressRecord existing))
            {
                return existing;
            }

            CompanionProgressRecord created = new CompanionProgressRecord
            {
                pawn = pawn,
                xp = 0,
                lifetimeEarningsSilver = 0,
                successfulDates = 0,
                lastRewardedSessionStartedAtTick = -1,
                lastPayoutSilver = 0,
                lastQualityScore = -1
            };

            records.Add(created);
            return created;
        }
    }

    public class CompanionProgressRecord : IExposable
    {
        public Pawn pawn;

        public int xp;
        public int lifetimeEarningsSilver;
        public int successfulDates;

        // Guard against double-rewarding the same session.
        public int lastRewardedSessionStartedAtTick;

        // For inspect/debug flavor
        public int lastPayoutSilver;
        public int lastQualityScore;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref xp, "xp", 0);
            Scribe_Values.Look(ref lifetimeEarningsSilver, "lifetimeEarningsSilver", 0);
            Scribe_Values.Look(ref successfulDates, "successfulDates", 0);

            Scribe_Values.Look(ref lastRewardedSessionStartedAtTick, "lastRewardedSessionStartedAtTick", -1);
            Scribe_Values.Look(ref lastPayoutSilver, "lastPayoutSilver", 0);
            Scribe_Values.Look(ref lastQualityScore, "lastQualityScore", -1);
        }
    }

    public static class CompanionshipTierUtility
    {
        // --- Tier thresholds (open-ended) ---
        // Tier 1:   0–9
        // Tier 2:  10–24
        // Tier 3:  25–49
        // Tier 4:  50–99
        // Tier 5: 100–149
        // Tier 6+: +50 per tier (150–199, 200–249, 250–299, ...)
        public static int GetTierForXp(int xp)
        {
            if (xp < 10) return 1;
            if (xp < 25) return 2;
            if (xp < 50) return 3;
            if (xp < 100) return 4;
            if (xp < 150) return 5;

            // Tier 6 starts at 150.
            return 6 + Mathf.FloorToInt((xp - 150) / 50f);
        }

        public static int GetNextTierThresholdForTier(int tier)
        {
            if (tier <= 1) return 10;
            if (tier == 2) return 25;
            if (tier == 3) return 50;
            if (tier == 4) return 100;
            if (tier == 5) return 150;

            // Tier 6 next is 200, Tier 7 next is 250, etc.
            return 150 + (tier - 5) * 50;
        }

        public static int GetNextTierThresholdForXp(int xp)
        {
            int tier = GetTierForXp(xp);
            return GetNextTierThresholdForTier(tier);
        }

        public static string GetTierTitle(int tier)
        {
            // Matches the titles you've been using in the inspect UI.
            string[] titles =
            {
                "Newbie",
                "Sweet Treat",
                "Rising Star",
                "Crowd Pleaser",
                "Professional",
                "Headliner",
                "Star Attraction",
                "Icon",
                "Mythic",
                "Legend"
            };

            if (tier <= 0) tier = 1;
            if (tier <= titles.Length) return titles[tier - 1];
            return titles[titles.Length - 1];
        }
    }
}
