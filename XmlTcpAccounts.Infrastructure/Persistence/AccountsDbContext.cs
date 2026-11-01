using Microsoft.EntityFrameworkCore;
using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Infrastructure.Persistence;

public class AccountsDbContext
(
    DbContextOptions<AccountsDbContext> options)
    : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AccountsDbContext).Assembly);
    }
}