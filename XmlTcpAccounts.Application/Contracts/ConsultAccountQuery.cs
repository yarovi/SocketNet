namespace XmlTcpAccounts.Application.Contracts;

public sealed record ConsultAccountQuery
(
    string DocumentNumber,
    string? AccountNumber,
    bool All);