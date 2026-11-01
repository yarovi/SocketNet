using Microsoft.EntityFrameworkCore;
using XmlTcpAccounts.Application.Ports;
using XmlTcpAccounts.Application.UseCases;
using XmlTcpAccounts.Infrastructure.Persistence;
using XmlTcpAccounts.Infrastructure.Repositories;
using XmlTcpAccounts.Server;
using XmlTcpAccounts.Server.Protocol;
using XmlTcpAccounts.Server.Workes;

/*
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
*/


var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("AccountsDatabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "AccountsDatabase connection string is required.");
}

// Infrastructure: PostgreSQL + Entity Framework Core
builder.Services.AddDbContext<AccountsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Ports and adapters
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();

// Application use cases
builder.Services.AddScoped<ConsultAccountUseCase>();
builder.Services.AddScoped<DeactivateAccountUseCase>();

builder.Services.AddScoped<XmlRequestDispatcher>();
// TCP server lifecycle
builder.Services.AddHostedService<TcpServerWorker>();

var host = builder.Build();

await host.RunAsync();