using EmpireIdle.Application.Lootboxes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Lootboxes.Validators
{
    public sealed class OpenLootboxCommandValidator : AbstractValidator<OpenLootboxCommand>
    {
        public OpenLootboxCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, OpenLootboxCommand.MaxCount);
        }
    }
}
