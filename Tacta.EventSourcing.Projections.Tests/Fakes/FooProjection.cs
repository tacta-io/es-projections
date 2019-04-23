using System.Threading.Tasks;
using NSubstitute.Core;

namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class FooProjection : Projection,
                IHandleEvent<FooEvent>
    {
        public int Called { get; private set; }

        public FooProjection(IProjectionStateRepository projectionStateRepository) 
            : base(projectionStateRepository)
        {
        }

        public Task Handle(FooEvent @event)
        {
            Called++;
            return Task.CompletedTask;
        }
    }
}