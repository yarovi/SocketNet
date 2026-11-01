namespace XmlTcpAccounts.Domain.Entities;

public sealed class Person
{
    public long Id { get; }
    public string DocumentNumber { get; }
    public string FullName { get; }

    public Person(
        long id,
        string documentNumber,
        string fullName)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            throw new ArgumentException(
                "Document number is required.",
                nameof(documentNumber));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException(
                "Full name is required.",
                nameof(fullName));
        }

        Id = id;
        DocumentNumber = documentNumber;
        FullName = fullName;
    }
}
