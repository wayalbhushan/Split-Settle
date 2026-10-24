namespace SplitSettle.Api.Errors;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string error)
        : base(error)
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = new[] { error }
        };
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }
}
