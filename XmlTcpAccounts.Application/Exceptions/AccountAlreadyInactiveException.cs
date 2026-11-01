namespace XmlTcpAccounts.Application.Exceptions;

public class AccountAlreadyInactiveException : Exception
{
    public AccountAlreadyInactiveException()
        : base("Account is already inactive.")
    {
    }
}