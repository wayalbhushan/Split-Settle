namespace SplitSettle.Api.Errors;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment env, ILogger<GlobalExceptionHandler> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails();

        switch (exception)
        {
            case NotFoundException notFound:
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = "Not Found";
                problemDetails.Detail = notFound.Message;
                break;

            case ConflictException conflict:
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Conflict";
                problemDetails.Detail = conflict.Message;
                break;

            case ValidationException validation:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Bad Request";
                problemDetails.Detail = validation.Message;
                problemDetails.Extensions["errors"] = validation.Errors;
                foreach (var (key, value) in validation.Errors)
                {
                    problemDetails.Extensions[key] = value;
                }
                break;

            case ArgumentException argument:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Bad Request";
                problemDetails.Detail = argument.Message;
                break;

            case LlmNotConfiguredException notConfigured:
                problemDetails.Status = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Title = "Service Unavailable";
                problemDetails.Detail = notConfigured.Message;
                break;

            case LlmProviderException providerEx:
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Title = "Bad Gateway";
                problemDetails.Detail = providerEx.Message;
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "An error occurred while processing your request.";
                problemDetails.Detail = _env.IsDevelopment() ? exception.ToString() : null;
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
