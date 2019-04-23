using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IProjection
    {
        // HandleEvent should apply @event if projections current offset is higher than eventOffset  
        Task HandleEvent(IDomainEvent @event);

        // Offset should return projection current offset
        Task<int> Offset();
    }
}