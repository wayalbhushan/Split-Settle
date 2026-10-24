namespace SplitSettle.Core.Services;

public static class SplitCalculator
{
    /// <summary>
    /// Splits an amount equally into shares for the given memberIds.
    /// Leftover cents are given one by one to members in ascending order of their IDs.
    /// If the amount has more than 2 real decimal places (e.g. 10.005m), throws ArgumentException.
    /// Trailing zeros (e.g. 10.000m) are accepted.
    /// </summary>
    public static Dictionary<int, decimal> Split(decimal amount, IReadOnlyList<int> memberIds)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        if (decimal.Truncate(amount * 100m) != (amount * 100m))
        {
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", nameof(amount));
        }

        if (memberIds == null || memberIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var sortedIds = memberIds.OrderBy(id => id).ToList();
        long totalCents = (long)(amount * 100m);
        int n = sortedIds.Count;
        long baseCents = totalCents / n;
        int remainderCents = (int)(totalCents % n);

        var shares = new Dictionary<int, decimal>(n);
        for (int i = 0; i < n; i++)
        {
            long centsForMember = baseCents + (i < remainderCents ? 1 : 0);
            shares[sortedIds[i]] = centsForMember / 100m;
        }

        return shares;
    }
}
