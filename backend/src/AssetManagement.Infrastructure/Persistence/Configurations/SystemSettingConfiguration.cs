using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("system_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
