using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitSettle.Core.Entities;

namespace SplitSettle.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamptz");

        builder.HasOne(e => e.Group)
            .WithMany(g => g.Expenses)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.PaidByMember)
            .WithMany()
            .HasForeignKey(e => e.PaidByMemberId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Splits)
            .WithOne(s => s.Expense)
            .HasForeignKey(s => s.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
