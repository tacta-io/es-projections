using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IProjection
    {
        // HandleEvent should apply @event if projections current offset is higher than eventOffset  
        Task HandleEvent(IDomainEvent @event);

        // Offset should return projection current offset
        // eg. double dispatch to projection state repository GetOffset and store it in memory
        Task<int> Offset();

        // Should return list of names of events relevant to the projection
        List<string> Subscriptions();
    }
}