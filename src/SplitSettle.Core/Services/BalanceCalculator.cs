using SplitSettle.Core.Entities;

namespace SplitSettle.Core.Services;

public static class BalanceCalculator
{
    public static Dictionary<int, decimal> Compute(IReadOnlyList<int> memberIds, IReadOnlyList<Expense> expenses)
    {
        var balances = new Dictionary<int, decimal>();

        if (memberIds != null)
        {
            foreach (var id in memberIds)
            {
                balances[id] = 0.00m;
            }
        }

        if (expenses != null)
        {
            foreach (var expense in expenses)
            {
                if (balances.ContainsKey(expense.PaidByMemberId))
                {
                    balances[expense.PaidByMemberId] += expense.Amount;
                }
                else
                {
                    balances[expense.PaidByMemberId] = expense.Amount;
                }

                if (expense.Splits != null)
                {
                    foreach (var split in expense.Splits)
                    {
                        if (balances.ContainsKey(split.MemberId))
                        {
                            balances[split.MemberId] -= split.ShareAmount;
                        }
                        else
                        {
                            balances[split.MemberId] = -split.ShareAmount;
                        }
                    }
                }
            }
        }

        return balances;
    }
}
