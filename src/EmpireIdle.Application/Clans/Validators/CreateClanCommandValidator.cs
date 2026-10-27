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

            // Обробник зберігає назву після Trim — довжину міряємо так само, і без кратних пробілів
            RuleFor(x => x.Name)
                .Must(name => name.Trim().Length >= 3 && !name.Contains("  "))
                .When(x => x.Name is not null)
                .WithMessage("Clan name needs at least 3 characters without leading, trailing or double spaces.");

            RuleFor(x => x.Tag)
                .NotEmpty()
                .Length(2, 5)
                .Matches("^[A-Za-z0-9]+$")
                .WithMessage("Clan tag may contain letters and digits only.");
        }
    }
}
