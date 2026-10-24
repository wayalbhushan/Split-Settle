using SplitSettle.Core.Entities;
using SplitSettle.Core.Services;
using Xunit;

namespace SplitSettle.Tests;

public class BalanceCalculatorTests
{
    [Fact]
    public void Compute_PayerNotPartOfSplit_CalculatesCorrectBalances()
    {
        // Member 1 pays 60.00, but only members 2 and 3 split it (30.00 each)
        var memberIds = new List<int> { 1, 2, 3 };

        var expense = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "Gift for 2 and 3",
            Amount = 60.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 2, ShareAmount = 30.00m },
                new() { ExpenseId = 1, MemberId = 3, ShareAmount = 30.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { expense });

        Assert.Equal(60.00m, balances[1]);
        Assert.Equal(-30.00m, balances[2]);
        Assert.Equal(-30.00m, balances[3]);
        Assert.Equal(0.00m, balances.Values.Sum());
    }

    [Fact]
    public void Compute_SubsetSplit_OnlyIncludesChosenMembersInDebts()
    {
        // Group has members 1, 2, 3. Member 1 pays 100 for members 1 and 2 (50 each). Member 3 is untouched.
        var memberIds = new List<int> { 1, 2, 3 };

        var expense = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "Lunch for 1 and 2",
            Amount = 100.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 1, ShareAmount = 50.00m },
                new() { ExpenseId = 1, MemberId = 2, ShareAmount = 50.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { expense });

        Assert.Equal(50.00m, balances[1]);
        Assert.Equal(-50.00m, balances[2]);
        Assert.Equal(0.00m, balances[3]);
        Assert.Equal(0.00m, balances.Values.Sum());
    }

    [Fact]
    public void Compute_SeveralExpenses_AggregatesPaidAndOwedCorrectly()
    {
        var memberIds = new List<int> { 1, 2, 3 };

        var e1 = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "Hotel",
            Amount = 120.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 1, ShareAmount = 40.00m },
                new() { ExpenseId = 1, MemberId = 2, ShareAmount = 40.00m },
                new() { ExpenseId = 1, MemberId = 3, ShareAmount = 40.00m }
            }
        };

        var e2 = new Expense
        {
            Id = 2,
            GroupId = 10,
            PaidByMemberId = 2,
            Description = "Cab",
            Amount = 60.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 2, MemberId = 1, ShareAmount = 20.00m },
                new() { ExpenseId = 2, MemberId = 2, ShareAmount = 20.00m },
                new() { ExpenseId = 2, MemberId = 3, ShareAmount = 20.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { e1, e2 });

        // Member 1: +120 - 40 - 20 = +60
        // Member 2: +60 - 40 - 20 = 0
        // Member 3: 0 - 40 - 20 = -60
        Assert.Equal(60.00m, balances[1]);
        Assert.Equal(0.00m, balances[2]);
        Assert.Equal(-60.00m, balances[3]);
        Assert.Equal(0.00m, balances.Values.Sum());
    }

    [Fact]
    public void Compute_MemberWithNoExpenses_HasZeroBalance()
    {
        var memberIds = new List<int> { 1, 2, 99 };

        var expense = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "Coffee",
            Amount = 20.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 1, ShareAmount = 10.00m },
                new() { ExpenseId = 1, MemberId = 2, ShareAmount = 10.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { expense });

        Assert.Equal(0.00m, balances[99]);
        Assert.Equal(0.00m, balances.Values.Sum());
    }

    [Fact]
    public void Compute_AllBalances_AlwaysSumToZero()
    {
        var memberIds = new List<int> { 1, 2, 3, 4, 5 };
        var expenses = new List<Expense>();

        // Create 5 varied expenses
        for (int i = 1; i <= 5; i++)
        {
            decimal amt = i * 25.50m;
            expenses.Add(new Expense
            {
                Id = i,
                GroupId = 1,
                PaidByMemberId = i,
                Description = $"Exp {i}",
                Amount = amt,
                Splits = memberIds.Select(m => new ExpenseSplit
                {
                    ExpenseId = i,
                    MemberId = m,
                    ShareAmount = amt / 5.0m
                }).ToList()
            });
        }

        var balances = BalanceCalculator.Compute(memberIds, expenses);
        Assert.Equal(0.00m, balances.Values.Sum());
    }
}
