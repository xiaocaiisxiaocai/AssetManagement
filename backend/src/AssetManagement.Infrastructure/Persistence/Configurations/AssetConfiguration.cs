using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("assets");
        b.HasKey(x => x.Id);
        b.Property(x => x.AssetNo).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.Brand).HasMaxLength(100);
        b.Property(x => x.ImageUrls).HasMaxLength(2000);
        b.HasIndex(x => x.AssetNo).IsUnique();
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.IsDeleted);
        b.HasIndex(x => x.Status);
    }
}
