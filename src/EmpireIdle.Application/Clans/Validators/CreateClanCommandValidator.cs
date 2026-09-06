using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    /// <summary>
    /// Межі збігаються з колонками: Clans.Name(32), Clans.Tag(5).
    /// Без цього довга назва долітає до бази і повертається 500.
    /// </summary>
    public sealed class CreateClanCommandValidator : AbstractValidator<CreateClanCommand>
    {
        public CreateClanCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();

            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(3, 32)
                .Matches(@"^[\p{L}0-9 '\-]+$")
                .WithMessage("Clan name may contain letters, digits, spaces, apostrophes and hyphens.");

            RuleFor(x => x.Tag)
                .NotEmpty()
                .Length(2, 5)
                .Matches("^[A-Za-z0-9]+$")
                .WithMessage("Clan tag may contain letters and digits only.");
        }
    }
}
