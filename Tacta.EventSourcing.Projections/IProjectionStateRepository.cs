using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IProjectionStateRepository
    {
        Task<int> GetOffset();
    }
}