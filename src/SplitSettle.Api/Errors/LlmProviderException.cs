namespace SplitSettle.Api.Errors;

public class LlmProviderException : Exception
{
    public LlmProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
