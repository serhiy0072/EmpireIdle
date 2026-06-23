using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace EmpireIdle.API.Middleware
{
    /// <summary>
    /// Глобальний обробник помилок. Перетворює доменні exceptions в ProblemDetails HTTP відповіді.
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Exception handled: {Message}", exception.Message);

            if (exception is ValidationException validationException)
            {
                var errors = validationException.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                var validationProblem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Instance = httpContext.Request.Path
                };

                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; ;
                await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
                return true;
            }

            var (statusCode, title) = exception switch
            {
                InvalidOperationException => (StatusCodes.Status400BadRequest, "BadRequest"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument"),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
