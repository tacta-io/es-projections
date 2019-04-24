namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class FooEvent : IDomainEvent
    {
        public int Sequence { get; set; }

        public FooEvent(int sequence)
        {
            Sequence = sequence;
        }
    }
}