namespace SplitSettle.Api.DTOs;

public record ParseExpenseRequest(string? Sentence);

public record ParsedExpenseDraft(
    string Description,
    string Amount,
    int PaidBy,
    string PaidByName,
    IReadOnlyList<int> SplitAmong,
    IReadOnlyList<string> SplitAmongNames);

public record LlmExpenseExtraction(
    string? Description,
    object? Amount,
    string? PaidBy,
    IReadOnlyList<string>? SplitAmong);
