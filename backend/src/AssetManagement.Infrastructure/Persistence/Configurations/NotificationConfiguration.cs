using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(30).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(500).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        // MySQL/InnoDB may bind the UserId foreign key to this physical index.
        // Keep it even though the wider query index has the same left prefix.
        b.HasIndex(x => new { x.UserId, x.IsRead });
        b.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id });
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt, x.Id });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
