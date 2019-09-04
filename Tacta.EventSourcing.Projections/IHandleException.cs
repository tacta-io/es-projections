using System;

namespace Tacta.EventSourcing.Projections
{
    public interface IHandleException
    {
        void Handle(Exception ex);
    }
}