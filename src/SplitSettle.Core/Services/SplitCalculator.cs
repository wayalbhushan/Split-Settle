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
        throw new NotImplementedException();
    }
}
