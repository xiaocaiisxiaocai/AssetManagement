using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

using WorkflowEntity = AssetManagement.Domain.Entities.Workflow;

public class WorkflowConfiguration : IEntityTypeConfiguration<WorkflowEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowEntity> b)
    {
        b.ToTable("workflows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.BizType).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.BizType).IsUnique();

        // BPMN XML 存储
        b.Property(x => x.BpmnXml).HasColumnType("TEXT");
    }
}
