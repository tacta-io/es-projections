using System;
using System.Collections.Concurrent;
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

            public int PollingInterval { get; set; } = 200;

            public IHandleException ExceptionHandler { get; private set; } =
                new ConsoleExceptionHandler();

            public void AddExceptionHandler(IHandleException handler)
            {
                ExceptionHandler = handler;
            }
        }

        private readonly Configuration _configuration = new Configuration();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly CancellationToken _cancellationToken;

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
            _cancellationToken = _cts.Token;

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
            if (IsUsingLocking())
                RunKeepAliveLoop();
            else
                _isActive = true;

            RunMainLoop();
        }

        public void RunKeepAliveLoop()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (_cancellationToken.IsCancellationRequested) break;

                    Task.Delay(_configuration.KeepAliveInterval).GetAwaiter().GetResult();

                    var checkIfActiveTask = Task.Run(() =>
                    {
                        try
                        {
                            _isActive = IsProjectionActive();
                        }
                        catch (Exception ex)
                        {
                            HandleException(ex);
                        }
                    });

                    if (!checkIfActiveTask.Wait(_configuration.KeepAliveInterval)) _isActive = false;
                }
            }, _cancellationToken);
        }

        private bool IsUsingLocking() =>
            _projectionLock != null && !string.IsNullOrEmpty(_agentId);

        private bool IsProjectionActive() =>
            _projectionLock.IsActiveProjection(_agentId).GetAwaiter().GetResult();

        public void RunMainLoop()
        {
            // TODO - Start task for each projection
            Task.Run(() =>
            {
                while (true)
                {
                    if (_cancellationToken.IsCancellationRequested) break;

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
            }, _cancellationToken);
        }

        private void BuildProjections()
        {
            foreach (var projection in _projections)
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
                        if (!_isActive)
                        {
                            ResetProjections();

                            return;
                        }

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

        private void ResetProjections()
        {
            foreach (var projection in _projections)
            {
                projection.ResetOffset();
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
            _cts.Cancel();
        }
    }
}