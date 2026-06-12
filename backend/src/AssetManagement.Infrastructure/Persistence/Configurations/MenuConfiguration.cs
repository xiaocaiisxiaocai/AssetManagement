using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("menus");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.Property(x => x.Path).HasMaxLength(200);
        b.Property(x => x.Component).HasMaxLength(200);
        b.Property(x => x.Icon).HasMaxLength(100);
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.PermissionCode).HasMaxLength(100);
        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => x.PermissionCode);
    }
}
