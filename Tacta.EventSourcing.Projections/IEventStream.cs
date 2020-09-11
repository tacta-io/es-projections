using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IEventStream
    {
        // Load implementation should handle fromOffset sequence as inclusive
        Task<IReadOnlyCollection<IDomainEvent>> Load(int fromOffset, int count);
    }
}