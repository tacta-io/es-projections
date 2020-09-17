using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

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

        public async Task HandleEvent(IDomainEvent @event, bool isLast)
        {
            if (@event.Sequence <= _currentOffset) return;

            var t = GetType();

            var mtd = t.GetMethod("Handle", new Type[] {@event.GetType()});

            if(mtd == null && !isLast) return;
            
            using (var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                if (mtd != null)
                    await (Task) mtd.Invoke(this, new object[] {@event});

                await _projectionStateRepository.SaveOffset(@event.Sequence, GetType().Name);

                tx.Complete();
            }

            _currentOffset = @event.Sequence;
        }

        public virtual async Task<int> Offset()
        {
            _currentOffset = await _projectionStateRepository.GetOffset(GetType().Name);

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