using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> b)
    {
        b.ToTable("asset_categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.CodeSeg).HasMaxLength(50).IsRequired();
        b.Property(x => x.Code).HasMaxLength(200).IsRequired();
        b.Property(x => x.Remark).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.IsDeleted);
        b.HasIndex(x => x.ParentId);
        b.Ignore(x => x.Children);
    }
}
