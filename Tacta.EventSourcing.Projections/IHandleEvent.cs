using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IHandleEvent<T>
    {
        // After applying @event, projection should keep track (persist)
        // of @event.Sequence in order to avoid duplicate event processing
        // (see IProjectionStateRepository)
        Task Handle(T @event);
    }
}