using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmployeeNo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.TokenVersion).IsRequired();
        b.HasIndex(x => x.EmployeeNo).IsUnique();
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
    }
}
