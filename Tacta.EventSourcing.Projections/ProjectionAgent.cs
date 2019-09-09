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

            public IHandleException ExceptionHandler { get; private set; }

            public void AddExceptionHandler(IHandleException handler)
            {
                ExceptionHandler = handler;
            }
        }

        private readonly Configuration _configuration = new Configuration();

        private IDisposable _timer;

        private readonly IEventStream _eventStream;

        private readonly List<IProjection> _projections;

        private bool _dispatchInProgress;
        private readonly IProjectionLock _projectionLock;
        private readonly string _activeIdentity;

        public static volatile bool IsActiveProjection = false;
        public static volatile bool HasChangedToActive = false;

        public ProjectionAgent(IEventStream eventStream,
            IProjection[] projections,
            IProjectionLock projectionLock = null, 
            string activeIdentity = null)
        {
            _eventStream = eventStream ?? throw new InvalidEnumArgumentException("ProjectionAgent: You have to provide an event stream");
            _projections = projections.ToList();
            
            if (_projections == null) Console.WriteLine("ProjectionAgent: No projections registered");

            if (IsUsingLocking(projectionLock, activeIdentity))
            {
                _projectionLock = projectionLock;
                _activeIdentity = activeIdentity;
            }
        }

        private static bool IsUsingLocking(IProjectionLock projectionLock, string activeIdentity)
        {
            return projectionLock != null && activeIdentity != null;
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

        private bool IsProjectionActive()
        {
            var isActive = _projectionLock.IsActiveProjection(_activeIdentity).GetAwaiter().GetResult();
            if (IsActiveProjection && isActive)
            {
                HasChangedToActive = false;
                
            }
            else if (IsActiveProjection && !isActive)
            {
                IsActiveProjection = false;
                HasChangedToActive = false;
            }
            else if (!IsActiveProjection && isActive)
            {
                IsActiveProjection = true;
                HasChangedToActive = true;
            }
            else
            {
                IsActiveProjection = false;
                HasChangedToActive = false;
            }

            return IsActiveProjection;
        }

        public void OnTimer(object source, ElapsedEventArgs e)
        {
            if (_dispatchInProgress)
            {
                // refresh timer if rebuild lasts longer
                // we need this to activate inactive agent once active agent is deactivated
                if (IsProjectionActive()) ToggleDispatchProgress();
                return;
            }

            ToggleDispatchProgress();

            if (IsUsingLocking(_projectionLock, _activeIdentity))
            {
                if (IsProjectionActive())
                {
                    ProjectEvents();
                    Console.WriteLine($"Process {_activeIdentity} is now projecting");
                }
                else
                {
                    Console.WriteLine($"Process {_activeIdentity} is not active");
                }
            }
            else
            {
                ProjectEvents();
            }
        }

        private void ProjectEvents()
        {
            try
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
                            var exMessage =
                                $"ProjectionAgent: Unable to apply {@event.GetType().Name} event for {projection.GetType().Name} projection: {ex.Message}";

                            try
                            {
                                var exception = new AggregateException(new[]
                                {
                                    new Exception(exMessage), 
                                    ex
                                });

                                _configuration.ExceptionHandler?.Handle(exception);
                            }
                            finally
                            {
                                Console.WriteLine(exMessage);
                            }
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
                ToggleDispatchProgress();
            }
        }

        private void ToggleDispatchProgress() => _dispatchInProgress = !_dispatchInProgress;
    }
}