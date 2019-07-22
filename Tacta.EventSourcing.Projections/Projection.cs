using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public abstract class Projection : IProjection
    {
        private readonly IProjectionStateRepository _projectionStateRepository;

        private int _currentOffset;

        protected Projection(IProjectionStateRepository projectionStateRepository)
        {
            _projectionStateRepository = projectionStateRepository;
        }

        public async Task HandleEvent(IDomainEvent @event)
        {
            if (@event.Sequence <= _currentOffset) return;

            var t = GetType();

            var mtd = t.GetMethod("Handle", new Type[] { @event.GetType() });

            if (mtd == null) return;

            await (Task)mtd?.Invoke(this, new object[] { @event });

            _currentOffset = @event.Sequence;
        }

        public virtual async Task<int> Offset()
        {
            if (_currentOffset == 0 || ProjectionAgent.HasChangedToActive)
                _currentOffset = await _projectionStateRepository.GetOffset();

            return _currentOffset;
        }

        public virtual List<string> Subscriptions()
        {
            return (from method in GetType().GetMethods() 
                    where method.Name == "Handle" 
                    select method.GetParameters().Select(p => p.ParameterType.Name).First()).ToList();
        }

        public void ResetOffset() => _currentOffset = 0;
    }
}