using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitSettle.Core.Entities;

namespace SplitSettle.Data.Configurations;

public class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShareAmount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.HasOne(s => s.Expense)
            .WithMany(e => e.Splits)
            .HasForeignKey(s => s.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Member)
            .WithMany()
            .HasForeignKey(s => s.MemberId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
