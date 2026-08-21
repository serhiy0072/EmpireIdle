using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
                return true;
            }

            var (statusCode, title) = exception switch
            {
                System.ComponentModel.DataAnnotations.ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed"),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "BadRequest"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was modified by another request. Retry with the current state."),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            // На 500 не віддаємо exception.Message: DbUpdateException містить імена
            // таблиць і констрейнтів, NpgsqlException — деталі підключення
            var detail = statusCode is StatusCodes.Status500InternalServerError
                                    or StatusCodes.Status409Conflict
                ? title
                : exception.Message;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            };

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
