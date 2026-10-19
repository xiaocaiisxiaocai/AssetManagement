using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class ProjectReportDocumentConfiguration : IEntityTypeConfiguration<ProjectReportDocument>
{
    public void Configure(EntityTypeBuilder<ProjectReportDocument> b)
    {
        b.ToTable("project_report_documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.StoredName).HasMaxLength(50).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.UploadedBy).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.ProjectId, x.IsDeleted });
        b.HasIndex(x => x.StoredName).IsUnique();
        b.HasOne<TestProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectTestStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}
