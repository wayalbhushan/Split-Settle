namespace SplitSettle.Core.Entities;

public class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
}
