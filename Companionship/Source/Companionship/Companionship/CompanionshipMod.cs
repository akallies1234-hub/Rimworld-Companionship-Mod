using RimWorld;
using UnityEngine;
using Verse;

namespace Companionship
{
    public class CompanionshipMod : Mod
    {
        public static CompanionshipMod Instance { get; private set; }
        public static CompanionshipSettings Settings { get; private set; }

        private Vector2 scrollPos = Vector2.zero;

        public CompanionshipMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<CompanionshipSettings>();
        }

        public override string SettingsCategory() => "Companionship";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);

            if (Settings == null)
                Settings = GetSettings<CompanionshipSettings>();

            // Scroll view (this gets long)
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 900f);
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            list.Label("Debug");
            list.GapLine();

            list.CheckboxLabeled("Enable debug overlay (Dev Mode only)", ref Settings.enableDebugOverlay,
                "Shows a small overlay above visitors showing desire/claim/session state.\nRequires RimWorld Dev Mode to be enabled.");

            list.CheckboxLabeled("Enable verbose logging", ref Settings.verboseLogging,
                "Prints additional Companionship logs to the RimWorld log. Useful for diagnosing weird edge cases.");

            list.Gap(12f);

            list.Label("Live Tuning");
            list.GapLine();

            list.CheckboxLabeled("Enable custom tuning (live)", ref Settings.useCustomTuning,
                "When enabled, the values below override the mod defaults immediately.\nExisting jobs won't retroactively change durations/expiry, but new jobs will use the updated values.");

            if (!Settings.useCustomTuning)
            {
                list.Gap(6f);
                list.Label("Custom tuning is currently disabled. Defaults are active.");
                list.Gap(12f);
            }

            // Helper: grey-out tuning controls when disabled
            bool oldEnabled = GUI.enabled;
            GUI.enabled = Settings.useCustomTuning;

            // --- Core system knobs ---
            list.Label("Core");
            list.GapLine();

            Settings.trackerTickIntervalTicks = Mathf.RoundToInt(
                list.SliderLabeled("Tracker update interval (ticks)",
                    Settings.trackerTickIntervalTicks, 10f, 600f,
                    $"How often the tracker runs its state machine. Lower = more responsive, higher = cheaper.\nCurrent: {Settings.trackerTickIntervalTicks}"));

            Settings.waitRadius = Mathf.RoundToInt(
                list.SliderLabeled("Wait radius around Companion Spot (cells)",
                    Settings.waitRadius, 3f, 30f,
                    $"How far a visitor can be from the spot and still be considered 'waiting'.\nCurrent: {Settings.waitRadius}"));

            list.Gap(8f);

            // --- Visitor desire / cooldown ---
            list.Label("Visitor desire & cooldown");
            list.GapLine();

            float desireDelayHours = Settings.visitorDesireDelayTicks / (float)GenDate.TicksPerHour;
            desireDelayHours = list.SliderLabeled("Desire roll delay (hours)",
                desireDelayHours, 0f, 12f,
                $"How long after arriving a visitor becomes eligible to roll for 'wants a date'.\nCurrent: {desireDelayHours:0.0}h");
            Settings.visitorDesireDelayTicks = Mathf.RoundToInt(desireDelayHours * GenDate.TicksPerHour);

            Settings.visitorDesireChance = list.SliderLabeled("Desire roll chance",
                Settings.visitorDesireChance, 0f, 1f,
                $"Chance a visitor wants a date once the delay has passed.\nCurrent: {Settings.visitorDesireChance:0.00}");

            float retryCooldownHours = Settings.visitorRetryCooldownTicks / (float)GenDate.TicksPerHour;
            retryCooldownHours = list.SliderLabeled("Retry cooldown (hours)",
                retryCooldownHours, 0f, 12f,
                $"If a date fails (bed stolen, pawn invalid, etc.), how long before the visitor can try again.\nCurrent: {retryCooldownHours:0.0}h");
            Settings.visitorRetryCooldownTicks = Mathf.RoundToInt(retryCooldownHours * GenDate.TicksPerHour);

            Settings.visitorForceJobCooldownTicks = Mathf.RoundToInt(
                list.SliderLabeled("Visitor loiter re-push cooldown (ticks)",
                    Settings.visitorForceJobCooldownTicks, 60f, 6000f,
                    $"How often the tracker can forcibly re-assign the visitor 'loiter at spot' job.\nCurrent: {Settings.visitorForceJobCooldownTicks}"));

            float loiterExpiryHours = Settings.visitorLoiterExpiryTicks / (float)GenDate.TicksPerHour;
            loiterExpiryHours = list.SliderLabeled("Visitor loiter job expiry (hours)",
                loiterExpiryHours, 0.5f, 24f,
                $"Expiry interval for the visitor loiter job.\nCurrent: {loiterExpiryHours:0.0}h");
            Settings.visitorLoiterExpiryTicks = Mathf.RoundToInt(loiterExpiryHours * GenDate.TicksPerHour);

            list.Gap(8f);

            // --- Greeting / escort / claim ---
            list.Label("Greeting / escort / claim");
            list.GapLine();

            float greetingHours = Settings.greetingDurationTicks / (float)GenDate.TicksPerHour;
            greetingHours = list.SliderLabeled("Greeting duration (hours)",
                greetingHours, 0.1f, 6f,
                $"How long the greeting phase lasts.\nCurrent: {greetingHours:0.0}h");
            Settings.greetingDurationTicks = Mathf.RoundToInt(greetingHours * GenDate.TicksPerHour);

            Settings.visitorGreetingExpiryPaddingTicks = Mathf.RoundToInt(
                list.SliderLabeled("Visitor greeting expiry padding (ticks)",
                    Settings.visitorGreetingExpiryPaddingTicks, 0f, 6000f,
                    $"Extra expiry padding added to the visitor greeting job.\nCurrent: {Settings.visitorGreetingExpiryPaddingTicks}"));

            Settings.greetingChitchatIntervalTicks = Mathf.RoundToInt(
                list.SliderLabeled("Greeting chitchat interval (ticks)",
                    Settings.greetingChitchatIntervalTicks, 60f, 2000f,
                    $"How often the companion attempts a Chitchat interaction during greeting.\nCurrent: {Settings.greetingChitchatIntervalTicks}"));

            float claimGraceHours = Settings.claimGraceTicks / (float)GenDate.TicksPerHour;
            claimGraceHours = list.SliderLabeled("Claim grace window (hours)",
                claimGraceHours, 0f, 6f,
                $"How long a session has before we require the companion/visitor to be in pipeline jobs.\nCurrent: {claimGraceHours:0.0}h");
            Settings.claimGraceTicks = Mathf.RoundToInt(claimGraceHours * GenDate.TicksPerHour);

            float maxClaimHours = Settings.maxClaimTicks / (float)GenDate.TicksPerHour;
            maxClaimHours = list.SliderLabeled("Max session length (hours)",
                maxClaimHours, 0.5f, 24f,
                $"Hard timeout for a session.\nCurrent: {maxClaimHours:0.0}h");
            Settings.maxClaimTicks = Mathf.RoundToInt(maxClaimHours * GenDate.TicksPerHour);

            float followExpiryHours = Settings.visitorFollowJobExpiryTicks / (float)GenDate.TicksPerHour;
            followExpiryHours = list.SliderLabeled("Visitor follow job expiry (hours)",
                followExpiryHours, 0.5f, 24f,
                $"Expiry for the visitor follow job while escorting.\nCurrent: {followExpiryHours:0.0}h");
            Settings.visitorFollowJobExpiryTicks = Mathf.RoundToInt(followExpiryHours * GenDate.TicksPerHour);

            float followWaitHours = Settings.visitorFollowWaitDurationTicks / (float)GenDate.TicksPerHour;
            followWaitHours = list.SliderLabeled("Visitor wait-at-bed duration (hours)",
                followWaitHours, 0.1f, 12f,
                $"How long the visitor waits near the bed during the follow job.\nCurrent: {followWaitHours:0.0}h");
            Settings.visitorFollowWaitDurationTicks = Mathf.RoundToInt(followWaitHours * GenDate.TicksPerHour);

            list.Gap(8f);

            // --- Companion work ---
            list.Label("Companion work");
            list.GapLine();

            float idleHours = Settings.companionIdleAtSpotDurationTicks / (float)GenDate.TicksPerHour;
            idleHours = list.SliderLabeled("Companion idle at spot duration (hours)",
                idleHours, 0.1f, 12f,
                $"How long the companion's 'wait at spot' job lasts.\nCurrent: {idleHours:0.0}h");
            Settings.companionIdleAtSpotDurationTicks = Mathf.RoundToInt(idleHours * GenDate.TicksPerHour);

            float escortExpiryHours = Settings.companionGreetAndEscortJobExpiryTicks / (float)GenDate.TicksPerHour;
            escortExpiryHours = list.SliderLabeled("Greet & escort job expiry (hours)",
                escortExpiryHours, 0.5f, 24f,
                $"Expiry interval for the companion greet+escort job.\nCurrent: {escortExpiryHours:0.0}h");
            Settings.companionGreetAndEscortJobExpiryTicks = Mathf.RoundToInt(escortExpiryHours * GenDate.TicksPerHour);

            list.Gap(8f);

            // --- Lovin ---
            list.Label("Lovin");
            list.GapLine();

            float lovinHours = Settings.lovinDurationTicks / (float)GenDate.TicksPerHour;
            lovinHours = list.SliderLabeled("Lovin duration (hours)",
                lovinHours, 0.1f, 12f,
                $"How long the CustomLovin job runs.\nCurrent: {lovinHours:0.0}h");
            Settings.lovinDurationTicks = Mathf.RoundToInt(lovinHours * GenDate.TicksPerHour);

            float lovinExpiryHours = Settings.lovinJobExpiryTicks / (float)GenDate.TicksPerHour;
            lovinExpiryHours = list.SliderLabeled("Lovin job expiry (hours)",
                lovinExpiryHours, 0.5f, 24f,
                $"Expiry interval for the CustomLovin job.\nCurrent: {lovinExpiryHours:0.0}h");
            Settings.lovinJobExpiryTicks = Mathf.RoundToInt(lovinExpiryHours * GenDate.TicksPerHour);

            Settings.lovinPartnerExpiryPaddingTicks = Mathf.RoundToInt(
                list.SliderLabeled("Lovin partner expiry padding (ticks)",
                    Settings.lovinPartnerExpiryPaddingTicks, 0f, 6000f,
                    $"Extra expiry padding on the partner job.\nCurrent: {Settings.lovinPartnerExpiryPaddingTicks}"));

            Settings.lovinHeartFleckIntervalTicks = Mathf.RoundToInt(
                list.SliderLabeled("Heart fleck interval (ticks)",
                    Settings.lovinHeartFleckIntervalTicks, 30f, 2000f,
                    $"How often to spawn heart flecks during lovin.\nCurrent: {Settings.lovinHeartFleckIntervalTicks}"));

            GUI.enabled = oldEnabled;

            list.Gap(12f);

            if (list.ButtonText("Reset custom tuning to defaults"))
            {
                Settings.ResetCustomTuningToDefaults();
            }

            list.End();
            Widgets.EndScrollView();

            Settings.Write();
        }
    }

    internal static class ListingStandardExtensions
    {
        public static float SliderLabeled(this Listing_Standard list, string label, float value, float min, float max, string tooltip = null)
        {
            Rect rect = list.GetRect(Text.LineHeight);
            Widgets.Label(rect.LeftPart(0.70f), label);

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(rect, tooltip);

            float v = Widgets.HorizontalSlider(rect.RightPart(0.30f), value, min, max, true);
            list.Gap(list.verticalSpacing);
            return v;
        }
    }
}
