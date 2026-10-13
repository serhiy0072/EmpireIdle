using EmpireIdle.Application.Speedups.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Speedups.Validators
{
    public sealed class SpeedUpWithItemsCommandValidator : AbstractValidator<SpeedUpWithItemsCommand>
    {
        public SpeedUpWithItemsCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.TargetId).NotEmpty();
            RuleFor(x => x.Timer).IsInEnum();

            // Сім видів прискорень — більше рядків у запиті означало б помилку клієнта
            RuleFor(x => x.Items).NotEmpty().Must(items => items.Count <= 10)
                .WithMessage("Too many kinds of speed-ups in one request.");
            RuleForEach(x => x.Items).Must(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Key.Length <= 50)
                .WithMessage("Item key is required.");
            RuleForEach(x => x.Items).Must(pair => pair.Value is >= 1 and <= 1000)
                .WithMessage("Each speed-up count must be between 1 and 1000.");
        }
    }
}
