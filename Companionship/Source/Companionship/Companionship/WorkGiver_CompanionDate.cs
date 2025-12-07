using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using UnityEngine;

namespace Riot.Companionship
{
    public class WorkGiver_CompanionDate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // Scan all pawns on the map. This is okay because we filter aggressively afterward.
            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn companion, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null) return false;

            // Can't date self
            if (client == companion) return false;

            // Companions only date visitors
            if (client.Faction == null || client.Faction.IsPlayer) return false;

            // Visitors must be spawned, alive, and not downed
            if (!client.Spawned || client.Downed || client.Dead) return false;

            // Must be humanlike
            if (!client.RaceProps.Humanlike) return false;

            // Check comp
            CompVisitorCompanionship comp = client.TryGetComp<CompVisitorCompanionship>();
            if (comp == null) return false;

            // Must be in WAITING state
            if (!comp.IsWaiting) return false;

            // Must currently have the wait job
            if (client.CurJob == null || client.CurJob.def != CompanionJobDefOf.WaitForCompanionDate)
                return false;

            // Check distance — only near the spot (7–10 tiles)
            Thing spot = CompanionSpotUtility.GetClosestSpot(client);
            if (spot == null) return false;

            float dist = client.Position.DistanceTo(spot.Position);
            if (dist > 10f) return false;

            // Companion must be able to reach the client
            if (!companion.CanReserve(client) || !companion.CanReach(client, PathEndMode.Touch, Danger.None))
                return false;

            // Check if companion is available
            if (!CompanionUtility.IsCompanionAvailable(companion))
                return false;

            // Check date script availability
            int tier = CompanionUtility.GetCompanionTier(companion);
            DateScriptDef script = DateScriptUtility.SelectScriptFor(companion, client, tier);
            if (script == null) return false;

            return true;
        }

        public override Job JobOnThing(Pawn companion, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null) return null;

            // Select script based on companion tier
            int tier = CompanionUtility.GetCompanionTier(companion);
            DateScriptDef script = DateScriptUtility.SelectScriptFor(companion, client, tier);

            if (script == null)
            {
                Log.Warning($"[Companionship] No date script available for tier {tier}.");
                return null;
            }

            // Create the job
            Job job = JobMaker.MakeJob(CompanionJobDefOf.CompanionDate, client);
            job.count = 1;
            job.playerForced = forced;

            // Attach the selected script
            JobDriver_CompanionDate.SetSelectedScript(companion, script);

            Log.Message($"[Companionship] Companion {companion.NameShortColored} is starting a date with {client.NameShortColored} using script {script.defName}.");

            return job;
        }
    }
}
