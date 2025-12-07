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

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);

            Toil arrival = new Toil
            {
                initAction = () =>
                {
                    VisitorComp?.Notify_ArrivedAtSpot(VisitorPawn);
                    ReadyForNextToil();
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            yield return arrival;
        }
    }
}
