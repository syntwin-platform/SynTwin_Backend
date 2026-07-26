namespace Syntwin.Application.FactoryRuns.Exceptions;

public sealed class FactoryRunBarrierBusyException : Exception
{
    public FactoryRunBarrierBusyException()
        : base("Factory run barrier is busy. Please retry the arm request.")
    {
    }
}
