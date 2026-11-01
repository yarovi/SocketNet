using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Application.Ports;

public interface IPersonRepository
{
    Task<Person?> FindByDocumentNumberAsync(
        string documentNumber,
        CancellationToken cancellationToken);
}