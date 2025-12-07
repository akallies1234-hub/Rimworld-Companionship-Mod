using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Riot.Companionship
{
    public class JobDriver_GoToCompanionSpot : JobDriver
    {
        private Pawn VisitorPawn => pawn;
        private CompVisitorCompanionship VisitorComp => VisitorPawn.TryGetComp<CompVisitorCompanionship>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);

            // Move Toil
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);

            // Arrival Toil
            Toil arrival = new Toil();
            arrival.initAction = () =>
            {
                VisitorComp?.Notify_ArrivedAtSpot(VisitorPawn);
                ReadyForNextToil();
            };
            arrival.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return arrival;
        }
    }
}
