namespace Companionship
{
    /// <summary>
    /// Why a date session ended (success or failure).
    /// NOTE: This enum needs to remain backward-compatible with older names used throughout the project.
    /// </summary>
    public enum DateEndReason
    {
        None = 0,

        // -------------------------
        // Success / normal end
        // -------------------------
        Success = 10,
        Completed = Success, // alias used by newer code

        // -------------------------
        // Benign / intentional end
        // -------------------------
        Released = 20,        // older code path uses "Released"
        Cancelled = Released, // alias

        // -------------------------
        // Failures / interruptions
        // -------------------------
        Timeout = 30,
        Interrupted = 40,

        // -------------------------
        // Pipeline-specific failures
        // -------------------------
        VisitorPulledFromPipeline = 50,
        CompanionNotInPipeline = 60,
        CompanionWorkDisabled = 61,

        // -------------------------
        // Entity/target invalidation
        // -------------------------
        VisitorInvalid = 70,
        CompanionInvalid = 71,
        BedInvalid = 72,
        SpotInvalid = 73,

        // -------------------------
        // Catch-all
        // -------------------------
        Unknown = 999
    }
}
