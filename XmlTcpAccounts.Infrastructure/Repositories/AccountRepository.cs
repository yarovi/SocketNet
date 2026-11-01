using Microsoft.EntityFrameworkCore;
using XmlTcpAccounts.Application.Ports;
using XmlTcpAccounts.Domain;
using XmlTcpAccounts.Domain.Entities;
using XmlTcpAccounts.Infrastructure.Persistence;

namespace XmlTcpAccounts.Infrastructure.Repositories;

public sealed class AccountRepository(
    AccountsDbContext context) : IAccountRepository
{
    public Task<Account?> FindByNumberAndPersonIdAsync(
        string accountNumber,
        long personId,
        CancellationToken cancellationToken)
    {
        return context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                account =>
                    account.AccountNumber == accountNumber &&
                    account.PersonId == personId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> FindAllByPersonIdAsync(
        long personId,
        CancellationToken cancellationToken)
    {
        return await context.Accounts
            .AsNoTracking()
            .Where(account => account.PersonId == personId)
            .OrderBy(account => account.AccountNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeactivateIfActiveAsync(
        string accountNumber,
        long personId,
        CancellationToken cancellationToken)
    {
        var affectedRows = await context.Accounts
            .Where(account =>
                account.AccountNumber == accountNumber &&
                account.PersonId == personId &&
                account.Status == AccountStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        account => account.Status,
                        AccountStatus.Inactive),
                cancellationToken);

        return affectedRows == 1;
    }
}