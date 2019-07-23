using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class BooProjection : Projection,
        IHandleEvent<BooEvent>,
        IHandleEvent<FooEvent>
    {
        public int Called { get; private set; }

        public BooProjection(IProjectionStateRepository projectionStateRepository) 
            : base(projectionStateRepository)
        {
        }

        public Task Handle(BooEvent @event)
        {
            Called++;
            return Task.CompletedTask;
        }

        public Task Handle(FooEvent @event)
        {
            Called++;
            return Task.CompletedTask;
        }
    }
}