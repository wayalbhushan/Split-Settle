namespace SplitSettle.Api.Services;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Errors;
using SplitSettle.Core.Entities;
using SplitSettle.Core.Services;
using SplitSettle.Data;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseResponse>> GetExpensesByGroupIdAsync(int groupId, CancellationToken cancellationToken = default);
    Task<ExpenseResponse> CreateExpenseAsync(int groupId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseResponse> UpdateExpenseAsync(int groupId, int expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task DeleteExpenseAsync(int groupId, int expenseId, CancellationToken cancellationToken = default);
}

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _context;

    public ExpenseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExpenseResponse>> GetExpensesByGroupIdAsync(int groupId, CancellationToken cancellationToken = default)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        return await _context.Expenses
            .AsNoTracking()
            .Include(e => e.PaidByMember)
            .Include(e => e.Splits)
            .Where(e => e.GroupId == groupId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExpenseResponse(
                e.Id,
                e.GroupId,
                e.Description,
                e.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                e.PaidByMemberId,
                e.PaidByMember.Name,
                e.Splits.OrderBy(s => s.MemberId).Select(s => s.MemberId).ToList(),
                e.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseResponse> CreateExpenseAsync(int groupId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group == null)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        var (description, amountVal, paidById, splitMembers) = ValidateExpenseInputs(
            request.Description,
            request.Amount,
            request.PaidBy,
            request.SplitAmong,
            group.Members.Select(m => m.Id).ToHashSet());

        var shares = SplitCalculator.Split(amountVal, splitMembers);

        var expense = new Expense
        {
            GroupId = groupId,
            PaidByMemberId = paidById,
            Description = description,
            Amount = amountVal,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (memberId, share) in shares)
        {
            expense.Splits.Add(new ExpenseSplit
            {
                MemberId = memberId,
                ShareAmount = share
            });
        }

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);

        var paidByName = group.Members.First(m => m.Id == paidById).Name;

        return new ExpenseResponse(
            expense.Id,
            expense.GroupId,
            expense.Description,
            expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            expense.PaidByMemberId,
            paidByName,
            expense.Splits.OrderBy(s => s.MemberId).Select(s => s.MemberId).ToList(),
            expense.CreatedAt);
    }

    public async Task<ExpenseResponse> UpdateExpenseAsync(
        int groupId,
        int expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group == null)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        var expense = await _context.Expenses
            .Include(e => e.Splits)
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.GroupId == groupId, cancellationToken);

        if (expense == null)
        {
            throw new NotFoundException($"Expense with id {expenseId} not found in group {groupId}.");
        }

        var (description, amountVal, paidById, splitMembers) = ValidateExpenseInputs(
            request.Description,
            request.Amount,
            request.PaidBy,
            request.SplitAmong,
            group.Members.Select(m => m.Id).ToHashSet());

        var shares = SplitCalculator.Split(amountVal, splitMembers);

        expense.Description = description;
        expense.Amount = amountVal;
        expense.PaidByMemberId = paidById;

        _context.ExpenseSplits.RemoveRange(expense.Splits);
        expense.Splits.Clear();

        foreach (var (memberId, share) in shares)
        {
            expense.Splits.Add(new ExpenseSplit
            {
                ExpenseId = expense.Id,
                MemberId = memberId,
                ShareAmount = share
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var paidByName = group.Members.First(m => m.Id == paidById).Name;

        return new ExpenseResponse(
            expense.Id,
            expense.GroupId,
            expense.Description,
            expense.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            expense.PaidByMemberId,
            paidByName,
            expense.Splits.OrderBy(s => s.MemberId).Select(s => s.MemberId).ToList(),
            expense.CreatedAt);
    }

    public async Task DeleteExpenseAsync(int groupId, int expenseId, CancellationToken cancellationToken = default)
    {
        bool groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            throw new NotFoundException($"Group with id {groupId} not found.");
        }

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.GroupId == groupId, cancellationToken);

        if (expense == null)
        {
            throw new NotFoundException($"Expense with id {expenseId} not found in group {groupId}.");
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (string Description, decimal Amount, int PaidBy, IReadOnlyList<int> SplitMembers) ValidateExpenseInputs(
        string? descriptionRaw,
        object? amountRaw,
        int? paidByRaw,
        IReadOnlyList<int>? splitAmongRaw,
        HashSet<int> groupMemberIds)
    {
        var errors = new Dictionary<string, string[]>();

        string? description = descriptionRaw?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            errors["description"] = new[] { "This field is required." };
        }

        decimal amountVal = 0m;
        if (amountRaw == null)
        {
            errors["amount"] = new[] { "This field is required." };
        }
        else
        {
            string amountStr = amountRaw.ToString()?.Trim() ?? string.Empty;
            if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out amountVal))
            {
                errors["amount"] = new[] { "A valid number is required." };
            }
            else if (amountVal < 0.01m)
            {
                errors["amount"] = new[] { "Ensure this value is greater than or equal to 0.01." };
            }
            else if (decimal.Truncate(amountVal * 100m) != (amountVal * 100m))
            {
                errors["amount"] = new[] { "Amount cannot have more than 2 decimal places." };
            }
        }

        int paidById = 0;
        if (!paidByRaw.HasValue || !groupMemberIds.Contains(paidByRaw.Value))
        {
            errors["paid_by"] = new[] { "Valid group member ID is required." };
        }
        else
        {
            paidById = paidByRaw.Value;
        }

        List<int> splitMembers = new();
        if (splitAmongRaw != null)
        {
            if (splitAmongRaw.Count == 0)
            {
                errors["split_among"] = new[] { "You must select at least one member to split the expense with." };
            }
            else if (splitAmongRaw.Any(id => !groupMemberIds.Contains(id)))
            {
                errors["split_among"] = new[] { "One or more members selected are invalid or not in this group." };
            }
            else
            {
                splitMembers = splitAmongRaw.Distinct().ToList();
            }
        }
        else
        {
            // Default to all group members
            if (groupMemberIds.Count == 0)
            {
                errors["split_among"] = new[] { "You must select at least one member to split the expense with." };
            }
            else
            {
                splitMembers = groupMemberIds.ToList();
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (description!, amountVal, paidById, splitMembers);
    }
}
