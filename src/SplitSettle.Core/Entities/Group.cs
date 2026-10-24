namespace SplitSettle.Core.Entities;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Member> Members { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
}
