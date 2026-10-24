namespace SplitSettle.Core.Entities;

public class ExpenseSplit
{
    public int Id { get; set; }

    public int ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public decimal ShareAmount { get; set; }
}
