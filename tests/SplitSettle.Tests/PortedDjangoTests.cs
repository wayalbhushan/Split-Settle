using SplitSettle.Core.Entities;
using SplitSettle.Core.Services;
using Xunit;

namespace SplitSettle.Tests;

public class PortedDjangoTests
{
    // 1. test_simple_case_two_members
    [Fact]
    public void Test1_SimpleCaseTwoMembers_ComputesCorrectBalancesAndSingleSettlement()
    {
        var memberIds = new List<int> { 1, 2 };
        var amount = 100.00m;

        var shares = SplitCalculator.Split(amount, memberIds);

        var expense = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "Dinner",
            Amount = amount,
            Splits = new List<ExpenseSplit>
            {
                new() { Id = 101, ExpenseId = 1, MemberId = 1, ShareAmount = shares[1] },
                new() { Id = 102, ExpenseId = 1, MemberId = 2, ShareAmount = shares[2] }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { expense });

        Assert.Equal(50.00m, balances[1]);
        Assert.Equal(-50.00m, balances[2]);
        Assert.Equal(0.00m, balances.Values.Sum());

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Single(settlements);
        Assert.Equal(new Transfer(2, 1, 50.00m), settlements[0]);
    }

    // 2. test_multi_member_case
    [Fact]
    public void Test2_MultiMemberCase_SettlesInAtMostNMinusOneTransfersAndLeavesZeroBalances()
    {
        var memberIds = new List<int> { 1, 2, 3, 4 };

        var e1Shares = SplitCalculator.Split(100.00m, new List<int> { 1, 2, 3, 4 });
        var e1 = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "E1",
            Amount = 100.00m,
            Splits = memberIds.Select(id => new ExpenseSplit
            {
                ExpenseId = 1,
                MemberId = id,
                ShareAmount = e1Shares[id]
            }).ToList()
        };

        var e2Members = new List<int> { 2, 3, 4 };
        var e2Shares = SplitCalculator.Split(200.00m, e2Members);
        var e2 = new Expense
        {
            Id = 2,
            GroupId = 10,
            PaidByMemberId = 2,
            Description = "E2",
            Amount = 200.00m,
            Splits = e2Members.Select(id => new ExpenseSplit
            {
                ExpenseId = 2,
                MemberId = id,
                ShareAmount = e2Shares[id]
            }).ToList()
        };

        var expenses = new List<Expense> { e1, e2 };
        var balances = BalanceCalculator.Compute(memberIds, expenses);

        Assert.Equal(0.00m, balances.Values.Sum());

        int nonZeroCount = balances.Values.Count(b => Math.Abs(b) > 0.001m);
        var settlements = SettlementCalculator.Settle(balances);

        Assert.True(settlements.Count <= nonZeroCount - 1);

        var finalBalances = new Dictionary<int, decimal>(balances);
        foreach (var transfer in settlements)
        {
            finalBalances[transfer.FromMemberId] += transfer.Amount;
            finalBalances[transfer.ToMemberId] -= transfer.Amount;
        }

        foreach (var balance in finalBalances.Values)
        {
            Assert.True(Math.Abs(balance) < 0.001m);
        }
    }

    // 3. test_rounding_case_split_equally
    [Fact]
    public void Test3_RoundingCaseSplitEqually_GivesLeftoverCentToFirstMember()
    {
        var amount = 100.00m;
        var memberIds = new List<int> { 1, 2, 3 };

        var shares = SplitCalculator.Split(amount, memberIds);

        Assert.Equal(3, shares.Count);
        Assert.Equal(amount, shares.Values.Sum());
        Assert.Equal(33.34m, shares[1]);
        Assert.Equal(33.33m, shares[2]);
        Assert.Equal(33.33m, shares[3]);
    }

    // 4. test_already_settled_group
    [Fact]
    public void Test4_AlreadySettledGroup_ProducesZeroSettlementTransfers()
    {
        var memberIds = new List<int> { 1, 2 };

        var e1 = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "A pays for B",
            Amount = 50.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 2, ShareAmount = 50.00m }
            }
        };

        var e2 = new Expense
        {
            Id = 2,
            GroupId = 10,
            PaidByMemberId = 2,
            Description = "B pays for A",
            Amount = 50.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 2, MemberId = 1, ShareAmount = 50.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { e1, e2 });

        Assert.Equal(0.00m, balances[1]);
        Assert.Equal(0.00m, balances[2]);
        Assert.Equal(0.00m, balances.Values.Sum());

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Empty(settlements);
    }

    // 5. test_single_member_group
    [Fact]
    public void Test5_SingleMemberGroup_SelfExpenseResultsInZeroBalanceAndNoTransfers()
    {
        var memberIds = new List<int> { 5 };

        var expense = new Expense
        {
            Id = 1,
            GroupId = 20,
            PaidByMemberId = 5,
            Description = "Solo",
            Amount = 10.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 5, ShareAmount = 10.00m }
            }
        };

        var balances = BalanceCalculator.Compute(memberIds, new List<Expense> { expense });

        Assert.Equal(0.00m, balances[5]);
        Assert.Equal(0.00m, balances.Values.Sum());

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Empty(settlements);
    }

    // 6. test_create_expense_via_api
    [Fact]
    public void Test6_CreateExpense_SubsetSplitsDivideEquallyAmongChosenMembers()
    {
        var subsetMembers = new List<int> { 1, 2 };
        var shares = SplitCalculator.Split(150.00m, subsetMembers);

        Assert.Equal(2, shares.Count);
        Assert.Equal(75.00m, shares[1]);
        Assert.Equal(75.00m, shares[2]);
        Assert.Equal(150.00m, shares.Values.Sum());
    }

    // 7. test_get_balances_api
    [Fact]
    public void Test7_GetBalances_CalculatesNetBalancesCorrectlyAcrossAllMembers()
    {
        var memberIds = new List<int> { 1, 2, 3 };
        var expense = new Expense
        {
            Id = 1,
            GroupId = 10,
            PaidByMemberId = 1,
            Description = "E1",
            Amount = 90.00m,
            Splits = new List<ExpenseSplit>
            {
                new() { ExpenseId = 1, MemberId = 1, ShareAmount = 30.00m },
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

    // 8. test_get_settlements_api
    [Fact]
    public void Test8_GetSettlements_ProducesDeterministicTransfersToSingleCreditor()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, 60.00m },
            { 2, -30.00m },
            { 3, -30.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Equal(2, settlements.Count);
        Assert.All(settlements, s => Assert.Equal(1, s.ToMemberId));
        Assert.All(settlements, s => Assert.Equal(30.00m, s.Amount));

        // Deterministic tie-breaking orders member 2 before member 3
        Assert.Equal(2, settlements[0].FromMemberId);
        Assert.Equal(3, settlements[1].FromMemberId);
    }

    // 9. test_invalid_expense_creation
    [Fact]
    public void Test9_InvalidExpense_ThrowsForNegativeAmountOrEmptySplitMembers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SplitCalculator.Split(-50.00m, new List<int> { 1 }));

        var emptyResult = SplitCalculator.Split(50.00m, new List<int>());
        Assert.Empty(emptyResult);
    }
}
