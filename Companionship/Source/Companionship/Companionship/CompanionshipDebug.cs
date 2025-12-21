using Verse;

namespace Companionship
{
    public static class CompanionshipDebug
    {
        public static bool ShowOverlay
        {
            get
            {
                // Overlay should be shown only if user enables it AND DevMode is on.
                // (We still let the setting exist even if DevMode is off.)
                return Prefs.DevMode
                    && CompanionshipMod.Settings != null
                    && CompanionshipMod.Settings.enableDebugOverlay;
            }
        }

        public static bool VerboseLogging
        {
            get
            {
                return CompanionshipMod.Settings != null && CompanionshipMod.Settings.verboseLogging;
            }
        }

        public static void DevLog(string message)
        {
            if (!VerboseLogging) return;
            Log.Message(message);
        }
    }
}
