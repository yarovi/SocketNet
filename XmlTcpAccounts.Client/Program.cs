using System.Net.Sockets;
using System.Xml.Linq;
using XmlTcpAccounts.Client.Transport;


public static class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 9000;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("XML TCP ACCOUNTS CLIENT");

        while (true)
        {
            ShowMenu();

            var option = Console.ReadLine()?.Trim();

            if (option == "0")
            {
                Console.WriteLine("Client finished.");
                return;
            }

            if (option is not ("1" or "2" or "3" or "4" or "5"))
            {
                Console.WriteLine("Invalid option.");
                continue;
            }

            var requestXml = BuildRequest(option);

            if (requestXml is null)
                continue;

            await SendRequestAsync(requestXml);
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("       XML TCP ACCOUNTS CLIENT");
        Console.WriteLine("========================================");
        Console.WriteLine("1. Consult all accounts by DNI");
        Console.WriteLine("2. Consult one account");
        Console.WriteLine("3. Deactivate account");
        Console.WriteLine("4. Send unknown operation");
        Console.WriteLine("5. Send invalid XML");
        Console.WriteLine("0. Exit");
        Console.Write("Select an option: ");
    }

    private static string? BuildRequest(string option)
    {
        if (option == "5")
            return "<Request><Header></Request>";

        string operation = option switch
        {
            "3" => "DEACTIVATE_ACCOUNT",
            "4" => "UNKNOWN_OPERATION",
            _ => "CONSULT_ACCOUNT"
        };

        string documentNumber = "";
        string? accountNumber = null;

        if (option is "1" or "2" or "3")
        {
            Console.Write("Enter DNI: ");
            documentNumber = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                Console.WriteLine("DNI is required.");
                return null;
            }
        }

        if (option is "2" or "3")
        {
            Console.Write("Enter account number: ");
            accountNumber = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                Console.WriteLine("Account number is required.");
                return null;
            }
        }

        var body = new XElement(
            "Body",
            new XElement("DocumentNumber", documentNumber));

        if (option == "1")
        {
            body.Add(new XElement("All", "true"));
        }

        if (accountNumber is not null)
        {
            body.Add(new XElement(
                "AccountNumber",
                accountNumber));
        }

        var request = new XDocument(
            new XElement(
                "Request",
                new XElement(
                    "Header",
                    new XElement("Version", "1.0"),
                    new XElement("Operation", operation),
                    new XElement(
                        "CorrelationId",
                        Guid.NewGuid().ToString("N"))),
                body));

        return request.ToString();
    }

    private static async Task SendRequestAsync(string requestXml)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(15));

        try
        {
            using var client = new TcpClient();

            Console.WriteLine();
            Console.WriteLine($"Connecting to {Host}:{Port}...");

            await client.ConnectAsync(
                Host,
                Port,
                timeout.Token);

            Console.WriteLine("Connected successfully.");

            using var stream = client.GetStream();

            Console.WriteLine();
            Console.WriteLine("Request XML:");
            Console.WriteLine(requestXml);

            await TcpMessageFraming.WriteAsync(
                stream,
                requestXml,
                timeout.Token);

            var response = await TcpMessageFraming.ReadAsync(
                stream,
                timeout.Token);

            Console.WriteLine();
            Console.WriteLine("Server response:");
            Console.WriteLine(response);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("TCP operation timed out.");
        }
        catch (SocketException exception)
        {
            Console.Error.WriteLine(
                $"TCP connection error: {exception.Message}");
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine(
                $"TCP communication error: {exception.Message}");
        }
    }
}