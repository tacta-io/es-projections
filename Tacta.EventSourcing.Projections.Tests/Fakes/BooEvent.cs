namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class BooEvent : IDomainEvent
    {
        public int Sequence { get; set; }

        public BooEvent(int sequence)
        {
            Sequence = sequence;
        }
    }
}