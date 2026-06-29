using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class TestProjectFollowupConfiguration : IEntityTypeConfiguration<TestProjectFollowup>
{
    public void Configure(EntityTypeBuilder<TestProjectFollowup> b)
    {
        b.ToTable("test_project_followups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.DueDate);
    }
}
