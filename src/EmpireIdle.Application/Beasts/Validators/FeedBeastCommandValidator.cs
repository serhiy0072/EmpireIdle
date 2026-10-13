using EmpireIdle.Application.Beasts.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Beasts.Validators
{
    public sealed class FeedBeastCommandValidator : AbstractValidator<FeedBeastCommand>
    {
        /// <summary>Стеля одного годування: тримає запит далеко від межі int у кривій досвіду.</summary>
        public const int MaxPerFeeding = 100_000;

        public FeedBeastCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BeastKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, MaxPerFeeding);
        }
    }
}
