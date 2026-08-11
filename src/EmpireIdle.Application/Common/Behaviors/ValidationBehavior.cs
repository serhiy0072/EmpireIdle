using FluentValidation;
using MediatR;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Pipeline behavior: валідує запит усіма зареєстрованими валідаторами
    /// ДО того, як він дійде до хендлера. Кидає ValidationException при помилках.
    /// </summary>
    public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if(_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);

                var failures = (await Task.WhenAll(
                       _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                    .SelectMany(result => result.Errors)
                    .Where(f => f is not null)
                    .ToList();

                if (failures.Count != 0)
                    throw new ValidationException(failures);
            }

            return await next(cancellationToken);
        }
    }
}
