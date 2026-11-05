using System.Security.Cryptography;
using System.Text;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Technologies
{
    public enum ClanShopKind
    {
        Daily = 0,
        Weekly = 1
    }

    /// <summary>Пропозиція вітрини на цей період: ціна вже зі знижкою.</summary>
    public record ClanShopOffer(string ItemKey, int BasePrice, int Price, int DiscountPercent, int Limit);

    /// <summary>
    /// Клановий магазин (рішення 03.11): вітрина виводиться з періоду, а не зберігається. Сід — хеш періоду,
    /// тож усі гравці й усі процеси бачать те саме, клан на асортимент не впливає, а зберігати нема чого.
    /// Зміна пулу в конфігу пересуває чинну вітрину: це прийнятна ціна за відсутність стану.
    /// </summary>
    public sealed class ClanShop
    {
        private readonly ClanShopConfig _config;

        public ClanShop(ClanShopConfig config) => _config = config;

        /// <summary>Період, до якого належить момент: доба UTC або тиждень UTC від понеділка.</summary>
        public static string Period(ClanShopKind kind, DateTime utcNow)
        {
            return kind == ClanShopKind.Daily
                ? $"daily:{DateOnly.FromDateTime(utcNow):yyyy-MM-dd}"
                : $"weekly:{MondayOf(utcNow):yyyy-MM-dd}";
        }

        /// <summary>Тиждень безкоштовної вітрини; окремий префікс, щоб ліміт не змішувався з покупками за монети.</summary>
        public static string FreePeriod(DateTime utcNow) => $"free:{MondayOf(utcNow):yyyy-MM-dd}";

        private static DateOnly MondayOf(DateTime utcNow)
        {
            var date = DateOnly.FromDateTime(utcNow);

            return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        /// <summary>Періоди, чиї ліміти ще мають значення; решту особиста книга може забути.</summary>
        public static IReadOnlyCollection<string> LivePeriods(DateTime utcNow)
            => [Period(ClanShopKind.Daily, utcNow), Period(ClanShopKind.Weekly, utcNow), FreePeriod(utcNow)];

        public IReadOnlyList<ClanFreeItemConfig> FreeWeekly => _config.FreeWeekly;

        public IReadOnlyList<ClanShopOffer> Offers(ClanShopKind kind, DateTime utcNow)
        {
            var (slots, pool, discounts) = kind == ClanShopKind.Daily
                ? (_config.DailySlots, _config.DailyPool, _config.DailyDiscounts)
                : (_config.WeeklySlots, _config.WeeklyPool, _config.WeeklyDiscounts);

            var random = new DeterministicRandom(SeedOf(Period(kind, utcNow)));
            var left = new List<ClanShopItemConfig>(pool);
            var offers = new List<ClanShopOffer>();

            while (offers.Count < slots && left.Count > 0)
            {
                var item = Pick(left, i => i.Weight, random);
                left.Remove(item);

                // Знижку кидаємо на кожен слот, щоб її не можна було передбачити з наявності предмета
                var rolled = discounts.Count == 0 ? 0 : Pick(discounts, d => d.Weight, random).Percent;
                var percent = Math.Min(rolled, item.MaxDiscount);
                var price = Math.Max((int)Math.Round(item.Price * (100 - percent) / 100.0), 1);

                offers.Add(new ClanShopOffer(item.ItemKey, item.Price, price, percent, item.Limit));
            }

            return offers;
        }

        private static T Pick<T>(IReadOnlyList<T> options, Func<T, double> weight, IRandomSource random)
        {
            var roll = random.NextDouble() * options.Sum(weight);

            foreach (var option in options)
            {
                if (roll < weight(option))
                    return option;

                roll -= weight(option);
            }

            return options[^1];
        }

        /// <summary>Хеш, а не string.GetHashCode: той різний у кожному процесі, і гравці бачили б різні вітрини.</summary>
        private static ulong SeedOf(string period)
            => BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(period)), 0);
    }
}
