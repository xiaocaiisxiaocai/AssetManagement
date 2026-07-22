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
        b.Property(x => x.LocationName).HasMaxLength(100);
        b.Property(x => x.ImageUrls).HasMaxLength(2000);
        b.Property(x => x.Remark).HasMaxLength(500);
        b.HasIndex(x => x.MaterialNo).IsUnique();
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.IsDeleted);
        b.HasIndex(x => x.Status);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.Property<string>("ActiveNameKey")
            .HasMaxLength(191)
            .HasComputedColumnSql("IF(`IsDeleted` = 0, CONCAT(`ProjectId`, ':', `Name`), NULL)", stored: true);
        b.HasIndex("ActiveNameKey").IsUnique();
        b.HasOne<TestProject>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CustodianId).OnDelete(DeleteBehavior.Restrict);
    }
}
