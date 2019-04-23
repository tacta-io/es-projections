using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IHandleEvent<T>
    {
        Task Handle(T @event);
    }
}