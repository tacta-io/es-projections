using System;

namespace Tacta.EventSourcing.Projections
{
    public class ConsoleExceptionHandler : IHandleException
    {
        public void Handle(Exception ex) =>
            Console.WriteLine($"ProjectionAgent Exception Thrown: {ex}");
    }
}