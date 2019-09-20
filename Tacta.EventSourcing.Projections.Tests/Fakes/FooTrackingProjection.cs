using System.Threading;
using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class FooTrackingProjection : Projection,
                IHandleEvent<FooEvent>
    {
        private readonly ProjectionStateRepository _projectionStateRepository;

        public int Called { get; private set; }

        public int Sleep { get; set; } = 100;

        public FooTrackingProjection(ProjectionStateRepository projectionStateRepository) 
            : base(projectionStateRepository)
        {
            _projectionStateRepository = projectionStateRepository;
        }

        public async Task Handle(FooEvent @event)
        {
            if(Sleep > 0)  Thread.Sleep(Sleep);

            Called++;
            _projectionStateRepository.SetOffset(@event.Sequence);

            await Task.CompletedTask;
        }
    }
}