using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Timers;

namespace Tacta.EventSourcing.Projections
{
    public class ProjectionAgent
    {
        public class Configuration
        {
            public int BatchSize { get; set; } = 100;

            public double PeekIntervalMilliseconds { get; set; } = 1000;
        }

        private readonly Configuration _configuration = new Configuration();

        private IDisposable _timer;

        private readonly IEventStream _eventStream;

        private readonly List<IProjection> _projections;

        private bool _dispatchInProgress;

        public ProjectionAgent(IEventStream eventStream, IProjection[] projections)
        {
            _eventStream = eventStream ?? throw new InvalidEnumArgumentException("ProjectionAgent: You have to provide an event stream");
            _projections = projections.ToList();

            if (_projections == null) Console.WriteLine("ProjectionAgent: No projections registered");
        }

        public IDisposable Run(Action<Configuration> config)
        {
            config.Invoke(_configuration);

            var timer = new Timer();

            timer.Elapsed += OnTimer;
            timer.Interval = _configuration.PeekIntervalMilliseconds;
            timer.Enabled = true;

            _timer = timer;

            return _timer;
        }

        public void OnTimer(object source, ElapsedEventArgs e)
        {
            if (_dispatchInProgress) return;

            try
            {
                _dispatchInProgress = true;

                IReadOnlyCollection<IDomainEvent> events = new List<IDomainEvent>();

                foreach (var projection in _projections.OrderBy(p => p.Offset().GetAwaiter().GetResult()))
                {
                    var offset = projection.Offset().GetAwaiter().GetResult();

                    if (events.Count == 0 || (events.Count > 0 && offset >= (events.First().Sequence + events.Count)))
                    {
                        var from = offset + 1;

                        events = _eventStream.Load(@from, _configuration.BatchSize)
                                     .GetAwaiter()
                                     .GetResult() ?? new List<IDomainEvent>();
                    }

                    foreach (var @event in events)
                    {
                        try
                        {
                            projection.HandleEvent(@event).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"ProjectionAgent: Unable to apply {@event.GetType().Name} event for {projection.GetType().Name} projection: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProjectionAgent: An exception occured: {ex.Message}");
            }
            finally
            {
                _dispatchInProgress = false;
            }
        }
    }
}