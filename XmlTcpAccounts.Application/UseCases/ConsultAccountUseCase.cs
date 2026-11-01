using XmlTcpAccounts.Application.Contracts;
using XmlTcpAccounts.Application.Exceptions;
using XmlTcpAccounts.Application.Ports;
using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Application.UseCases;

public sealed class ConsultAccountUseCase
(
    IPersonRepository personRepository,
    IAccountRepository accountRepository)
{
    public async Task<ConsultAccountResult> ExecuteAsync(
        ConsultAccountQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentNumber))
            throw new ArgumentException("Document number is required.");

        if (query.All == !string.IsNullOrWhiteSpace(query.AccountNumber))
            throw new ArgumentException(
                "Specify either All=true or an account number.");

        var person = await personRepository.FindByDocumentNumberAsync(
            query.DocumentNumber,
            cancellationToken);

        if (person is null)
            throw new AccountNotFoundException();

        if (query.All)
        {
            var accounts = await accountRepository.FindAllByPersonIdAsync(
                person.Id,
                cancellationToken);

            return new ConsultAccountResult(
                accounts.Select(ToResult).ToList());
        }

        var account = await accountRepository.FindByNumberAndPersonIdAsync(
            query.AccountNumber!,
            person.Id,
            cancellationToken);

        if (account is null)
            throw new AccountNotFoundException();

        return new ConsultAccountResult(
            new[] { ToResult(account) });
    }

    private static AccountResult ToResult(Account account)
    {
        return new AccountResult(
            account.AccountNumber,
            account.Currency,
            account.Balance,
            account.Status);
    }
}