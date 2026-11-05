namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>
    /// Клановий магазин гравця. Вітрини за монети відкриті лише в клані, безкоштовна — усім,
    /// крім предметів із RequiresClan. Монети й ліміти особисті, тож показуються й без клану.
    /// </summary>
    public record ClanShopView(
        long Coins,
        bool InClan,
        ClanShopShowcaseView Daily,
        ClanShopShowcaseView Weekly,
        ClanFreeShowcaseView Free);

    public record ClanShopShowcaseView(DateTime RefreshesAt, List<ClanShopOfferView> Offers);

    /// <param name="Price">Ціна вже зі знижкою; клієнт шле її назад, щоб зміна вітрини дала відмову, а не іншу суму.</param>
    /// <param name="Purchased">Скільки куплено за цей період, із Limit.</param>
    public record ClanShopOfferView(string ItemKey, int BasePrice, int Price, int DiscountPercent, int Limit, int Purchased);

    public record ClanFreeShowcaseView(DateTime RefreshesAt, List<ClanFreeItemView> Items);

    /// <param name="Claimed">Скільки забрано цього тижня, із Limit.</param>
    public record ClanFreeItemView(string ItemKey, int Limit, int Claimed, bool RequiresClan);
}
