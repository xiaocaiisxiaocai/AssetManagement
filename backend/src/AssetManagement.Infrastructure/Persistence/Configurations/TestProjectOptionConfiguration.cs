using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class TestProjectOptionConfiguration : IEntityTypeConfiguration<TestProjectOption>
{
    public void Configure(EntityTypeBuilder<TestProjectOption> b)
    {
        b.ToTable("test_project_options");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(50).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Label).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.Kind, x.Code }).IsUnique();
    }
}
