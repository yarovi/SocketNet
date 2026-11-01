using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Application.Ports;

public interface IAccountRepository
{
    Task<Account?> FindByNumberAndPersonIdAsync(
        string accountNumber,
        long personId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> FindAllByPersonIdAsync(
        long personId,
        CancellationToken cancellationToken);

    Task<bool> DeactivateIfActiveAsync(
        string accountNumber,
        long personId,
        CancellationToken cancellationToken);
}