using EmpireIdle.Application.Villages.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Villages.Validators
{
    /// <summary>Форма запиту; межі муру й сітку перевіряє домен — з причиною для гравця.</summary>
    public sealed class MoveBuildingCommandValidator : AbstractValidator<MoveBuildingCommand>
    {
        public MoveBuildingCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BuildingId).NotEmpty();
            RuleFor(x => x.X).InclusiveBetween(0, 100);
            RuleFor(x => x.Y).InclusiveBetween(0, 100);
        }
    }
}
