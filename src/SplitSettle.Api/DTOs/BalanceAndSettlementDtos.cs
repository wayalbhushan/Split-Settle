namespace SplitSettle.Api.DTOs;

public record BalanceResponse(
    int MemberId,
    string MemberName,
    string Balance);

public record SettlementResponse(
    int FromMemberId,
    string FromMemberName,
    int ToMemberId,
    string ToMemberName,
    string Amount);
