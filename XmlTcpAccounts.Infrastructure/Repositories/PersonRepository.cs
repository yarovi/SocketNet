using Microsoft.EntityFrameworkCore;
using XmlTcpAccounts.Application.Ports;
using XmlTcpAccounts.Domain.Entities;
using XmlTcpAccounts.Infrastructure.Persistence;

namespace XmlTcpAccounts.Infrastructure.Repositories;

public sealed class PersonRepository(
    AccountsDbContext context) : IPersonRepository
{
    public Task<Person?> FindByDocumentNumberAsync(
        string documentNumber,
        CancellationToken cancellationToken)
    {
        return context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                person => person.DocumentNumber == documentNumber,
                cancellationToken);
    }
}