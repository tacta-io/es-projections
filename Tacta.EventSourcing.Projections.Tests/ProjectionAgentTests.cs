using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
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

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
            });

            var projection = Substitute.For<IProjection>();

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

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
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

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
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
        public void TestProjectionAgentReusesEventList()
        {
            var eventStream = Substitute.For<IEventStream>();

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
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

            eventStream.Received(1).Load(1, 50);
        }

        [TestMethod]
        public void TestProjectionAgentQueriesEventStreamPerProjectionIfNeeded()
        {
            var eventStream = Substitute.For<IEventStream>();

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
                new FooEvent(2),
                new FooEvent(3),
                new FooEvent(4),
                new FooEvent(5),
            });

            eventStream.Load(6, 50).Returns(new List<IDomainEvent>()
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

            eventStream.Received(1).Load(1, 50);
            eventStream.Received(1).Load(6, 50);
        }

        [TestMethod]
        public void TestProjectionAgentContinuesToPeekAfterProjectionsCatchUp()
        {
            var eventStream = Substitute.For<IEventStream>();

            eventStream.Load(1, 50).Returns(new List<IDomainEvent>()
            {
                new FooEvent(1),
                new FooEvent(2),
                new FooEvent(3),
                new FooEvent(4),
                new FooEvent(5),
            });

            eventStream.Load(6, 50).Returns(new List<IDomainEvent>()
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

            eventStream.Received().Load(10, 50);
        }

        // Test if projection throws, other projections are built
        // Test other exceptions - assert building continues
        // Test ResetOffset

    }
}
