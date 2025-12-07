using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace Riot.Companionship
{
    public class WorkGiver_CompanionDate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
                yield return p;
        }

        public override bool HasJobOnThing(Pawn companion, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null) return false;
            if (client == companion) return false;

            if (!client.Spawned || client.Dead || client.Downed) return false;
            if (!client.RaceProps.Humanlike) return false;
            if (client.Faction == null || client.Faction.IsPlayer) return false;

            var comp = client.TryGetComp<CompVisitorCompanionship>();
            if (comp == null || !comp.IsWaiting) return false;

            if (client.CurJob == null ||
                client.CurJob.def != CompanionshipDefOf.WaitForCompanionDate)
                return false;

            Thing spot = CompSpotUtility.GetClosestSpot(client);
            if (spot == null) return false;
            if (client.Position.DistanceToSquared(spot.Position) > 100) return false;

            if (!companion.CanReserve(client)) return false;
            if (!companion.CanReach(client, PathEndMode.Touch, Danger.None)) return false;

            var script = DateScriptUtility.SelectScriptFor(companion, client);
            if (script == null) return false;

            return true;
        }

        public override Job JobOnThing(Pawn companion, Thing t, bool forced = false)
        {
            Pawn client = t as Pawn;
            if (client == null) return null;

            var script = DateScriptUtility.SelectScriptFor(companion, client);
            if (script == null) return null;

            CompanionDateScriptHolder.Set(companion, script);

            Job job = JobMaker.MakeJob(CompanionshipDefOf.CompanionDate, client);
            job.playerForced = forced;
            return job;
        }
    }
}
