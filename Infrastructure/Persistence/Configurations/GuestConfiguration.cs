using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.HasIndex(g => g.PhoneE164);
        builder.HasIndex(g => g.Email);
        builder.HasIndex(g => g.NameSearch);
    }
}
