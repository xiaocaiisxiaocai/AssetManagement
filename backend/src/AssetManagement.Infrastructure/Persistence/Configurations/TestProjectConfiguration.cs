using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class TestProjectConfiguration : IEntityTypeConfiguration<TestProject>
{
    public void Configure(EntityTypeBuilder<TestProject> b)
    {
        b.ToTable("test_projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.IsDeleted);
    }
}
