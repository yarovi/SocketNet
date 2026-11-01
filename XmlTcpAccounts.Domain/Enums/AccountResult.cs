namespace XmlTcpAccounts.Domain;

public sealed record AccountResult(
    string AccountNumber,
    Currency Currency,
    decimal Balance,
    AccountStatus Status
);