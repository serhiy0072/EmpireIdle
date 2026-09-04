using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Domain.Exceptions;
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
            if (exception is ValidationException validationException)
            {
                var errors = validationException.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                var validationProblem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Instance = httpContext.Request.Path
                };

                _logger.LogWarning("Validation failed on {Path}: {Errors}", httpContext.Request.Path, errors);

                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
                return true;
            }

            var (statusCode, title) = exception switch
            {
                AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication Failed"),
                EntityNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
                IdempotencyKeyReusedException => (StatusCodes.Status422UnprocessableEntity, "Idempotency Key Reused"),
                OperationInProgressException => (StatusCodes.Status409Conflict, "Operation In Progress"),
                DomainException => (StatusCodes.Status400BadRequest, "Domain Rule Violated"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was modified by another request. Retry with the current state."),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            // 4xx — очікувана відмова, не інцидент: Error лишаємо для 5xx
            if (statusCode >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
            else
                _logger.LogWarning("Request to {Path} rejected with {StatusCode}: {Message}",
                    httpContext.Request.Path, statusCode, exception.Message);

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
