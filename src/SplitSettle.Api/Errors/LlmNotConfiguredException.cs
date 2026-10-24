namespace SplitSettle.Api.Errors;

public class LlmNotConfiguredException : Exception
{
    public LlmNotConfiguredException(string message = "AI parse service is not configured. An API key is required.")
        : base(message)
    {
    }
}
