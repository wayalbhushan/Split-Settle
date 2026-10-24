using SplitSettle.Core.Services;
using Xunit;

namespace SplitSettle.Tests;

public class SplitCalculatorTests
{
    [Fact]
    public void Split_100Among3_DistributesLeftoverCentToFirstMemberInIdOrder()
    {
        var memberIds = new List<int> { 1, 2, 3 };
        var shares = SplitCalculator.Split(100.00m, memberIds);

        Assert.Equal(3, shares.Count);
        Assert.Equal(33.34m, shares[1]);
        Assert.Equal(33.33m, shares[2]);
        Assert.Equal(33.33m, shares[3]);
        Assert.Equal(100.00m, shares.Values.Sum());
    }

    [Fact]
    public void Split_OneCentAmongThree_GivesOneCentToFirstMemberAndZeroToOthers()
    {
        var memberIds = new List<int> { 1, 2, 3 };
        var shares = SplitCalculator.Split(0.01m, memberIds);

        Assert.Equal(0.01m, shares[1]);
        Assert.Equal(0.00m, shares[2]);
        Assert.Equal(0.00m, shares[3]);
        Assert.Equal(0.01m, shares.Values.Sum());
    }

    [Fact]
    public void Split_TenAmongOne_GivesFullAmountToOneMember()
    {
        var memberIds = new List<int> { 42 };
        var shares = SplitCalculator.Split(10.00m, memberIds);

        Assert.Single(shares);
        Assert.Equal(10.00m, shares[42]);
    }

    [Fact]
    public void Split_UnsortedMemberIds_ExtraCentsStillGoInAscendingIdOrder()
    {
        // Passed in reverse: 30, 10, 20
        var memberIds = new List<int> { 30, 10, 20 };
        var shares = SplitCalculator.Split(100.00m, memberIds);

        // Smallest id 10 should receive the extra cent 33.34
        Assert.Equal(33.34m, shares[10]);
        Assert.Equal(33.33m, shares[20]);
        Assert.Equal(33.33m, shares[30]);
        Assert.Equal(100.00m, shares.Values.Sum());
    }

    [Fact]
    public void Split_ZeroMembers_ReturnsEmptyDictionary()
    {
        var shares = SplitCalculator.Split(100.00m, new List<int>());
        Assert.Empty(shares);
    }

    [Fact]
    public void Split_ZeroAmount_ReturnsZeroShareForEveryMember()
    {
        var memberIds = new List<int> { 1, 2, 3 };
        var shares = SplitCalculator.Split(0.00m, memberIds);

        Assert.Equal(3, shares.Count);
        Assert.All(shares.Values, share => Assert.Equal(0.00m, share));
        Assert.Equal(0.00m, shares.Values.Sum());
    }

    [Fact]
    public void Split_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var memberIds = new List<int> { 1, 2 };
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitCalculator.Split(-15.00m, memberIds));
    }

    [Fact]
    public void Split_AmountWithThreeDecimals_TruncatesOrRoundsToWholeCents()
    {
        var memberIds = new List<int> { 1, 2 };
        // 10.005m truncated/rounded to 10.00m or 10.01m
        var shares = SplitCalculator.Split(10.005m, memberIds);

        Assert.Equal(2, shares.Count);
        Assert.Equal(shares.Values.Sum(), Math.Round(10.005m, 2));
    }

    [Fact]
    public void Split_VariousAmountsAndMemberCounts1To12_SharesAlwaysSumToAmount()
    {
        decimal[] testAmounts = { 0.01m, 0.02m, 0.99m, 1.00m, 5.55m, 10.00m, 33.33m, 100.00m, 1234.56m };

        for (int k = 1; k <= 12; k++)
        {
            var memberIds = Enumerable.Range(1, k).ToList();
            foreach (var amount in testAmounts)
            {
                var shares = SplitCalculator.Split(amount, memberIds);

                Assert.Equal(k, shares.Count);
                Assert.Equal(amount, shares.Values.Sum());
            }
        }
    }
}
