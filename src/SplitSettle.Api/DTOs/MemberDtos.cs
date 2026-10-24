namespace SplitSettle.Api.DTOs;

public record CreateMemberRequest(string? Name);

public record MemberResponse(
    int Id,
    string Name,
    int Group);

public record MemberDetailResponse(
    int MemberId,
    string MemberName,
    string Balance,
    IReadOnlyList<PaidExpenseDto> PaidExpenses,
    IReadOnlyList<OwedExpenseDto> OwedExpenses);

public record PaidExpenseDto(
    int Id,
    int Group,
    string Description,
    string Amount,
    int PaidBy,
    string PaidByName,
    IReadOnlyList<int> SplitAmong,
    DateTime CreatedAt);

public record OwedExpenseDto(
    int ExpenseId,
    string Description,
    string ShareAmount,
    string TotalAmount,
    DateTime Date);
