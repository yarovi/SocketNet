using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlTcpAccounts.Domain;
using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration
    : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.PersonId)
            .HasColumnName("person_id")
            .IsRequired();

        builder.Property(a => a.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(a => a.AccountNumber)
            .IsUnique();

        builder.Property(a => a.Currency)
            .HasColumnName("currency")
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasColumnName("balance")
            .HasPrecision(18, 2);

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<
                    AccountStatus>(
                    value,
                    ignoreCase: true))
            .HasMaxLength(10)
            .IsRequired();
    }
}