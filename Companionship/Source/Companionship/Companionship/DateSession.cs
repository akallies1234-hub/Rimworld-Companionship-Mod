using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Serializable record of a single companionship date pipeline session.
    /// This is referenced heavily by CompanionshipVisitorTracker, so it intentionally exposes public fields.
    /// </summary>
    public class DateSession : IExposable
    {
        // Participants
        public Pawn visitor;
        public Pawn companion;

        // Targets used by the session
        public Thing spot;               // companion spot (or other "start" thing)
        public Building_Bed bed;         // companion bed selected for the date

        // Session lifecycle / state machine
        public CompanionshipVisitorTracker.DateState state = CompanionshipVisitorTracker.DateState.None;
        public int lastStateChangeTick = -1;

        // Phase timing (used for stuck detection/debug summaries)
        public int startedTick = -1;          // overall session start (claim/start)
        public int greetingStartedTick = -1;
        public int escortStartedTick = -1;
        public int atBedStartedTick = -1;

        // End tracking
        public bool ended = false;
        public int endedTick = -1;
        public DateEndReason endReason = DateEndReason.None;

        // ---------------------------------------------------------------------
        // Compatibility properties (other code references these exact names)
        // ---------------------------------------------------------------------

        public Building_Bed Bed => bed;

        // (Lowercase) referenced by CompanionshipRewardUtility previously
        public int startedAtTick => startedTick;

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        public DateSession()
        {
        }

        public DateSession(Pawn visitor, Pawn companion, Thing spot, int startedTick)
        {
            this.visitor = visitor;
            this.companion = companion;
            this.spot = spot;
            this.startedTick = startedTick;
            this.lastStateChangeTick = startedTick;
            this.state = CompanionshipVisitorTracker.DateState.None;
        }

        public void SetState(CompanionshipVisitorTracker.DateState newState, int tickNow)
        {
            state = newState;
            lastStateChangeTick = tickNow;

            // Populate phase start ticks the first time we enter a phase.
            if (newState == CompanionshipVisitorTracker.DateState.Greeting && greetingStartedTick < 0)
                greetingStartedTick = tickNow;

            // Some builds may not have Escorting; this is still safe if it exists.
            if (newState.ToString() == "Escorting" && escortStartedTick < 0)
                escortStartedTick = tickNow;

            if (newState == CompanionshipVisitorTracker.DateState.AtBed && atBedStartedTick < 0)
                atBedStartedTick = tickNow;
        }

        public void End(DateEndReason reason)
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            End(reason, now);
        }

        public void End(DateEndReason reason, int tickNow)
        {
            if (ended) return;

            ended = true;
            endedTick = tickNow;
            endReason = reason;

            // If nothing ever set lastStateChangeTick, set it on end.
            if (lastStateChangeTick < 0 && tickNow >= 0)
                lastStateChangeTick = tickNow;
        }

        public void ExposeData()
        {
            // participants
            Scribe_References.Look(ref visitor, "visitor");
            Scribe_References.Look(ref companion, "companion");

            // targets
            Scribe_References.Look(ref spot, "spot");
            Scribe_References.Look(ref bed, "bed");

            // state + ticks
            Scribe_Values.Look(ref state, "state", CompanionshipVisitorTracker.DateState.None);
            Scribe_Values.Look(ref lastStateChangeTick, "lastStateChangeTick", -1);

            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref greetingStartedTick, "greetingStartedTick", -1);
            Scribe_Values.Look(ref escortStartedTick, "escortStartedTick", -1);
            Scribe_Values.Look(ref atBedStartedTick, "atBedStartedTick", -1);

            // end
            Scribe_Values.Look(ref ended, "ended", false);
            Scribe_Values.Look(ref endedTick, "endedTick", -1);
            Scribe_Values.Look(ref endReason, "endReason", DateEndReason.None);
        }
    }
}
