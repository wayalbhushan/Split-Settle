namespace SplitSettle.Api.DTOs;

public record CreateExpenseRequest(
    string? Description,
    object? Amount,
    int? PaidBy,
    IReadOnlyList<int>? SplitAmong);

public record UpdateExpenseRequest(
    string? Description,
    object? Amount,
    int? PaidBy,
    IReadOnlyList<int>? SplitAmong);

public record ExpenseResponse(
    int Id,
    int Group,
    string Description,
    string Amount,
    int PaidBy,
    string PaidByName,
    IReadOnlyList<int> SplitAmong,
    DateTime CreatedAt);
