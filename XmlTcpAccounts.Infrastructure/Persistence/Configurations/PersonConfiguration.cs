using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XmlTcpAccounts.Domain.Entities;

namespace XmlTcpAccounts.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration
    : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(8)
            .IsRequired();

        builder.HasIndex(p => p.DocumentNumber)
            .IsUnique();

        builder.Property(p => p.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(150)
            .IsRequired();

        //builder.Ignore("CreatedAt");
    }
}