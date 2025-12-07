using RimWorld;
using Verse;
using UnityEngine;

namespace Riot.Companionship
{
    public class CompProperties_Companionship : CompProperties
    {
        public CompProperties_Companionship()
        {
            this.compClass = typeof(CompCompanionship);
        }
    }

    public class CompCompanionship : ThingComp
    {
        private int xp = 0;
        private int datesCompleted = 0;
        private int successfulDates = 0;
        private int datesCompletedToday = 0;
        private int lastDateDay = -1;

        public int XP
        {
            get => xp;
            set => xp = Mathf.Max(0, value);
        }

        public int DatesCompleted => datesCompleted;
        public int SuccessfulDates => successfulDates;

        public int CurrentTier
        {
            get
            {
                if (xp >= 100) return 5;
                if (xp >= 50) return 4;
                if (xp >= 25) return 3;
                if (xp >= 10) return 2;
                return 1;
            }
        }

        private int MaxDatesPerDay
        {
            get { return CurrentTier; }
        }

        public void RecordDate(bool success)
        {
            datesCompleted++;
            if (success)
            {
                successfulDates++;
            }
        }

        public void AddXP(int amount)
        {
            XP += amount;
        }

        /// <summary>
        /// Can this pawn start a new date right now, respecting per-day limits?
        /// </summary>
        public bool CanStartDateNow(Pawn pawn)
        {
            int day = GenLocalDate.DayOfYear(pawn);
            if (day != lastDateDay)
            {
                lastDateDay = day;
                datesCompletedToday = 0;
            }

            return datesCompletedToday < MaxDatesPerDay;
        }

        /// <summary>
        /// Notify the comp that a new date has started (increments daily count).
        /// </summary>
        public void Notify_DateStarted(Pawn pawn)
        {
            int day = GenLocalDate.DayOfYear(pawn);
            if (day != lastDateDay)
            {
                lastDateDay = day;
                datesCompletedToday = 0;
            }

            datesCompletedToday++;
        }

        public override string CompInspectStringExtra()
        {
            string baseString = base.CompInspectStringExtra();
            Pawn pawn = this.parent as Pawn;

            if (pawn == null || XP <= 0)
            {
                return baseString;
            }

            string tierName = GetTierTitle(CurrentTier);
            int nextTierXP = GetXPThresholdForTier(CurrentTier + 1);

            int estimated = PaymentUtility.EstimateBasePaymentWithoutTip(pawn);

            string progressLine;
            if (nextTierXP > 0)
            {
                progressLine = $"XP: {XP} / {nextTierXP}";
            }
            else
            {
                progressLine = $"XP: {XP} (max tier)";
            }

            float successRate = datesCompleted > 0
                ? (float)successfulDates / datesCompleted
                : 0f;

            string result =
                $"Companion rank: {tierName} (Tier {CurrentTier})\n" +
                $"{progressLine}\n" +
                $"Dates: {successfulDates}/{datesCompleted} successful ({successRate:P0})\n" +
                $"Today's dates: {datesCompletedToday} / {MaxDatesPerDay}\n" +
                $"Estimated earnings per date: {estimated} silver";

            if (!string.IsNullOrEmpty(baseString))
            {
                return baseString + "\n" + result;
            }

            return result;
        }

        /// <summary>
        /// Persistent data for this comp.
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref xp, "compCompanionshipXP", 0);
            Scribe_Values.Look(ref datesCompleted, "compCompanionshipDatesCompleted", 0);
            Scribe_Values.Look(ref successfulDates, "compCompanionshipSuccessfulDates", 0);
            Scribe_Values.Look(ref datesCompletedToday, "compCompanionshipDatesCompletedToday", 0);
            Scribe_Values.Look(ref lastDateDay, "compCompanionshipLastDateDay", -1);
        }

        private string GetTierTitle(int tier)
        {
            switch (tier)
            {
                case 1:
                    return "Novice Companion";
                case 2:
                    return "Seasoned Companion";
                case 3:
                    return "Esteemed Companion";
                case 4:
                    return "Elite Companion";
                case 5:
                    return "Legendary Companion";
                default:
                    return "Companion";
            }
        }

        private int GetXPThresholdForTier(int nextTier)
        {
            switch (nextTier)
            {
                case 2: return 10;
                case 3: return 25;
                case 4: return 50;
                case 5: return 100;
                default: return 0;
            }
        }
    }
}
