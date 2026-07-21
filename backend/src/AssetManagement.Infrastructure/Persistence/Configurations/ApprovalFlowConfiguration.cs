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
        b.Property(x => x.OriginalReturnDate).HasMaxLength(50);
        b.Property(x => x.ReturnDate).HasMaxLength(50);
        b.Property(x => x.Status).HasMaxLength(50).IsRequired();
        b.Property(x => x.ActiveScopeKey).HasMaxLength(100);
        b.HasIndex(x => x.FlowNo).IsUnique();
        b.HasIndex(x => x.AssetId);
        b.HasIndex(x => x.ApplicantId);
        b.HasIndex(x => x.SourceCustodianId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ActiveScopeKey).IsUnique();
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasOne<AssetManagement.Domain.Entities.Workflow>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SourceCustodianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TransfereeId).OnDelete(DeleteBehavior.Restrict);

        // BPMN 当前活跃节点列表（JSON 序列化）
        b.Property(x => x.CurrentNodeIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (l, r) => JsonSerializer.Serialize(l, JsonOptions) == JsonSerializer.Serialize(r, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions) ?? new()));

        // BPMN Token 状态字典（JSON 序列化）
        b.Property(x => x.BpmnTokens)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, BpmnToken>>(v, JsonOptions) ?? new())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, BpmnToken>>(
                (l, r) => JsonSerializer.Serialize(l, JsonOptions) == JsonSerializer.Serialize(r, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<Dictionary<string, BpmnToken>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions) ?? new()));

        // 条件表达式上下文（JSON 序列化，不映射为导航属性）
        b.Property(x => x.Context)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions))
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>?>(
                (l, r) => JsonSerializer.Serialize(l, JsonOptions) == JsonSerializer.Serialize(r, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)));
    }
}
