using System.Collections.Generic;
using Verse;

namespace Riot.Companionship
{
    /// <summary>
    /// Data-only definition for a companion date script.
    /// Each script has a tier requirement and a sequence of high-level actions
    /// (e.g. "WalkTogether", "JoyActivity", "MealTogether", "Lovin") that
    /// will later be translated into Toils in JobDriver_CompanionDate.
    /// </summary>
    public class DateScriptDef : Def
    {
        /// <summary>
        /// Minimum companion tier required to use this script.
        /// Typical range: 1–5.
        /// </summary>
        public int tier = 1;

        /// <summary>
        /// Ordered list of high-level actions that make up the date.
        /// Examples:
        ///   - { "WalkTogether", "Lovin" }
        ///   - { "WalkTogether", "JoyActivity", "MealTogether", "Lovin" }
        /// We'll map these strings to specific Toils later.
        /// </summary>
        public List<string> sequence = new List<string>();

        // In the future we can add:
        // - tags (romantic, playful, counseling, etc.)
        // - minimum mood / trait / need requirements
        // - weighting / selection preferences
    }
}
