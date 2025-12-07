using System.Collections.Generic;
using Verse;

namespace Riot.Companionship
{
    public static class CompanionDateScriptHolder
    {
        private static readonly Dictionary<Pawn, DateScriptDef> scriptMap = new Dictionary<Pawn, DateScriptDef>();

        public static void Set(Pawn pawn, DateScriptDef script)
        {
            scriptMap[pawn] = script;
        }

        public static DateScriptDef Get(Pawn pawn)
        {
            scriptMap.TryGetValue(pawn, out var script);
            return script;
        }

        public static void Clear(Pawn pawn)
        {
            scriptMap.Remove(pawn);
        }
    }
}
