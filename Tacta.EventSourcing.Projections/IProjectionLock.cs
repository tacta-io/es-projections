using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public interface IProjectionLock
    {
        Task<bool> IsActiveProjection(string activeIdentity);
    }
}
