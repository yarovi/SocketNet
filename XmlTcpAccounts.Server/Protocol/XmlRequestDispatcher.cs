using System.Xml;
using System.Xml.Linq;
using XmlTcpAccounts.Application.Contracts;
using XmlTcpAccounts.Application.Exceptions;
using XmlTcpAccounts.Application.UseCases;

namespace XmlTcpAccounts.Server.Protocol;

public sealed class XmlRequestDispatcher(
    ConsultAccountUseCase consultAccount,
    DeactivateAccountUseCase deactivateAccount)
{
    public async Task<string> DispatchAsync(
        string xml,
        CancellationToken cancellationToken)
    {
        string operation = "";
        string correlationId = "";

        try
        {
            using var reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });

            var document = XDocument.Load(reader);

            if (document.Root?.Name != "Request")
                return Error("", "", "INVALID_REQUEST");

            var header = document.Root.Element("Header");
            var body = document.Root.Element("Body");

            operation = header?.Element("Operation")?.Value ?? "";
            correlationId = header?.Element("CorrelationId")?.Value ?? "";
            var version = header?.Element("Version")?.Value ?? "";

            if (header is null || body is null ||
                string.IsNullOrWhiteSpace(operation) ||
                string.IsNullOrWhiteSpace(correlationId))
            {
                return Error(operation, correlationId, "INVALID_REQUEST");
            }

            if (version != "1.0")
                return Error(operation, correlationId, "UNSUPPORTED_VERSION");

            return operation switch
            {
                "CONSULT_ACCOUNT" => await ConsultAsync(
                    body, operation, correlationId, cancellationToken),

                "DEACTIVATE_ACCOUNT" => await DeactivateAsync(
                    body, operation, correlationId, cancellationToken),

                _ => Error(operation, correlationId, "UNKNOWN_OPERATION")
            };
        }
        catch (XmlException)
        {
            return Error(operation, correlationId, "INVALID_XML");
        }
        catch (ArgumentException)
        {
            return Error(operation, correlationId, "INVALID_REQUEST");
        }
        catch (AccountNotFoundException)
        {
            return Error(operation, correlationId, "ACCOUNT_NOT_FOUND");
        }
        catch (AccountAlreadyInactiveException)
        {
            return Error(
                operation, correlationId, "ACCOUNT_ALREADY_INACTIVE");
        }
    }

    private async Task<string> ConsultAsync(
        XElement body,
        string operation,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var documentNumber =
            body.Element("DocumentNumber")?.Value ?? "";

        var accountNumber =
            body.Element("AccountNumber")?.Value;

        var allText = body.Element("All")?.Value;
        bool all = false;

        if (allText is not null &&
            !bool.TryParse(allText, out all))
        {
            throw new ArgumentException("Invalid All value.");
        }

        var result = await consultAccount.ExecuteAsync(
            new ConsultAccountQuery(documentNumber, accountNumber, all),
            cancellationToken);

        var accounts = result.Accounts.Select(account =>
            new XElement("Account",
                new XElement("AccountNumber", account.AccountNumber),
                new XElement("Currency", account.Currency),
                new XElement("Balance", account.Balance),
                new XElement("Status",
                    account.Status.ToString().ToUpperInvariant())));

        return Success(
            operation,
            correlationId,
            new XElement("Accounts", accounts));
    }

    private async Task<string> DeactivateAsync(
        XElement body,
        string operation,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var documentNumber =
            body.Element("DocumentNumber")?.Value ?? "";

        var accountNumber =
            body.Element("AccountNumber")?.Value ?? "";

        await deactivateAccount.ExecuteAsync(
            new DeactivateAccountCommand(
                documentNumber,
                accountNumber),
            cancellationToken);

        return Success(
            operation,
            correlationId,
            new XElement("Account",
                new XElement("AccountNumber", accountNumber),
                new XElement("Status", "INACTIVE")));
    }

    private static string Success(
        string operation,
        string correlationId,
        XElement body)
    {
        return new XDocument(
            new XElement("Response",
                new XElement("Header",
                    new XElement("Version", "1.0"),
                    new XElement("Operation", operation),
                    new XElement("CorrelationId", correlationId),
                    new XElement("Status", "SUCCESS")),
                new XElement("Body", body)))
            .ToString();
    }

    private static string Error(
        string operation,
        string correlationId,
        string code)
    {
        return new XDocument(
            new XElement("Response",
                new XElement("Header",
                    new XElement("Version", "1.0"),
                    new XElement("Operation", operation),
                    new XElement("CorrelationId", correlationId),
                    new XElement("Status", "ERROR")),
                new XElement("Body",
                    new XElement("Error",
                        new XElement("Code", code)))))
            .ToString();
    }
}