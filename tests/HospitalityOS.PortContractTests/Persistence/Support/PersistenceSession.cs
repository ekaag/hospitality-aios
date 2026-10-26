using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// One DI scope acting for one tenant: what a request or a message handler gets in production.
/// </summary>
internal sealed class PersistenceSession(AsyncServiceScope scope) : IAsyncDisposable
{
    public IServiceProvider Services => scope.ServiceProvider;

    public T Get<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    public Task<Result<CommitReceipt>> CommitAsync(CancellationToken cancellationToken) =>
        Get<IUnitOfWork>().CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
