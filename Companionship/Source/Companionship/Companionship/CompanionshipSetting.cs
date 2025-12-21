using Verse;

namespace Companionship
{
    public class CompanionshipSettings : ModSettings
    {
        // Debug
        public bool enableDebugOverlay = false;
        public bool verboseLogging = false;

        // Enable overrides
        public bool useCustomTuning = false;

        // Tuning values (ticks unless otherwise noted)
        public int trackerTickIntervalTicks = CompanionshipTuning.DefaultTrackerTickIntervalTicks;

        public int waitRadius = CompanionshipTuning.DefaultWaitRadius;

        public int visitorDesireDelayTicks = CompanionshipTuning.DefaultVisitorDesireDelayTicks;
        public float visitorDesireChance = CompanionshipTuning.DefaultVisitorDesireChance;

        public int visitorRetryCooldownTicks = CompanionshipTuning.DefaultVisitorRetryCooldownTicks;
        public int visitorForceJobCooldownTicks = CompanionshipTuning.DefaultVisitorForceJobCooldownTicks;
        public int visitorLoiterExpiryTicks = CompanionshipTuning.DefaultVisitorLoiterExpiryTicks;

        public int greetingDurationTicks = CompanionshipTuning.DefaultGreetingDurationTicks;
        public int visitorGreetingExpiryPaddingTicks = CompanionshipTuning.DefaultVisitorGreetingExpiryPaddingTicks;
        public int greetingChitchatIntervalTicks = CompanionshipTuning.DefaultGreetingChitchatIntervalTicks;

        public int claimGraceTicks = CompanionshipTuning.DefaultClaimGraceTicks;
        public int maxClaimTicks = CompanionshipTuning.DefaultMaxClaimTicks;

        public int visitorFollowJobExpiryTicks = CompanionshipTuning.DefaultVisitorFollowJobExpiryTicks;
        public int visitorFollowWaitDurationTicks = CompanionshipTuning.DefaultVisitorFollowWaitDurationTicks;

        public int companionIdleAtSpotDurationTicks = CompanionshipTuning.DefaultCompanionIdleAtSpotDurationTicks;
        public int companionGreetAndEscortJobExpiryTicks = CompanionshipTuning.DefaultCompanionGreetAndEscortJobExpiryTicks;

        public int lovinDurationTicks = CompanionshipTuning.DefaultLovinDurationTicks;
        public int lovinJobExpiryTicks = CompanionshipTuning.DefaultLovinJobExpiryTicks;
        public int lovinPartnerExpiryPaddingTicks = CompanionshipTuning.DefaultLovinPartnerExpiryPaddingTicks;
        public int lovinHeartFleckIntervalTicks = CompanionshipTuning.DefaultLovinHeartFleckIntervalTicks;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref enableDebugOverlay, "enableDebugOverlay", false);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);

            Scribe_Values.Look(ref useCustomTuning, "useCustomTuning", false);

            Scribe_Values.Look(ref trackerTickIntervalTicks, "trackerTickIntervalTicks", CompanionshipTuning.DefaultTrackerTickIntervalTicks);

            Scribe_Values.Look(ref waitRadius, "waitRadius", CompanionshipTuning.DefaultWaitRadius);

            Scribe_Values.Look(ref visitorDesireDelayTicks, "visitorDesireDelayTicks", CompanionshipTuning.DefaultVisitorDesireDelayTicks);
            Scribe_Values.Look(ref visitorDesireChance, "visitorDesireChance", CompanionshipTuning.DefaultVisitorDesireChance);

            Scribe_Values.Look(ref visitorRetryCooldownTicks, "visitorRetryCooldownTicks", CompanionshipTuning.DefaultVisitorRetryCooldownTicks);
            Scribe_Values.Look(ref visitorForceJobCooldownTicks, "visitorForceJobCooldownTicks", CompanionshipTuning.DefaultVisitorForceJobCooldownTicks);
            Scribe_Values.Look(ref visitorLoiterExpiryTicks, "visitorLoiterExpiryTicks", CompanionshipTuning.DefaultVisitorLoiterExpiryTicks);

            Scribe_Values.Look(ref greetingDurationTicks, "greetingDurationTicks", CompanionshipTuning.DefaultGreetingDurationTicks);
            Scribe_Values.Look(ref visitorGreetingExpiryPaddingTicks, "visitorGreetingExpiryPaddingTicks", CompanionshipTuning.DefaultVisitorGreetingExpiryPaddingTicks);
            Scribe_Values.Look(ref greetingChitchatIntervalTicks, "greetingChitchatIntervalTicks", CompanionshipTuning.DefaultGreetingChitchatIntervalTicks);

            Scribe_Values.Look(ref claimGraceTicks, "claimGraceTicks", CompanionshipTuning.DefaultClaimGraceTicks);
            Scribe_Values.Look(ref maxClaimTicks, "maxClaimTicks", CompanionshipTuning.DefaultMaxClaimTicks);

            Scribe_Values.Look(ref visitorFollowJobExpiryTicks, "visitorFollowJobExpiryTicks", CompanionshipTuning.DefaultVisitorFollowJobExpiryTicks);
            Scribe_Values.Look(ref visitorFollowWaitDurationTicks, "visitorFollowWaitDurationTicks", CompanionshipTuning.DefaultVisitorFollowWaitDurationTicks);

            Scribe_Values.Look(ref companionIdleAtSpotDurationTicks, "companionIdleAtSpotDurationTicks", CompanionshipTuning.DefaultCompanionIdleAtSpotDurationTicks);
            Scribe_Values.Look(ref companionGreetAndEscortJobExpiryTicks, "companionGreetAndEscortJobExpiryTicks", CompanionshipTuning.DefaultCompanionGreetAndEscortJobExpiryTicks);

            Scribe_Values.Look(ref lovinDurationTicks, "lovinDurationTicks", CompanionshipTuning.DefaultLovinDurationTicks);
            Scribe_Values.Look(ref lovinJobExpiryTicks, "lovinJobExpiryTicks", CompanionshipTuning.DefaultLovinJobExpiryTicks);
            Scribe_Values.Look(ref lovinPartnerExpiryPaddingTicks, "lovinPartnerExpiryPaddingTicks", CompanionshipTuning.DefaultLovinPartnerExpiryPaddingTicks);
            Scribe_Values.Look(ref lovinHeartFleckIntervalTicks, "lovinHeartFleckIntervalTicks", CompanionshipTuning.DefaultLovinHeartFleckIntervalTicks);
        }

        public void ResetCustomTuningToDefaults()
        {
            trackerTickIntervalTicks = CompanionshipTuning.DefaultTrackerTickIntervalTicks;

            waitRadius = CompanionshipTuning.DefaultWaitRadius;

            visitorDesireDelayTicks = CompanionshipTuning.DefaultVisitorDesireDelayTicks;
            visitorDesireChance = CompanionshipTuning.DefaultVisitorDesireChance;

            visitorRetryCooldownTicks = CompanionshipTuning.DefaultVisitorRetryCooldownTicks;
            visitorForceJobCooldownTicks = CompanionshipTuning.DefaultVisitorForceJobCooldownTicks;
            visitorLoiterExpiryTicks = CompanionshipTuning.DefaultVisitorLoiterExpiryTicks;

            greetingDurationTicks = CompanionshipTuning.DefaultGreetingDurationTicks;
            visitorGreetingExpiryPaddingTicks = CompanionshipTuning.DefaultVisitorGreetingExpiryPaddingTicks;
            greetingChitchatIntervalTicks = CompanionshipTuning.DefaultGreetingChitchatIntervalTicks;

            claimGraceTicks = CompanionshipTuning.DefaultClaimGraceTicks;
            maxClaimTicks = CompanionshipTuning.DefaultMaxClaimTicks;

            visitorFollowJobExpiryTicks = CompanionshipTuning.DefaultVisitorFollowJobExpiryTicks;
            visitorFollowWaitDurationTicks = CompanionshipTuning.DefaultVisitorFollowWaitDurationTicks;

            companionIdleAtSpotDurationTicks = CompanionshipTuning.DefaultCompanionIdleAtSpotDurationTicks;
            companionGreetAndEscortJobExpiryTicks = CompanionshipTuning.DefaultCompanionGreetAndEscortJobExpiryTicks;

            lovinDurationTicks = CompanionshipTuning.DefaultLovinDurationTicks;
            lovinJobExpiryTicks = CompanionshipTuning.DefaultLovinJobExpiryTicks;
            lovinPartnerExpiryPaddingTicks = CompanionshipTuning.DefaultLovinPartnerExpiryPaddingTicks;
            lovinHeartFleckIntervalTicks = CompanionshipTuning.DefaultLovinHeartFleckIntervalTicks;
        }
    }
}
