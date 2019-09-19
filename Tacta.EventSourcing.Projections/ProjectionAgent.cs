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

            public IHandleException ExceptionHandler { get; private set; } =
                new ConsoleExceptionHandler();

            public void AddExceptionHandler(IHandleException handler)
            {
                ExceptionHandler = handler;
            }
        }

        private readonly Configuration _configuration = new Configuration();

        private IDisposable _timer;

        private readonly IEventStream _eventStream;

        private readonly List<IProjection> _projections;

        private readonly IProjectionLock _projectionLock;
        private readonly string _activeIdentity;

        private volatile bool _dispatchInProgress;

        public ProjectionAgent(IEventStream eventStream,
            IProjection[] projections,
            IProjectionLock projectionLock = null,
            string activeIdentity = null)
        {
            _eventStream = eventStream ?? throw new InvalidEnumArgumentException("ProjectionAgent: You have to provide an event stream");
            _projections = projections.ToList();

            if (_projections == null) Console.WriteLine("ProjectionAgent: No projections registered");

            _projectionLock = projectionLock;
            _activeIdentity = activeIdentity;
        }

        public IDisposable Run(Action<Configuration> config)
        {
            config.Invoke(_configuration);
            return Run();
        }

        public IDisposable Run()
        {
            var timer = new Timer();

            timer.Elapsed += OnTimer;
            timer.Interval = _configuration.PeekIntervalMilliseconds;
            timer.Enabled = true;

            _timer = timer;

            return _timer;
        }

        public void OnTimer(object source, ElapsedEventArgs e)
        {
            if (_dispatchInProgress)
            {
                // Refresh timer if rebuild lasts longer
                if(IsUsingLocking()) RefreshActiveTimer();
                return;
            }

            try
            {
                _dispatchInProgress = true;

                if (IsUsingLocking() && !IsProjectionActive())
                {
                    Console.WriteLine($"Process {_activeIdentity} is not active");
                    return;
                }

                Console.WriteLine($"Process {_activeIdentity} is now projecting");

                ProjectEvents();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                _dispatchInProgress = false;
            }
        }

        private void RefreshActiveTimer()
        {
            try
            {
                IsProjectionActive();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private bool IsUsingLocking() =>
            _projectionLock != null && _activeIdentity != null;

        private bool IsProjectionActive() =>
            _projectionLock.IsActiveProjection(_activeIdentity).GetAwaiter().GetResult();

        private void ProjectEvents()
        {
            foreach (var projection in _projections.OrderBy(p => p.Offset().GetAwaiter().GetResult()))
            {
                var offset = projection.Offset().GetAwaiter().GetResult();

                var @from = offset + 1;

                var events = _eventStream
                                 .Load(@from, _configuration.BatchSize, projection.Subscriptions())
                                 .GetAwaiter()
                                 .GetResult() ?? new List<IDomainEvent>();

                foreach (var @event in events)
                {
                    try
                    {
                        projection.HandleEvent(@event).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        HandleException(new AggregateException(new[]
                        {
                            new Exception($"ProjectionAgent: Unable to apply {@event.GetType().Name} event for {projection.GetType().Name} projection: {ex.Message}"),
                            ex
                        }));

                        break;
                    }
                }
            }
        }

        private void HandleException(Exception ex)
        {
            try
            {
                _configuration.ExceptionHandler.Handle(ex);
            }
            catch (Exception e)
            {
                // Log to console if exception handler fails for some reason
                Console.WriteLine($"ProjectionAgent Exception thrown: {ex}");
                Console.WriteLine($"Unable to call external exception handler due to {e}");
            }
        }
    }
}