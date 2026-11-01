using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using XmlTcpAccounts.Infrastructure.Persistence;
using XmlTcpAccounts.Server.Protocol;
using XmlTcpAccounts.Server.Transport;

namespace XmlTcpAccounts.Server.Workes;


/*
public sealed class TcpServerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TcpServerWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "XmlTcpAccounts Server starting...");

        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AccountsDbContext>();

        try
        {
            var canConnect = await dbContext.Database
                .CanConnectAsync(stoppingToken);

            if (!canConnect)
            {
                throw new InvalidOperationException(
                    "Cannot connect to PostgreSQL.");
            }

            logger.LogInformation(
                "PostgreSQL connection established successfully.");

            var personCount = await dbContext.Persons
                .AsNoTracking()
                .CountAsync(stoppingToken);

            var accountCount = await dbContext.Accounts
                .AsNoTracking()
                .CountAsync(stoppingToken);

            logger.LogInformation(
                "Database ready. Persons: {Persons}, Accounts: {Accounts}",
                personCount,
                accountCount);
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                exception,
                "Database initialization verification failed.");

            throw;
        }

        // Temporary: keep the worker running until shutdown.
        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            stoppingToken);
    }
}

*/

public sealed class TcpServerWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TcpServerWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var listener = new TcpListener(
            IPAddress.Loopback,
            9000);

        listener.Start();

        logger.LogInformation(
            "TCP server listening on 127.0.0.1:9000");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(
                    stoppingToken);

                _ = HandleClientSafelyAsync(
                    client,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("TCP server stopping.");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientSafelyAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await TcpMessageFraming.ReadAsync(
                        stream,
                        cancellationToken);

                    if (message is null)
                        break;

                    logger.LogInformation(
                        "TCP message received. Length: {Length}",
                        message.Length);

                    string response;

                    try
                    {
                        await using var scope =
                            scopeFactory.CreateAsyncScope();

                        var dispatcher = scope.ServiceProvider
                            .GetRequiredService<XmlRequestDispatcher>();

                        response = await dispatcher.DispatchAsync(
                            message,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Unexpected error processing XML request.");

                        response = """
                            <Response>
                              <Header>
                                <Version>1.0</Version>
                                <Status>ERROR</Status>
                              </Header>
                              <Body>
                                <Error>
                                  <Code>INTERNAL_ERROR</Code>
                                </Error>
                              </Body>
                            </Response>
                            """;
                    }

                    await TcpMessageFraming.WriteAsync(
                        stream,
                        response,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "TCP client processing cancelled.");
            }
            catch (IOException exception)
            {
                logger.LogWarning(
                    exception,
                    "TCP connection closed unexpectedly.");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Error processing TCP connection.");
            }
        }
    }
}