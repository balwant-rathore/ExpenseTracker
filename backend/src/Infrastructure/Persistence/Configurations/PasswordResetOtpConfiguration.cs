using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PasswordResetOtpConfiguration : IEntityTypeConfiguration<PasswordResetOtp>
{
    public void Configure(EntityTypeBuilder<PasswordResetOtp> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OtpHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(o => o.UserId);

        builder.HasOne(o => o.User)
            .WithMany(u => u.PasswordResetOtps)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
