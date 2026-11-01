using XmlTcpAccounts.Application.Contracts;
using XmlTcpAccounts.Application.Exceptions;
using XmlTcpAccounts.Application.Ports;
using XmlTcpAccounts.Domain;

namespace XmlTcpAccounts.Application.UseCases;

public sealed class DeactivateAccountUseCase
(
    IPersonRepository personRepository,
    IAccountRepository accountRepository)
{
    public async Task ExecuteAsync(
        DeactivateAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentNumber) ||
            string.IsNullOrWhiteSpace(command.AccountNumber))
        {
            throw new ArgumentException(
                "Document number and account number are required.");
        }

        var person = await personRepository.FindByDocumentNumberAsync(
            command.DocumentNumber,
            cancellationToken);

        if (person is null)
            throw new AccountNotFoundException();

        var updated = await accountRepository.DeactivateIfActiveAsync(
            command.AccountNumber,
            person.Id,
            cancellationToken);

        if (updated)
            return;

        var account = await accountRepository.FindByNumberAndPersonIdAsync(
            command.AccountNumber,
            person.Id,
            cancellationToken);

        if (account is null)
            throw new AccountNotFoundException();

        if (account.Status == AccountStatus.Inactive)
            throw new AccountAlreadyInactiveException();

        throw new InvalidOperationException(
            "Unable to deactivate account.");
    }
}