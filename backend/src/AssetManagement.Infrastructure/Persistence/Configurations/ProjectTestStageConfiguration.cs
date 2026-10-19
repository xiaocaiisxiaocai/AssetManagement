using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class ProjectTestStageConfiguration : IEntityTypeConfiguration<ProjectTestStage>
{
    public void Configure(EntityTypeBuilder<ProjectTestStage> b)
    {
        b.ToTable("project_test_stages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Objective).HasMaxLength(2000);
        b.Property(x => x.TestContent).HasMaxLength(8000).HasColumnType("text");
        b.Property(x => x.TestResult).HasMaxLength(8000).HasColumnType("text");
        b.Property(x => x.Conclusion).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.StartDate).HasColumnType("date");
        b.Property(x => x.EndDate).HasColumnType("date");
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.Property<int?>("ActiveSequence")
            .HasComputedColumnSql("IF(`IsDeleted` = 0, `Sequence`, NULL)", stored: true);
        b.HasIndex("ProjectId", "ActiveSequence").IsUnique();
        b.Property<int?>("CurrentProjectKey")
            .HasComputedColumnSql("IF(`IsDeleted` = 0 AND `Status` IN ('in_progress', 'blocked'), `ProjectId`, NULL)", stored: true);
        b.HasIndex("CurrentProjectKey").IsUnique();
        b.HasOne<TestProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
