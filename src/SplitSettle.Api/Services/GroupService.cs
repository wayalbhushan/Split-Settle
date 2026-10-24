namespace SplitSettle.Api.Services;

using Microsoft.EntityFrameworkCore;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Entities;
using SplitSettle.Data;

public interface IGroupService
{
    Task<IReadOnlyList<GroupResponse>> GetAllGroupsAsync(CancellationToken cancellationToken = default);
    Task<GroupDetailResponse> GetGroupByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GroupResponse> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default);
}

public class GroupService : IGroupService
{
    private readonly AppDbContext _context;

    public GroupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GroupResponse>> GetAllGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GroupResponse(
                g.Id,
                g.Name,
                g.CreatedAt,
                g.Members.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<GroupDetailResponse> GetGroupByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await _context.Groups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (group == null)
        {
            throw new NotFoundException($"Group with id {id} not found.");
        }

        var members = group.Members
            .OrderBy(m => m.Id)
            .Select(m => new MemberResponse(m.Id, m.Name, group.Id))
            .ToList();

        return new GroupDetailResponse(
            group.Id,
            group.Name,
            group.CreatedAt,
            members.Count,
            members);
    }

    public async Task<GroupResponse> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        string? name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new ValidationException("name", "This field is required.");
        }

        if (name.Length > 255)
        {
            throw new ValidationException("name", "Ensure this field has no more than 255 characters.");
        }

        var group = new Group
        {
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        return new GroupResponse(group.Id, group.Name, group.CreatedAt, 0);
    }
}
