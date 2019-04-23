namespace Tacta.EventSourcing.Projections
{
    public interface IDomainEvent
    {
        int Sequence { get; }
    }
}