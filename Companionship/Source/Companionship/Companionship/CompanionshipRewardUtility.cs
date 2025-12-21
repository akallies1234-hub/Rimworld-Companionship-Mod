using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Companionship
{
    public enum CompanionshipDateQuality
    {
        Terrible,
        Bad,
        Neutral,
        Good,
        Excellent,
        Exceptional
    }

    public static class CompanionshipRewardsUtility
    {
        // ------------------------------
        // Payout tuning
        // ------------------------------

        private const int BaseFeePerTierSilver = 20;
        private const int MinPayoutSilver = 5;

        // Stat scaling (multiplier components)
        private const float SocialSkillMaxBonus = 0.30f;   // +30%
        private const float BeautyMaxBonus = 0.20f;        // +20%
        private const float ImpactMaxBonus = 0.25f;        // +25%

        // Tech modifier based on VISITOR faction tech level
        private const float TechLowBonus = 0.00f;          // Neolithic/Medieval
        private const float TechMidBonus = 0.10f;          // Industrial/Spacer
        private const float TechHighBonus = 0.20f;         // Ultra

        // ------------------------------
        // Quality scoring tuning
        // ------------------------------

        private const float SocialImpactMinForScore = 0.8f;  // maps to 0 points
        private const float SocialImpactMaxForScore = 1.4f;  // maps to 20 points

        private const float BeautyMinForScore = -2f;          // maps to 0 points
        private const float BeautyMaxForScore = 2f;           // maps to 20 points

        private const float RoomImpressivenessForMaxPoints = 200f; // maps to 20 points

        // ------------------------------
        // NEW mood thresholds (forgiving)
        // ------------------------------
        private const int TerribleMax = 19;     // 0..19
        private const int BadMax = 30;          // 20..30
        private const int NeutralMax = 45;      // 31..45 (no moodlet)
        private const int GoodMax = 55;         // 46..55
        private const int ExcellentMax = 74;    // 56..74
        // 75..100 => Exceptional

        // XP reward
        private const int XpOnSuccessBase = 1;
        private const int XpBonusIfGoodOrBetter = 1; // total 2 on Good/Excellent/Exceptional

        // Cache EndTable def by name to avoid relying on ThingDefOf.EndTable existing in every build.
        private static ThingDef cachedEndTableDef;
        private static bool searchedEndTableDef;

        private static ThingDef EndTableDef
        {
            get
            {
                if (searchedEndTableDef) return cachedEndTableDef;
                searchedEndTableDef = true;
                cachedEndTableDef = DefDatabase<ThingDef>.GetNamedSilentFail("EndTable");
                return cachedEndTableDef;
            }
        }

        public static void OnSuccessfulDate(Pawn visitor, Pawn companion, DateSession session)
        {
            try
            {
                if (companion == null) return;

                // Visitor can be null in rare edge cases (e.g. visitor despawns at the very end). In that case we
                // still reward the companion, using a safe fallback for any visitor-derived modifiers.
                Pawn resolvedVisitor = visitor ?? session?.visitor;

                // Only track player-side companions.
                if (companion.Faction != Faction.OfPlayer) return;
                if (!companion.RaceProps?.Humanlike ?? true) return;

                // Require Companion work type enabled.
                if (!CompanionshipPawnUtility.HasCompanionWorkTypeEnabled(companion)) return;

                CompanionshipProgressionWorldComponent wc = Find.World?.GetComponent<CompanionshipProgressionWorldComponent>();
                if (wc == null) return;

                CompanionProgressRecord rec = wc.GetOrCreate(companion);
                if (rec == null) return;

                // Guard against duplicate reward for the same session.
                int sessionStart = session != null ? session.startedAtTick : -1;
                if (sessionStart >= 0 && rec.lastRewardedSessionStartedAtTick == sessionStart)
                {
                    return;
                }

                Building_Bed bed = session != null ? session.Bed : null;

                int qualityScore = CalculateQualityScore(companion, bed);
                CompanionshipDateQuality quality = GetQualityTier(qualityScore);

                int tier = CompanionshipTierUtility.GetTierForXp(rec.xp);
                int payout = CalculatePayoutSilver(resolvedVisitor, companion, tier);

                // Spawn payout silver (prefer end tables at the bed head corners)
                TrySpawnPayoutSilver(payout, bed, companion);

                // Moodlet/memory (NEW logic: Neutral applies no moodlet)
                TryApplyDateMoodlet(companion, resolvedVisitor, quality);

                // Pregnancy chance (Biotech) - forced "Avoid pregnancy" behavior inside the utility
                // (No-op when Biotech is disabled.)
                CompanionshipPregnancyUtility.TryApplyPregnancy(companion, resolvedVisitor);

                // XP progression
                int xpGain = XpOnSuccessBase;
                if (quality == CompanionshipDateQuality.Good ||
                    quality == CompanionshipDateQuality.Excellent ||
                    quality == CompanionshipDateQuality.Exceptional)
                {
                    xpGain += XpBonusIfGoodOrBetter;
                }

                rec.xp += xpGain;
                rec.lifetimeEarningsSilver += payout;
                rec.successfulDates += 1;

                rec.lastPayoutSilver = payout;
                rec.lastQualityScore = qualityScore;
                rec.lastRewardedSessionStartedAtTick = sessionStart;
            }
            catch (Exception ex)
            {
                Log.Error($"[Companionship] Rewards failed: {ex}");
            }
        }

        private static int CalculateQualityScore(Pawn companion, Building_Bed bed)
        {
            int score = 0;

            // Social skill: up to 40 points
            int social = companion.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            score += Mathf.Clamp(social * 2, 0, 40);

            // Beauty: up to 20 points
            float beauty = SafeGetStat(companion, StatDefOf.PawnBeauty, 0f);
            float beautyN = Mathf.InverseLerp(BeautyMinForScore, BeautyMaxForScore, beauty);
            score += Mathf.RoundToInt(Mathf.Clamp01(beautyN) * 20f);

            // Social impact: up to 20 points
            float impact = SafeGetStat(companion, StatDefOf.SocialImpact, 1f);
            float impactN = Mathf.InverseLerp(SocialImpactMinForScore, SocialImpactMaxForScore, impact);
            score += Mathf.RoundToInt(Mathf.Clamp01(impactN) * 20f);

            // Room impressiveness: up to 20 points (outdoors => 0)
            int roomPoints = 0;
            if (bed != null && bed.Map != null)
            {
                Room room = bed.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    float imp = room.GetStat(RoomStatDefOf.Impressiveness);
                    float impN = Mathf.InverseLerp(0f, RoomImpressivenessForMaxPoints, imp);
                    roomPoints = Mathf.RoundToInt(Mathf.Clamp01(impN) * 20f);
                }
            }
            score += Mathf.Clamp(roomPoints, 0, 20);

            return Mathf.Clamp(score, 0, 100);
        }

        private static CompanionshipDateQuality GetQualityTier(int score)
        {
            if (score <= TerribleMax) return CompanionshipDateQuality.Terrible;
            if (score <= BadMax) return CompanionshipDateQuality.Bad;
            if (score <= NeutralMax) return CompanionshipDateQuality.Neutral;
            if (score <= GoodMax) return CompanionshipDateQuality.Good;
            if (score <= ExcellentMax) return CompanionshipDateQuality.Excellent;
            return CompanionshipDateQuality.Exceptional;
        }

        private static float SafeGetStat(Pawn pawn, StatDef stat, float fallback)
        {
            try
            {
                if (pawn == null || stat == null) return fallback;
                return pawn.GetStatValue(stat, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static void TrySpawnPayoutSilver(int payout, Building_Bed bed, Pawn companion)
        {
            if (payout <= 0) return;

            Map map = bed?.Map ?? companion?.Map;
            if (map == null) return;

            IntVec3 root = IntVec3.Invalid;
            bool preferDirect = false;

            if (bed != null && bed.Map == map)
            {
                root = FindPreferredPayoutRootCell(bed, out preferDirect);
            }

            if (!root.IsValid && companion != null && companion.Spawned && companion.Map == map)
            {
                root = companion.Position;
            }

            if (!root.IsValid) return;

            ThingDef silverDef = ThingDefOf.Silver;
            int remaining = payout;

            while (remaining > 0)
            {
                int stack = Mathf.Min(remaining, silverDef.stackLimit);
                remaining -= stack;

                Thing silver = ThingMaker.MakeThing(silverDef);
                silver.stackCount = stack;

                // If we're targeting an end table cell, try to place DIRECTLY there first.
                if (preferDirect)
                {
                    if (!GenPlace.TryPlaceThing(silver, root, map, ThingPlaceMode.Direct))
                    {
                        GenPlace.TryPlaceThing(silver, root, map, ThingPlaceMode.Near);
                    }
                }
                else
                {
                    // Near so it can “snap” to a valid adjacent cell if needed.
                    GenPlace.TryPlaceThing(silver, root, map, ThingPlaceMode.Near);
                }
            }
        }

        /// <summary>
        /// Prefer placing silver on a bedside end table adjacent to the head corner cells of the bed.
        /// Falls back to the "ideal" end-table cell even if no end table exists.
        /// Final fallback: bed position.
        /// </summary>
        private static IntVec3 FindPreferredPayoutRootCell(Building_Bed bed, out bool preferDirect)
        {
            preferDirect = false;
            if (bed == null || bed.Map == null) return IntVec3.Invalid;

            Map map = bed.Map;

            CellRect rect = GenAdj.OccupiedRect(bed);
            IntVec3 center = rect.CenterCell;

            // Determine "foot direction" from center -> interaction cell (more stable than using Position on a 2x2 bed).
            IntVec3 delta = bed.InteractionCell - center;

            Rot4 footDir;
            if (Math.Abs(delta.x) > Math.Abs(delta.z))
                footDir = delta.x > 0 ? Rot4.East : Rot4.West;
            else
                footDir = delta.z > 0 ? Rot4.North : Rot4.South;

            Rot4 headDir = footDir.Opposite;

            IntVec3 headVec = headDir.FacingCell;
            IntVec3 leftVec = headDir.Rotated(RotationDirection.Counterclockwise).FacingCell;
            IntVec3 rightVec = headDir.Rotated(RotationDirection.Clockwise).FacingCell;

            // Find the two "head corner" cells of the bed (top-left/top-right relative to head direction).
            IntVec3 headLeft;
            IntVec3 headRight;

            if (headDir == Rot4.North)
            {
                int z = rect.maxZ;
                headLeft = new IntVec3(rect.minX, 0, z);
                headRight = new IntVec3(rect.maxX, 0, z);
            }
            else if (headDir == Rot4.South)
            {
                int z = rect.minZ;
                // Facing South, "left" is East (higher X)
                headLeft = new IntVec3(rect.maxX, 0, z);
                headRight = new IntVec3(rect.minX, 0, z);
            }
            else if (headDir == Rot4.East)
            {
                int x = rect.maxX;
                // Facing East, "left" is North (higher Z)
                headLeft = new IntVec3(x, 0, rect.maxZ);
                headRight = new IntVec3(x, 0, rect.minZ);
            }
            else // West
            {
                int x = rect.minX;
                // Facing West, "left" is South (lower Z)
                headLeft = new IntVec3(x, 0, rect.minZ);
                headRight = new IntVec3(x, 0, rect.maxZ);
            }

            // Candidate cells where an end table would typically be placed:
            // 1) Beside the head corners (left of head-left, right of head-right)
            // 2) In front of the head corners (head direction)
            List<IntVec3> candidates = new List<IntVec3>(6)
            {
                headLeft + leftVec,
                headRight + rightVec,
                headLeft + headVec,
                headRight + headVec
            };

            ThingDef endTableDef = EndTableDef;

            // 1) Prefer a cell that actually contains an EndTable.
            if (endTableDef != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    IntVec3 c = candidates[i];
                    if (!c.InBounds(map)) continue;
                    if (rect.Contains(c)) continue;

                    // Skip solid walls/impassable edifices unless it's the end table itself.
                    Building ed = c.GetEdifice(map);
                    if (ed != null && ed.def != null && ed.def != endTableDef && ed.def.passability == Traversability.Impassable)
                        continue;

                    List<Thing> things = c.GetThingList(map);
                    for (int t = 0; t < things.Count; t++)
                    {
                        Thing th = things[t];
                        if (th != null && th.def == endTableDef)
                        {
                            preferDirect = true;
                            return c;
                        }
                    }
                }
            }

            // 2) No end table found: return the "ideal" cell anyway.
            for (int i = 0; i < candidates.Count; i++)
            {
                IntVec3 c = candidates[i];
                if (!c.InBounds(map)) continue;
                if (rect.Contains(c)) continue;

                Building ed = c.GetEdifice(map);
                if (ed != null && ed.def != null && ed.def.passability == Traversability.Impassable)
                    continue;

                return c;
            }

            // 3) Fallback: bed position.
            return bed.Position;
        }

        private static int CalculatePayoutSilver(Pawn visitor, Pawn companion, int tier)
        {
            int baseFee = Mathf.Max(1, tier) * BaseFeePerTierSilver;

            float socialMod = CalculateSocialModifier(companion);
            float beautyMod = CalculateBeautyModifier(companion);
            float impactMod = CalculateSocialImpactModifier(companion);
            float techMod = CalculateTechModifier(visitor);

            float multSocial = Mathf.Max(0.10f, 1f + socialMod);
            float multBeauty = Mathf.Max(0.10f, 1f + beautyMod);
            float multImpact = Mathf.Max(0.10f, 1f + impactMod);
            float multTech = Mathf.Max(0.10f, 1f + techMod);

            float payout = baseFee * multSocial * multBeauty * multImpact * multTech;
            return Mathf.Max(MinPayoutSilver, Mathf.RoundToInt(payout));
        }

        private static float CalculateSocialModifier(Pawn companion)
        {
            int social = companion.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            float n = Mathf.InverseLerp(0f, 20f, social);
            return Mathf.Clamp01(n) * SocialSkillMaxBonus;
        }

        private static float CalculateBeautyModifier(Pawn companion)
        {
            float beauty = SafeGetStat(companion, StatDefOf.PawnBeauty, 0f);
            float n = Mathf.InverseLerp(-2f, 2f, beauty);
            return Mathf.Clamp01(n) * BeautyMaxBonus;
        }

        private static float CalculateSocialImpactModifier(Pawn companion)
        {
            float impact = SafeGetStat(companion, StatDefOf.SocialImpact, 1f);
            float n = Mathf.InverseLerp(0.8f, 1.4f, impact);
            return Mathf.Clamp01(n) * ImpactMaxBonus;
        }

        private static float CalculateTechModifier(Pawn visitor)
        {
            TechLevel tech = TechLevel.Neolithic;
            if (visitor?.Faction?.def != null)
            {
                tech = visitor.Faction.def.techLevel;
            }

            if (tech <= TechLevel.Medieval) return TechLowBonus;
            if (tech <= TechLevel.Spacer) return TechMidBonus;
            return TechHighBonus;
        }

        private static void TryApplyDateMoodlet(Pawn companion, Pawn visitor, CompanionshipDateQuality quality)
        {
            if (companion?.needs?.mood?.thoughts?.memories == null) return;

            // Always clear any previous Companionship date memories first.
            RemoveAllCompanionshipDateMemories(companion);

            // Neutral dates apply NO moodlet (per design).
            if (quality == CompanionshipDateQuality.Neutral)
                return;

            ThoughtDef thought = null;
            switch (quality)
            {
                case CompanionshipDateQuality.Terrible:
                    thought = CompanionshipThoughtDefOf.Companionship_DateTerrible;
                    break;
                case CompanionshipDateQuality.Bad:
                    thought = CompanionshipThoughtDefOf.Companionship_DateBad;
                    break;
                case CompanionshipDateQuality.Good:
                    thought = CompanionshipThoughtDefOf.Companionship_DateGood;
                    break;
                case CompanionshipDateQuality.Excellent:
                    thought = CompanionshipThoughtDefOf.Companionship_DateExcellent;
                    break;
                case CompanionshipDateQuality.Exceptional:
                    thought = CompanionshipThoughtDefOf.Companionship_DateExceptional;
                    break;
            }

            if (thought == null) return;

            companion.needs.mood.thoughts.memories.TryGainMemory(thought, visitor);
        }

        private static void RemoveAllCompanionshipDateMemories(Pawn pawn)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;

            List<Thought_Memory> mems = pawn.needs.mood.thoughts.memories.Memories;
            if (mems == null || mems.Count == 0) return;

            for (int i = mems.Count - 1; i >= 0; i--)
            {
                Thought_Memory m = mems[i];
                if (m?.def == null) continue;

                if (m.def == CompanionshipThoughtDefOf.Companionship_DateTerrible ||
                    m.def == CompanionshipThoughtDefOf.Companionship_DateBad ||
                    m.def == CompanionshipThoughtDefOf.Companionship_DateNeutral ||
                    m.def == CompanionshipThoughtDefOf.Companionship_DateGood ||
                    m.def == CompanionshipThoughtDefOf.Companionship_DateExcellent ||
                    m.def == CompanionshipThoughtDefOf.Companionship_DateExceptional)
                {
                    pawn.needs.mood.thoughts.memories.RemoveMemory(m);
                }
            }
        }
    }
}
