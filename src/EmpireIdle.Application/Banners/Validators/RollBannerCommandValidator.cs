using EmpireIdle.Application.Banners.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Banners.Validators
{
    public sealed class RollBannerCommandValidator : AbstractValidator<RollBannerCommand>
    {
        public RollBannerCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.BannerKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, RollBannerCommand.MaxCount);
        }
    }
}
