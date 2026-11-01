namespace XmlTcpAccounts.Application.Contracts;

public sealed record DeactivateAccountCommand
(
    string DocumentNumber,
    string AccountNumber
    );