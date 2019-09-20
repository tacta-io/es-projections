using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using NSubstitute.Core;

namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class ProjectionStateRepository : IProjectionStateRepository, IProjectionLock
    {
        public int Called { get; private set; }

        private int _offset;

        private string _agent1;

        private string _agent2;

        public ProjectionStateRepository()
        {
            
        }

        public ProjectionStateRepository(string agent1, string agent2)
        {
            _agent1 = agent1;
            _agent2 = agent2;
        }

        public Task<int> GetOffset()
        {
            return Task.FromResult(_offset);
        }

        public void SetOffset(int offset)
        {
            Called++;
            _offset = offset;
        }

        public async Task<bool> IsActiveProjection(string activeIdentity)
        {
            return await Task.FromResult(true);
        }
    }
}