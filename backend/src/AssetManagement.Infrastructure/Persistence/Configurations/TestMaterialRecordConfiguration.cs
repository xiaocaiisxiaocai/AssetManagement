using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class TestMaterialRecordConfiguration : IEntityTypeConfiguration<TestMaterialRecord>
{
    public void Configure(EntityTypeBuilder<TestMaterialRecord> b)
    {
        b.ToTable("test_material_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.Operator).HasMaxLength(100);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => new { x.MaterialId, x.OperatedAt });
        b.HasIndex(x => x.OperatorUserId);
        b.HasOne<TestMaterial>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
