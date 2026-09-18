using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
                    Instance = httpContext.Request.Path,
                    Extensions = { ["errorCode"] = "Validation" }
                };

                _logger.LogWarning("Validation failed on {Path}: {Errors}", httpContext.Request.Path, errors);

                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
                return true;
            }

            // errorCode — контракт із клієнтом. Назви типів не беремо рефлексією:
            // перейменування винятку не має мовчки ламати гілку у фронтенді
            var (statusCode, title, errorCode) = exception switch
            {
                AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication Failed", "AuthenticationFailed"),
                EntityNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "NotFound"),
                IdempotencyKeyReusedException => (StatusCodes.Status422UnprocessableEntity, "Idempotency Key Reused", "IdempotencyKeyReused"),
                OperationInProgressException => (StatusCodes.Status409Conflict, "Operation In Progress", "OperationInProgress"),
                NotEnoughResourcesException => (StatusCodes.Status400BadRequest, "Not Enough Resources", "NotEnoughResources"),
                RequirementNotMetException => (StatusCodes.Status400BadRequest, "Requirement Not Met", "RequirementNotMet"),
                AlreadyExistsException => (StatusCodes.Status400BadRequest, "Already Exists", "AlreadyExists"),
                DomainException => (StatusCodes.Status400BadRequest, "Domain Rule Violated", "DomainRuleViolated"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "Forbidden"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument", "InvalidArgument"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The resource was modified by another request. Retry with the current state.", "ConcurrencyConflict"),
                // Унікальний індекс — арбітр гонки (уламки, замовлення, дублікат героя).
                // Його вердикт — конфлікт, а не інцидент: 409, без LogError
                DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (StatusCodes.Status409Conflict, "The resource already exists. Retry with the current state.", "AlreadyExists"),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "Internal")
            };

            // 4xx — очікувана відмова, не інцидент: Error лишаємо для 5xx
            if (statusCode >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
            else
                _logger.LogWarning("Request to {Path} rejected with {StatusCode} {ErrorCode}: {Message}",
                    httpContext.Request.Path, statusCode, errorCode, exception.Message);

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
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier,
                    ["errorCode"] = errorCode
                }
            };

            // Цифри нестачі — окремими полями, щоб клієнт не розбирав Detail
            if (exception is NotEnoughResourcesException shortfall)
            {
                problemDetails.Extensions["resource"] = shortfall.Resource;
                problemDetails.Extensions["need"] = shortfall.Need;
                problemDetails.Extensions["have"] = shortfall.Have;
            }

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
