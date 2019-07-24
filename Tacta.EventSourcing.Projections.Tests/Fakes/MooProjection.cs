namespace Tacta.EventSourcing.Projections.Tests.Fakes
{
    public class MooProjection : Projection
    {
        public int Called { get; private set; }

        public MooProjection(IProjectionStateRepository projectionStateRepository) 
            : base(projectionStateRepository)
        {
        }
    }
}