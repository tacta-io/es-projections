using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tacta.EventSourcing.Projections
{
    public class ProjectionAgent : IDisposable
    {
        public class Configuration
        {
            public int BatchSize { get; set; } = 100;

            public int KeepAliveInterval { get; set; } = 1000;

            // NOTE - Should be less than the value from projection lock repo 
            public int KeepAliveTimeout { get; set; } = 2000;

            public int PollingInterval { get; set; } = 200;

            public IHandleException ExceptionHandler { get; private set; } =
                new ConsoleExceptionHandler();

            public void AddExceptionHandler(IHandleException handler)
            {
                ExceptionHandler = handler;
            }
        }

        private readonly Configuration _configuration = new Configuration();

        private bool _disposed = false;

        private volatile bool _isActive = false;

        private readonly IEventStream _eventStream;

        private readonly List<IProjection> _projections;

        private readonly IProjectionLock _projectionLock;

        private readonly string _agentId;

        public ProjectionAgent(IEventStream eventStream,
            IProjection[] projections,
            IProjectionLock projectionLock = null,
            string agentId = null)
        {
            _eventStream = eventStream ?? throw new InvalidEnumArgumentException("ProjectionAgent: You have to provide an event stream");
            _projections = projections.ToList();

            if (_projections == null) Console.WriteLine("ProjectionAgent: No projections registered");

            _projectionLock = projectionLock;
            _agentId = agentId;
        }

        public void Run(Action<Configuration> config)
        {
            config(_configuration);
            Run();
        }

        public void Run()
        {
            StartKeepAliveLoop();
            RunMainLoop();
        }

        public void StartKeepAliveLoop()
        {
            if (!IsUsingLocking())
            {
                _isActive = true;
                return;
            }

            Task.Run(() =>
            {
                while (!_disposed)
                {
                    Task.Delay(_configuration.KeepAliveInterval).GetAwaiter().GetResult();

                    try
                    {
                        // TODO - We need timeout here (configurable) that set's agent as inactive
                        // NOTE - Needs to be less than 
                        //_isActive = false;

                        _isActive = IsUsingLocking() && IsProjectionActive();
                    }
                    catch (Exception ex)
                    {
                        HandleException(ex);
                    }
                }
            });
        }

        public void RunMainLoop()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (_disposed) break;

                    Task.Delay(_configuration.PollingInterval).GetAwaiter().GetResult();

                    try
                    {
                        if (!_isActive)
                        {
                            Console.WriteLine($"Process {_agentId} is not active");

                            // After becoming inactive, reset projection offsets
                            // to 0, which will cause each projection to re-read 
                            // the offset from the database.
                            ResetProjections();

                            continue;
                        }

                        Console.WriteLine($"Process {_agentId} is now active");

                        BuildProjections();
                    }
                    catch (Exception ex)
                    {
                        HandleException(ex);
                    }
                }

            });
        }

        private bool IsUsingLocking() =>
            _projectionLock != null && !string.IsNullOrEmpty(_agentId);

        private bool IsProjectionActive() =>
            _projectionLock.IsActiveProjection(_agentId).GetAwaiter().GetResult();

        private void ResetProjections()
        {
            foreach (var projection in _projections)
            {
                projection.ResetOffset();
            }
        }

        private void BuildProjections()
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
                        if (!_isActive) return;

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
                // Log to console if provided exception handler fails for some reason
                Console.WriteLine($"ProjectionAgent Exception thrown: {ex}");
                Console.WriteLine($"Unable to call external exception handler due to {e}");
            }
        }

        public void Dispose()
        {
            // TODO - Handle this better
            _disposed = true;
        }
    }
}