using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class FlowRecordConfiguration : IEntityTypeConfiguration<FlowRecord>
{
    public void Configure(EntityTypeBuilder<FlowRecord> b)
    {
        b.ToTable("flow_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.Operator).HasMaxLength(100);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => x.FlowId);
        b.HasIndex(x => x.OperatedAt);
    }
}
