using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class TestMaterialConfiguration : IEntityTypeConfiguration<TestMaterial>
{
    public void Configure(EntityTypeBuilder<TestMaterial> b)
    {
        b.ToTable("test_materials");
        b.HasKey(x => x.Id);
        b.Property(x => x.MaterialNo).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.VendorName).HasMaxLength(100);
        b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.Brand).HasMaxLength(100);
        b.Property(x => x.ImageUrls).HasMaxLength(2000);
        b.Property(x => x.Remark).HasMaxLength(500);
        b.HasIndex(x => x.MaterialNo).IsUnique();
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.IsDeleted);
        b.HasIndex(x => x.Status);
    }
}
