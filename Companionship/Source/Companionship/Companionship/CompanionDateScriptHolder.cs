using Verse;
using System.Collections.Generic;

namespace Riot.Companionship
{
    public static class CompanionDateScriptHolder
    {
        private static readonly Dictionary<Pawn, DateScriptDef> scriptMap = new Dictionary<Pawn, DateScriptDef>();

        public static void Set(Pawn companion, DateScriptDef script)
        {
            scriptMap[companion] = script;
        }

        public static DateScriptDef Get(Pawn companion)
        {
            scriptMap.TryGetValue(companion, out var script);
            return script;
        }

        public static void Clear(Pawn companion)
        {
            scriptMap.Remove(companion);
        }
    }
}
