using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class MaterialFlowRecordConfiguration : IEntityTypeConfiguration<MaterialFlowRecord>
{
    public void Configure(EntityTypeBuilder<MaterialFlowRecord> b)
    {
        b.ToTable("material_flow_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.NodeId).HasMaxLength(100);
        b.Property(x => x.Operator).HasMaxLength(100);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => x.FlowId);
        b.HasIndex(x => new { x.OperatorUserId, x.Action, x.OperatedAt });
        b.HasOne<MaterialFlow>().WithMany().HasForeignKey(x => x.FlowId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
