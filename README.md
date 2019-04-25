# ESProjections
Provides a simple way of incrementally building / rebuilding read model projections for .NET projects

`Install-Package ESProjections [-Version x.x.x]`

*nuget URL*: [https://www.nuget.org/packages/ESProjections](https://www.nuget.org/packages/ESProjections)

## Writing Projections
In order for your projections to be functional, we have a few prerequisites.

### 1. Prepare your domain events
 Your domain events should implement `IDomainEvent`, eg.
```c#
  public MyDomainEvent : IDomainEvent
  {
      public int Sequence { get; set; }
  }
```

Sequence property should return the sequnce number of your domain event.
Note that this is very important in order for the mechanism to work properly.

### 2. Create your projection
Create a class for your projection and extend `Projection` abstract class provided by this nuget.

In order for the projection to apply your domain events (handled by the abstract class) it should also implement a `IHandleEvent<T>` interface for every event it wants to handle, eg. `IHandleEvent<MyDomainEvent>`

`IHandleEvent<T>` is defined as follows:
```c#
  public interface IHandleEvent<T>
  {
      // After applying @event, projection should keep track (persist)
      // of @event.Sequence in order to avoid duplicate event processing
      // (see IProjectionStateRepository)
      Task Handle(T @event);
  }
```

Example:
```c#
  public class MyProjection : Projection, IHandleEvent<MyDomainEvent>
  {
      private readonly ProjectionRepository _repository;

      public MyProjection(ProjectionRepository repository)
          : base(repository)
      {
          _repository = repository;
      }

      public async Task Handle(MyDomainEvent @event)
      {
          ...

          await _repository.Save( ... , @event.Sequence ); // it's important to persist the Sequence
      }
  }
```
### Setting up your projection repository
As you can see `Projection` requires it's protected counstructor to be called with `IProjectionStateRepository` as an argument.

`IProjectionStateRepository` requires you to implement one simple method and is defined as follows:
```c#
    public interface IProjectionStateRepository
    {
        // GetOffset should return the sequence of the last event it processed
        // (see IHandleEvent for info on where to persist last processed sequence)
        Task<int> GetOffset();
    }
```
This is neccessary in order to track last processed domain event for each projection.

You could implement `IProjectionStateRepository` in a separate class if you want, but it's more convenient for the projection repository to implement it itself.

### Rebuilding from scratch
Because your projection tracks its offset, it inherently decides on when to rebuild itself from scratch. `Projection` base class makes this as simple as deleting entries from the projection table and calling `ResetOffset()` from your projection. This will cause the projection to rehydrate it's offset from the database.

### 3. Set up your event stream
One last thing you need to do (but not least), is to set up your event stream.

This is done by implementing `IEventStream` interface which you will probably want to do in your event store repository, eg:
```c#
 public class EventStoreRepository : IEventStream
 {
      public async Task<IReadOnlyCollection<IDomainEvent>> Load(int fromOffset, int count)
      {
          ... 
      }
 }
```
This method should return `count` number of events starting from `fromOffset` sequence (inclusive)

You want this method to be as optimized as possible.

### 4. Run `ProjectionAgent`
Now all we need to do is to start the `ProjectionAgent` by providing it with our `IProjection` and `IEventStream` implementations.

Example using Unity:
Somwhere in your unity configuration:
```c#
  container.RegisterType<IEventStream, EventStoreRepository>(new ContainerControlledLifetimeManager());
  container.RegisterType<IProjection, MyProjection>("MyProjection", new ContainerControlledLifetimeManager());
  container.RegisterType<IProjection, AnotherProjection>("AnotherProjection", new ContainerControlledLifetimeManager());
```

Somwhere in your `Startup` or `Program` classes:
```c#
  var projectionAgent = new ProjectionAgent(
    container.Resolve<IEventStream>(),
    container.Resolve<IProjection[]>()
  );

  // OR: var projectionAgent = container.Resolve<ProjectionAgent>();

  _disposable = projectionAgent.Run(config =>
  {
      // Number of events to load in each call to IEventStream
      config.BatchSize = 50;

      // Interval at which to query the IEventStream
      config.PeekIntervalMilliseconds = 500;
  });

  // OR with default config: _disposable = projectionAgent.Run();
  ...

  // Remember to dispose on shutdown
  _disposable.Dispose();
```

`ProjectionAgent` is now running in the background until `Dispose` is called.
