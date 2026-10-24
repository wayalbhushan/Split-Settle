namespace SplitSettle.Api.Services;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Entities;
using SplitSettle.Core.Services;
using SplitSettle.Data;

public interface IMemberService
{
    Task<IReadOnlyList<MemberResponse>> GetMembersByGroupIdAsync(int groupId, CancellationToken cancellationToken = default);
    Task<MemberResponse> CreateMemberAsync(int groupId, CreateMemberRequest request, CancellationToken cancellationToken = default);
    Task<MemberDetailResponse> GetMemberDetailAsync(int groupId, int memberId, CancellationToken cancellationToken = default);
}

public class MemberService : IMemberService
{
    private readonly AppDbContext _context;

    public MemberService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MemberResponse>> GetMembersByGroupIdAsync(int groupId, CancellationToken cancellationToken = default)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        return await _context.Members
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Id)
            .Select(m => new MemberResponse(m.Id, m.Name, groupId))
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberResponse> CreateMemberAsync(int groupId, CreateMemberRequest request, CancellationToken cancellationToken = default)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        string? name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new ValidationException("name", "This field is required.");
        }

        if (name.Length > 255)
        {
            throw new ValidationException("name", "Ensure this field has no more than 255 characters.");
        }

        bool duplicate = await _context.Members
            .AnyAsync(m => m.GroupId == groupId && m.Name.ToLower() == name.ToLower(), cancellationToken);

        if (duplicate)
        {
            throw new ConflictException($"A member with name '{name}' already exists in this group.");
        }

        var member = new Member
        {
            Name = name,
            GroupId = groupId
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        return new MemberResponse(member.Id, member.Name, groupId);
    }

    public async Task<MemberDetailResponse> GetMemberDetailAsync(int groupId, int memberId, CancellationToken cancellationToken = default)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        var member = await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.GroupId == groupId, cancellationToken);

        if (member == null)
        {
            throw new NotFoundException($"Member with id {memberId} not found in group {groupId}.");
        }

        var allGroupMembers = await _context.Members
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var allExpenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.PaidByMember)
            .Include(e => e.Splits)
            .Where(e => e.GroupId == groupId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var balances = BalanceCalculator.Compute(allGroupMembers, allExpenses);
        decimal memberBalance = balances.TryGetValue(memberId, out var b) ? b : 0.00m;

        var paidExpenses = allExpenses
            .Where(e => e.PaidByMemberId == memberId)
            .Select(e => new PaidExpenseDto(
                e.Id,
                e.GroupId,
                e.Description,
                e.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                e.PaidByMemberId,
                e.PaidByMember?.Name ?? string.Empty,
                e.Splits.OrderBy(s => s.MemberId).Select(s => s.MemberId).ToList(),
                e.CreatedAt))
            .ToList();

        var owedExpenses = allExpenses
            .SelectMany(e => e.Splits.Where(s => s.MemberId == memberId).Select(s => new { SplitItem = s, Expense = e }))
            .OrderByDescending(x => x.Expense.CreatedAt)
            .Select(x => new OwedExpenseDto(
                x.Expense.Id,
                x.Expense.Description,
                x.SplitItem.ShareAmount.ToString("0.00", CultureInfo.InvariantCulture),
                x.Expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                x.Expense.CreatedAt))
            .ToList();

        return new MemberDetailResponse(
            member.Id,
            member.Name,
            memberBalance.ToString("0.00", CultureInfo.InvariantCulture),
            paidExpenses,
            owedExpenses);
    }
}
