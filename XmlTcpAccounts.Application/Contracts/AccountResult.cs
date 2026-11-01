using XmlTcpAccounts.Domain;

namespace XmlTcpAccounts.Application.Contracts;

public sealed record AccountResult(
    string AccountNumber,
    Currency Currency,
    decimal Balance,
    AccountStatus Status);