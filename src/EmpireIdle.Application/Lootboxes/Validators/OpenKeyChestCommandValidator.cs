using EmpireIdle.Application.Lootboxes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Lootboxes.Validators
{
    public sealed class OpenKeyChestCommandValidator : AbstractValidator<OpenKeyChestCommand>
    {
        public OpenKeyChestCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ChestKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, OpenKeyChestCommand.MaxCount);

            // Безкоштовне відкриття — завжди одне
            RuleFor(x => x.Count).Equal(1).When(x => x.Free);
        }
    }
}
