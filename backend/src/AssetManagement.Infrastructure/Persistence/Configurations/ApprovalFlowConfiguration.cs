using System.Text.Json;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class ApprovalFlowConfiguration : IEntityTypeConfiguration<ApprovalFlow>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<ApprovalFlow> b)
    {
        b.ToTable("approval_flows");
        b.HasKey(x => x.Id);
        b.Property(x => x.FlowNo).HasMaxLength(50).IsRequired();
        b.Property(x => x.BizType).HasMaxLength(50).IsRequired();
        b.Property(x => x.AssetNo).HasMaxLength(100).IsRequired();
        b.Property(x => x.AssetName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Applicant).HasMaxLength(100).IsRequired();
        b.Property(x => x.ApplicantDept).HasMaxLength(100);
        b.Property(x => x.Transferee).HasMaxLength(100);
        b.Property(x => x.TransfereeDept).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ReturnDate).HasMaxLength(50);
        b.Property(x => x.Status).HasMaxLength(50).IsRequired();
        b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.FlowNo).IsUnique();
        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.ApplicantId);
        b.HasIndex(x => x.Status);
        b.Property(x => x.Nodes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<FlowInstanceNode>>(v, JsonOptions) ?? new())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<List<FlowInstanceNode>>(
                (l, r) => JsonSerializer.Serialize(l, JsonOptions) == JsonSerializer.Serialize(r, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<List<FlowInstanceNode>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions) ?? new()));
    }
}
