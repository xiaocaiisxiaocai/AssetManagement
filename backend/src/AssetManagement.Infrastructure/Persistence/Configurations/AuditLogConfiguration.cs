using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(100);
        b.Property(x => x.TargetId).HasMaxLength(100);
        b.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        b.Property(x => x.Detail);
        b.Property(x => x.Ip).HasMaxLength(100);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.OccurredAt);
    }
}
