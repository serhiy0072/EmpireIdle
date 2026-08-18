using EmpireIdle.Application.Quests.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Quests.Validators
{
    public sealed class ClaimQuestRewardCommandValidator : AbstractValidator<ClaimQuestRewardCommand>
    {
        public ClaimQuestRewardCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.QuestKey).NotEmpty().MaximumLength(50);
        }
    }
}
