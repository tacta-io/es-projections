using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tacta.EventSourcing.Projections.Tests.Fakes;

namespace Tacta.EventSourcing.Projections.Tests
{
    [TestClass]
    public class ProjectionAgentTests
    {
        [TestMethod]
        public void TestProjectionAgentCallsIProjectionHandle()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, subscriptions)
                .Returns(new List<IDomainEvent>
            {
                new FooEvent(1),
            });

            var projection = Substitute.For<IProjection>();

            projection.Subscriptions().Returns(subscriptions);

            var projectionAgent = new ProjectionAgent(eventStream, new[] { projection });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            projection.Received().HandleEvent(Arg.Any<IDomainEvent>());
        }

        [TestMethod]
        public void TestProjectionAppliesOneEventForSameSequence()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>
            {
                new FooEvent(1),
                new FooEvent(1),
                new FooEvent(1),
                new FooEvent(1),
                new FooEvent(1),
            });

            var projectionState = Substitute.For<IProjectionStateRepository>();

            projectionState.GetOffset().Returns(0);

            var projection1 = new FooProjection(projectionState);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            Assert.AreEqual(1, projection1.Called);
        }

        [TestMethod]
        public void TestProjectionTracksEventSequence()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>
            {
                new FooEvent(1),
                new FooEvent(2),
                new FooEvent(3),
                new FooEvent(4),
                new FooEvent(5),
            });

            var projectionState = Substitute.For<IProjectionStateRepository>();

            projectionState.GetOffset().Returns(0);

            var projection1 = new FooProjection(projectionState);
            var projection2 = new FooProjection(projectionState);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            Assert.AreEqual(5, projection1.Called);
            Assert.AreEqual(5, projection2.Called);
        }

        [TestMethod]
        public void TestProjectionAgentDoesNotReuseEventList()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream.Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
                new FooEvent(2),
                new FooEvent(3),
                new FooEvent(4),
                new FooEvent(5),
            });

            var projectionState = Substitute.For<IProjectionStateRepository>();

            projectionState.GetOffset().Returns(0);

            var projection1 = new FooProjection(projectionState);
            var projection2 = new FooProjection(projectionState);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            eventStream.Received(2).Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)));
        }

        [TestMethod]
        public void TestProjectionAgentQueriesEventStreamPerProjection()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>()	
            {	
                new FooEvent(1),	
                new FooEvent(2),	
                new FooEvent(3),	
                new FooEvent(4),	
                new FooEvent(5),	
            });

            eventStream
                .Load(15, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>()
            {
                new FooEvent(15),
                new FooEvent(16),
                new FooEvent(17),
            });

            var projectionState1 = Substitute.For<IProjectionStateRepository>();

            projectionState1.GetOffset().Returns(0);

            var projectionState2 = Substitute.For<IProjectionStateRepository>();

            projectionState2.GetOffset().Returns(14);

            var projection1 = new FooProjection(projectionState1);
            var projection2 = new FooProjection(projectionState2);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

           Thread.Sleep(100);

            disposable.Dispose();

            eventStream.Received(1).Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)));
            eventStream.Received(1).Load(15, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)));
        }

        [TestMethod]
        public void TestProjectionAgentContinuesToPeekAfterProjectionsCatchUp()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
                new FooEvent(2),
                new FooEvent(3),
                new FooEvent(4),
                new FooEvent(5),
            });

            eventStream
                .Load(6, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>()
            {
                new FooEvent(7),
                new FooEvent(8),
                new FooEvent(9),
            });

            var projectionState1 = Substitute.For<IProjectionStateRepository>();

            projectionState1.GetOffset().Returns(0);

            var projectionState2 = Substitute.For<IProjectionStateRepository>();

            projectionState2.GetOffset().Returns(5);

            var projection1 = new FooProjection(projectionState1);
            var projection2 = new FooProjection(projectionState2);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            eventStream.Received().Load(10, 50, Arg.Is<List<string>>(x => subscriptions.SequenceEqual(x)));
        }

        // Test if projection throws, other projections are built
        // Test other exceptions - assert building continues
        // Test ResetOffset
        [TestMethod]
        public void TestProjectionAgentWithProjectionLockCallsIProjectionHandle()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions = new List<string> { typeof(FooEvent).Name };

            eventStream
                .Load(1, 50, subscriptions)
                .Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
            });

            var projectionLock = Substitute.For<IProjectionLock>();
                projectionLock.IsActiveProjection(Arg.Any<string>()).Returns(true);

            const string activeIdentity = "identity";

            var projection = Substitute.For<IProjection>();

            projection.Subscriptions().Returns(subscriptions);

            var projectionAgent = new ProjectionAgent(eventStream,
                new[] { projection }, 
                projectionLock,
                activeIdentity);

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            projection.Received().HandleEvent(Arg.Any<IDomainEvent>());
        }

        [TestMethod]
        public void TestProjectionAgentQueriesEventStreamByCorrespondingSubscriptions()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions1 = new List<string> { typeof(FooEvent).Name };

            var subscriptions2 = new List<string> { typeof(BooEvent).Name, typeof(FooEvent).Name };

            var projectionState1 = Substitute.For<IProjectionStateRepository>();

            projectionState1.GetOffset().Returns(0);

            var projectionState2 = Substitute.For<IProjectionStateRepository>();

            projectionState2.GetOffset().Returns(20);

            var projection1 = new FooProjection(projectionState1);
            var projection2 = new BooProjection(projectionState2);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 50;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            eventStream.Received().Load(1, 50, Arg.Is<List<string>>(x => subscriptions1.SequenceEqual(x)));
            eventStream.Received().Load(21, 50, Arg.Is<List<string>>(x => subscriptions2.SequenceEqual(x)));
        }

        
        [TestMethod]
        public void TestProjectionAgentQueriesEventStreamByCorrespondingProjectionOffset()
        {
            var eventStream = Substitute.For<IEventStream>();

            var subscriptions1 = new List<string> { typeof(FooEvent).Name };

            var subscriptions2 = new List<string> { typeof(BooEvent).Name, typeof(FooEvent).Name };

            eventStream
                .Load(1, 3, Arg.Is<List<string>>(x => subscriptions1.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>
                {
                    new FooEvent(1), 
                    new FooEvent(2), 
                    new FooEvent(3)
                });

            eventStream
                .Load(21, 3, Arg.Is<List<string>>(x => subscriptions2.SequenceEqual(x)))
                .Returns(new List<IDomainEvent>
                {
                    new BooEvent(21),
                    new BooEvent(22),
                    new BooEvent(23)
                });

            var projectionState1 = Substitute.For<IProjectionStateRepository>();

            projectionState1.GetOffset().Returns(0);

            var projectionState2 = Substitute.For<IProjectionStateRepository>();

            projectionState2.GetOffset().Returns(20);

            var projection1 = new FooProjection(projectionState1);
            var projection2 = new BooProjection(projectionState2);

            var projectionAgent = new ProjectionAgent(eventStream, new IProjection[] { projection1, projection2 });

            var disposable = projectionAgent.Run(config =>
            {
                config.BatchSize = 3;
                config.PeekIntervalMilliseconds = 1;
            });

            Thread.Sleep(100);

            disposable.Dispose();

            eventStream.Received().Load(1, 3, Arg.Is<List<string>>(x => subscriptions1.SequenceEqual(x)));
            eventStream.Received().Load(21, 3, Arg.Is<List<string>>(x => subscriptions2.SequenceEqual(x)));
            eventStream.Received().Load(4, 3, Arg.Is<List<string>>(x => subscriptions1.SequenceEqual(x)));
            eventStream.Received().Load(24, 3, Arg.Is<List<string>>(x => subscriptions2.SequenceEqual(x)));
        }

        [TestMethod]
        public void TestSubscriptionsMethodReturnsCorrespondingListOfEvents()
        {
            var subscriptions1 = new List<string> { typeof(BooEvent).Name, typeof(FooEvent).Name };
            var subscriptions2 = new List<string>();

            var projectionState1 = Substitute.For<IProjectionStateRepository>();
            var projectionState2 = Substitute.For<IProjectionStateRepository>();

            var projection1 = new BooProjection(projectionState1);
            var projection2 = new MooProjection(projectionState2);

            Assert.AreEqual(projection1.Subscriptions().SequenceEqual(subscriptions1), true);
            Assert.AreEqual(projection2.Subscriptions().SequenceEqual(subscriptions2), true);
        }
    }
}
