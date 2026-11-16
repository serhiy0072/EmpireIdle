using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Domain.Enums;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    /// <summary>Межі полів ролі, спільні для створення й зміни.</summary>
    internal static class ClanRoleRules
    {
        /// <summary>Довжина назви — колонка ClanRoles.Name.</summary>
        public const int MaxNameLength = 32;

        /// <summary>Ранг 0..100 (ClanRole.Rank); 100 — лише лідер, і вище за себе агрегат однаково не дасть.</summary>
        public const int MaxRank = 100;

        /// <summary>Усі відомі прапорці разом: будь-який біт поза ними — підроблений запит.</summary>
        public static readonly ClanPermission Known = Enum.GetValues<ClanPermission>().Aggregate(ClanPermission.None, (all, p) => all | p);

        public static bool AreKnown(ClanPermission permissions) => (permissions & ~Known) == ClanPermission.None;
    }

    public sealed class CreateClanRoleCommandValidator : AbstractValidator<CreateClanRoleCommand>
    {
        public CreateClanRoleCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(ClanRoleRules.MaxNameLength);
            RuleFor(x => x.Rank).InclusiveBetween(0, ClanRoleRules.MaxRank);
            RuleFor(x => x.Permissions).Must(ClanRoleRules.AreKnown);
        }
    }

    public sealed class UpdateClanRoleCommandValidator : AbstractValidator<UpdateClanRoleCommand>
    {
        public UpdateClanRoleCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RoleId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(ClanRoleRules.MaxNameLength);
            RuleFor(x => x.Rank).InclusiveBetween(0, ClanRoleRules.MaxRank);
            RuleFor(x => x.Permissions).Must(ClanRoleRules.AreKnown);
        }
    }

    public sealed class DeleteClanRoleCommandValidator : AbstractValidator<DeleteClanRoleCommand>
    {
        public DeleteClanRoleCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RoleId).NotEmpty();
        }
    }
}
