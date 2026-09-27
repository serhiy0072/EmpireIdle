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
            // Послідовно й кожному свій контекст: async-валідатор може ходити в DbContext,
            // а той не терпить двох операцій одночасно; спільний контекст ще й накопичує
            // помилки попередніх валідаторів і дублює їх у кожному результаті
            var failures = new List<FluentValidation.Results.ValidationFailure>();

            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
                failures.AddRange(result.Errors.Where(f => f is not null));
            }

            if (failures.Count != 0)
                throw new ValidationException(failures);

            return await next(cancellationToken);
        }
    }
}
