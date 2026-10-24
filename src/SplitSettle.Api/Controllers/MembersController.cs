namespace SplitSettle.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Services;

[ApiController]
[Route("api/groups/{groupId:int}/members")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> GetMembers(
        int groupId,
        CancellationToken cancellationToken)
    {
        var members = await _memberService.GetMembersByGroupIdAsync(groupId, cancellationToken);
        return Ok(members);
    }

    [HttpPost]
    public async Task<ActionResult<MemberResponse>> CreateMember(
        int groupId,
        [FromBody] CreateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _memberService.CreateMemberAsync(groupId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMemberDetail), new { groupId, memberId = member.Id }, member);
    }

    [HttpGet("{memberId:int}")]
    public async Task<ActionResult<MemberDetailResponse>> GetMemberDetail(
        int groupId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var detail = await _memberService.GetMemberDetailAsync(groupId, memberId, cancellationToken);
        return Ok(detail);
    }
}
