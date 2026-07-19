using AssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssetManagement.Infrastructure.Persistence.Configurations;

public class BusinessSequenceConfiguration : IEntityTypeConfiguration<BusinessSequence>
{
    public void Configure(EntityTypeBuilder<BusinessSequence> b)
    {
        b.ToTable("business_sequences");
        b.HasKey(x => x.SequenceKey);
        b.Property(x => x.SequenceKey).HasMaxLength(80);
    }
}
