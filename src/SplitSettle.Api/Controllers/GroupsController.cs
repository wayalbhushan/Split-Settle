namespace SplitSettle.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Services;

[ApiController]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupsController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _groupService.GetAllGroupsAsync(cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GroupDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var group = await _groupService.GetGroupByIdAsync(id, cancellationToken);
        return Ok(group);
    }

    [HttpPost]
    public async Task<ActionResult<GroupResponse>> Create(
        [FromBody] CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _groupService.CreateGroupAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
    }
}
