using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Domain.Enums;
using FluentValidation;

namespace EmpireIdle.Application.Market.Validators
{
    /// <summary>
    /// Форма запиту: вид задає, яке з полів має бути заповнене. Решта полів
    /// іншого виду — помилка клієнта, а не тихе ігнорування.
    /// </summary>
    public sealed class ListOnMarketCommandValidator : AbstractValidator<ListOnMarketCommand>
    {
        /// <summary>Стеля пачки: захист від переповнення ціни за одиницю.</summary>
        public const int MaxQuantity = 1000;

        public ListOnMarketCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Kind).IsInEnum();
            RuleFor(x => x.PriceGold).GreaterThan(0);

            When(x => x.Kind == MarketListingKind.Equipment, () =>
            {
                RuleFor(x => x.EquipmentId).NotNull().NotEqual(Guid.Empty);
                RuleFor(x => x.HeroId).Null();
                RuleFor(x => x.ItemKey).Null();
                RuleFor(x => x.Quantity).Equal(1);
            });

            When(x => x.Kind == MarketListingKind.Hero, () =>
            {
                RuleFor(x => x.HeroId).NotNull().NotEqual(Guid.Empty);
                RuleFor(x => x.EquipmentId).Null();
                RuleFor(x => x.ItemKey).Null();
                RuleFor(x => x.Quantity).Equal(1);
            });

            When(x => x.Kind == MarketListingKind.Item, () =>
            {
                RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
                RuleFor(x => x.EquipmentId).Null();
                RuleFor(x => x.HeroId).Null();
                RuleFor(x => x.Quantity).InclusiveBetween(1, MaxQuantity);
            });
        }
    }
}
