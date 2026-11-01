namespace XmlTcpAccounts.Application.Contracts;

public sealed record ConsultAccountResult(IReadOnlyList<AccountResult> Accounts);