namespace SplitSettle.Core.Entities;

public class Expense
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public int PaidByMemberId { get; set; }
    public Member PaidByMember { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ExpenseSplit> Splits { get; set; } = new();
}
