using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExpenseNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RejectionComment).HasMaxLength(1000);

        builder.HasIndex(e => e.ExpenseNumber).IsUnique();
        builder.HasIndex(e => e.EmployeeId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.SubmittedAt);
        builder.HasIndex(e => e.AttachmentId).IsUnique();

        builder.HasOne(e => e.Employee)
            .WithMany(emp => emp.Expenses)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Attachment)
            .WithOne(a => a.Expense)
            .HasForeignKey<Expense>(e => e.AttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
