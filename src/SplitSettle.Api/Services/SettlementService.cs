namespace SplitSettle.Api.Services;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Services;
using SplitSettle.Data;

public interface ISettlementService
{
    Task<IReadOnlyList<BalanceResponse>> GetBalancesByGroupIdAsync(int groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementResponse>> GetSettlementsByGroupIdAsync(int groupId, CancellationToken cancellationToken = default);
}

public class SettlementService : ISettlementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SettlementService> _logger;

    public SettlementService(AppDbContext context, ILogger<SettlementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BalanceResponse>> GetBalancesByGroupIdAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var (members, balances) = await ComputeBalancesInternalAsync(groupId, cancellationToken);

        return members
            .OrderBy(m => m.Id)
            .Select(m => new BalanceResponse(
                m.Id,
                m.Name,
                balances.TryGetValue(m.Id, out var b) ? b.ToString("0.00", CultureInfo.InvariantCulture) : "0.00"))
            .ToList();
    }

    public async Task<IReadOnlyList<SettlementResponse>> GetSettlementsByGroupIdAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var (members, balances) = await ComputeBalancesInternalAsync(groupId, cancellationToken);
        var memberNameLookup = members.ToDictionary(m => m.Id, m => m.Name);

        var transfers = SettlementCalculator.Settle(balances);

        return transfers
            .Select(t => new SettlementResponse(
                t.FromMemberId,
                memberNameLookup.TryGetValue(t.FromMemberId, out var fromName) ? fromName : string.Empty,
                t.ToMemberId,
                memberNameLookup.TryGetValue(t.ToMemberId, out var toName) ? toName : string.Empty,
                t.Amount.ToString("0.00", CultureInfo.InvariantCulture)))
            .ToList();
    }

    private record MemberSummary(int Id, string Name);

    private async Task<(List<MemberSummary> Members, Dictionary<int, decimal> Balances)> ComputeBalancesInternalAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        var members = await _context.Members
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => new MemberSummary(m.Id, m.Name))
            .ToListAsync(cancellationToken);

        var memberIds = members.Select(m => m.Id).Distinct().ToList();

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Splits)
            .Where(e => e.GroupId == groupId)
            .ToListAsync(cancellationToken);

        var balances = BalanceCalculator.Compute(memberIds, expenses);

        decimal sumOfBalances = balances.Values.Sum();
        if (Math.Abs(sumOfBalances) > 0.001m)
        {
            _logger.LogError("Balance sum precondition violated for group {GroupId}: sum is {Sum}", groupId, sumOfBalances);
            throw new InvalidOperationException($"Balance sum precondition violated: balances sum to {sumOfBalances} instead of zero.");
        }

        return (members, balances);
    }
}
