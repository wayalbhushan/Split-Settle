namespace SplitSettle.Api.Errors;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
