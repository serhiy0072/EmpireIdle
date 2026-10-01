using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Pipeline behavior: один запис на кожен MediatR-запит — тривалість і результат.
    /// Відмову логуємо як Warning з типом винятку; деталі й стек пише той, хто його ловить.
    /// </summary>
    public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            Exception? failure = null;

            try
            {
                return await next(cancellationToken);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();

                if (failure is null)
                    _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms",
                        typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
                else
                    _logger.LogWarning("Failed {RequestName} in {ElapsedMs}ms with {ExceptionType}",
                        typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, failure.GetType().Name);
            }
        }
    }
}
