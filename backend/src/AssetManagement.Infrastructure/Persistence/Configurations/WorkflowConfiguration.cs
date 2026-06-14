using System.Text.Json;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

public class WorkflowConfiguration : IEntityTypeConfiguration<WorkflowEntity>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<WorkflowEntity> b)
    {
        b.ToTable("workflows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.BizType).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.BizType).IsUnique();
        b.Property(x => x.Nodes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<WorkflowNode>>(v, JsonOptions) ?? new())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<List<WorkflowNode>>(
                (l, r) => JsonSerializer.Serialize(l, JsonOptions) == JsonSerializer.Serialize(r, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<List<WorkflowNode>>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions) ?? new()));
    }
}
