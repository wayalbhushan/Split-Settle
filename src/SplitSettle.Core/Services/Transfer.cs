namespace SplitSettle.Core.Services;

public record Transfer(int FromMemberId, int ToMemberId, decimal Amount);
