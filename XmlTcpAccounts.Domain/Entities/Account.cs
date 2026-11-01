namespace XmlTcpAccounts.Domain.Entities;

public sealed class Account
{
    public long Id { get; }
    public long PersonId { get; }
    public string AccountNumber { get; }
    public Currency Currency { get; }
    public decimal Balance { get; }
    public AccountStatus Status { get; private set; }

    public Account(
        long id,
        long personId,
        string accountNumber,
        Currency currency,
        decimal balance,
        AccountStatus status)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException(
                "Account number is required.",
                nameof(accountNumber));

        if (balance < 0)
            throw new ArgumentOutOfRangeException(
                nameof(balance),
                "Balance cannot be negative.");

        Id = id;
        PersonId = personId;
        AccountNumber = accountNumber;
        Currency = currency;
        Balance = balance;
        Status = status;
    }

    public bool IsActive()
    {
        return Status == AccountStatus.Active;
    }

    public void Deactivate()
    {
        if (!IsActive())
            throw new InvalidOperationException(
                "Account is already inactive.");

        Status = AccountStatus.Inactive;
    }
}