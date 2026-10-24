using SplitSettle.Core.Services;
using Xunit;

namespace SplitSettle.Tests;

public class SettlementCalculatorTests
{
    [Fact]
    public void Settle_AllZeroBalances_ReturnsNoTransfers()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, 0.00m },
            { 2, 0.00m },
            { 3, 0.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);
        Assert.Empty(settlements);
    }

    [Fact]
    public void Settle_TwoPeople_ProducesSingleTransfer()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, 50.00m },
            { 2, -50.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Single(settlements);
        Assert.Equal(2, settlements[0].FromMemberId);
        Assert.Equal(1, settlements[0].ToMemberId);
        Assert.Equal(50.00m, settlements[0].Amount);
    }

    [Fact]
    public void Settle_OneCreditorWithManyDebtors_TransfersFlowToSingleCreditor()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, 90.00m },
            { 2, -30.00m },
            { 3, -30.00m },
            { 4, -30.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Equal(3, settlements.Count);
        Assert.All(settlements, s => Assert.Equal(1, s.ToMemberId));
        Assert.Equal(90.00m, settlements.Sum(s => s.Amount));
    }

    [Fact]
    public void Settle_ManyCreditorsWithOneDebtor_TransfersFlowFromSingleDebtor()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, -90.00m },
            { 2, 30.00m },
            { 3, 30.00m },
            { 4, 30.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        Assert.Equal(3, settlements.Count);
        Assert.All(settlements, s => Assert.Equal(1, s.FromMemberId));
        Assert.Equal(90.00m, settlements.Sum(s => s.Amount));
    }

    [Fact]
    public void Settle_AtMostNMinusOneTransfers_VerifiedAcrossMultipleScenarios()
    {
        var scenarios = new List<Dictionary<int, decimal>>
        {
            new() { { 1, 100m }, { 2, -50m }, { 3, -50m } }, // 3 members -> <= 2 transfers
            new() { { 1, 150m }, { 2, -50m }, { 3, -50m }, { 4, -50m } }, // 4 members -> <= 3 transfers
            new() { { 1, 100m }, { 2, 50m }, { 3, -75m }, { 4, -75m } }, // 4 members -> <= 3 transfers
            new() { { 1, 200m }, { 2, 100m }, { 3, -100m }, { 4, -100m }, { 5, -100m } } // 5 members -> <= 4 transfers
        };

        foreach (var balances in scenarios)
        {
            int nonZeroMembers = balances.Values.Count(b => Math.Abs(b) > 0.001m);
            var settlements = SettlementCalculator.Settle(balances);

            Assert.True(settlements.Count <= nonZeroMembers - 1);
        }
    }

    [Fact]
    public void Settle_ApplyingAllTransfers_LeavesEveryMemberAtZeroBalance()
    {
        var balances = new Dictionary<int, decimal>
        {
            { 1, 135.50m },
            { 2, 45.25m },
            { 3, -80.00m },
            { 4, -75.50m },
            { 5, -25.25m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        var finalBalances = new Dictionary<int, decimal>(balances);
        foreach (var t in settlements)
        {
            finalBalances[t.FromMemberId] += t.Amount;
            finalBalances[t.ToMemberId] -= t.Amount;
        }

        foreach (var b in finalBalances.Values)
        {
            Assert.True(Math.Abs(b) < 0.001m, $"Expected zero balance but got {b}");
        }
    }

    [Fact]
    public void Settle_FirstTransfer_PairsLargestDebtorWithLargestCreditor()
    {
        // Debtors: Member 2 (-40), Member 3 (-80, largest debtor)
        // Creditors: Member 1 (+70), Member 4 (+50)
        var balances = new Dictionary<int, decimal>
        {
            { 1, 70.00m },
            { 2, -40.00m },
            { 3, -80.00m },
            { 4, 50.00m }
        };

        var settlements = SettlementCalculator.Settle(balances);

        // First transfer must pair largest debtor (Member 3) with largest creditor (Member 1)
        Assert.NotEmpty(settlements);
        Assert.Equal(3, settlements[0].FromMemberId);
        Assert.Equal(1, settlements[0].ToMemberId);
        Assert.Equal(70.00m, settlements[0].Amount);
    }

    [Fact]
    public void Settle_TiesBreakDeterministicallyByMemberIdOnEveryRun()
    {
        // Equal debtors: 10 owes 50, 20 owes 50
        // Equal creditors: 1 is owed 50, 2 is owed 50
        var balances = new Dictionary<int, decimal>
        {
            { 20, -50.00m },
            { 10, -50.00m },
            { 2, 50.00m },
            { 1, 50.00m }
        };

        var firstRun = SettlementCalculator.Settle(balances);
        var secondRun = SettlementCalculator.Settle(balances);

        Assert.Equal(2, firstRun.Count);
        Assert.Equal(firstRun.Count, secondRun.Count);

        for (int i = 0; i < firstRun.Count; i++)
        {
            Assert.Equal(firstRun[i], secondRun[i]);
        }

        // Deterministic tie-breaker selects smaller ID first: debtor 10 to creditor 1
        Assert.Equal(10, firstRun[0].FromMemberId);
        Assert.Equal(1, firstRun[0].ToMemberId);
        Assert.Equal(50.00m, firstRun[0].Amount);

        // Second transfer: debtor 20 to creditor 2
        Assert.Equal(20, firstRun[1].FromMemberId);
        Assert.Equal(2, firstRun[1].ToMemberId);
        Assert.Equal(50.00m, firstRun[1].Amount);
    }
}
