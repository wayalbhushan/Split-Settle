namespace SplitSettle.Api.DTOs;

public record CreateGroupRequest(string? Name);

public record GroupResponse(
    int Id,
    string Name,
    DateTime CreatedAt,
    int MemberCount);

public record GroupDetailResponse(
    int Id,
    string Name,
    DateTime CreatedAt,
    int MemberCount,
    IReadOnlyList<MemberResponse> Members);
