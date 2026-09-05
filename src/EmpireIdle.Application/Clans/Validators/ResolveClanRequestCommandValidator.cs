using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class ResolveClanRequestCommandValidator : AbstractValidator<ResolveClanRequestCommand>
    {
        public ResolveClanRequestCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RequestId).NotEmpty();
        }
    }
}
