namespace SplitSettle.Core.Services;

public static class SettlementCalculator
{
    public static List<Transfer> Settle(IReadOnlyDictionary<int, decimal> balances)
    {
        var transfers = new List<Transfer>();

        if (balances == null || balances.Count == 0)
        {
            return transfers;
        }

        const decimal Tolerance = 0.001m;

        var debtors = new PriorityQueue<(int MemberId, decimal Amount), (decimal NegatedAmount, int MemberId)>();
        var creditors = new PriorityQueue<(int MemberId, decimal Amount), (decimal NegatedAmount, int MemberId)>();

        foreach (var (memberId, balance) in balances)
        {
            if (balance > Tolerance)
            {
                creditors.Enqueue((memberId, balance), (-balance, memberId));
            }
            else if (balance < -Tolerance)
            {
                decimal owed = -balance;
                debtors.Enqueue((memberId, owed), (-owed, memberId));
            }
        }

        while (debtors.Count > 0 && creditors.Count > 0)
        {
            var (debtorId, debtorAmount) = debtors.Dequeue();
            var (creditorId, creditorAmount) = creditors.Dequeue();

            decimal settleAmount = Math.Min(debtorAmount, creditorAmount);
            transfers.Add(new Transfer(debtorId, creditorId, settleAmount));

            decimal remainingDebtor = debtorAmount - settleAmount;
            if (remainingDebtor > Tolerance)
            {
                debtors.Enqueue((debtorId, remainingDebtor), (-remainingDebtor, debtorId));
            }

            decimal remainingCreditor = creditorAmount - settleAmount;
            if (remainingCreditor > Tolerance)
            {
                creditors.Enqueue((creditorId, remainingCreditor), (-remainingCreditor, creditorId));
            }
        }

        return transfers;
    }
}
