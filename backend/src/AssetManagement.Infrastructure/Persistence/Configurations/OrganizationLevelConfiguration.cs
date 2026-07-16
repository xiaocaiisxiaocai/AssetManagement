using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class OrganizationLevelConfiguration : IEntityTypeConfiguration<OrganizationLevel>
{
    public void Configure(EntityTypeBuilder<OrganizationLevel> b)
    {
        b.ToTable("organization_levels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Sort);
    }
}
