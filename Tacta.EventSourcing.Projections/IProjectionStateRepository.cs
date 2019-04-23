using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IProjectionStateRepository
    {
        // GetOffset should return the sequence of the last event it processed
        // (see IHandleEvent for info on where to persist last processed sequence)
        Task<int> GetOffset();
    }
}