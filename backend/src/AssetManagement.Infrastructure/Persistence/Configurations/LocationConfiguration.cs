using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("locations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.QrCode).HasMaxLength(200);
        b.HasIndex(x => x.ParentId);
    }
}
