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
            if (_currentOffset == 0)
                _currentOffset = await _projectionStateRepository.GetOffset();

            return _currentOffset;
        }

        public virtual List<string> Subscriptions()
        {
            return GetType()
                .GetInterfaces()
                .Where(x => x.Name.Contains(typeof(IHandleEvent<>).Name))
                .Select(x => x.GenericTypeArguments?.FirstOrDefault()?.Name)
                .ToList();
        }

        public void ResetOffset() => _currentOffset = 0;
    }
}