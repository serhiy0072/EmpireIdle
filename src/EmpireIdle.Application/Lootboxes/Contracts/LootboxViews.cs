namespace EmpireIdle.Application.Lootboxes.Contracts
{
    /// <summary>
    /// Що випало зі скрині — вже видане. Випадковий артефакт тут уже конкретний предмет (Equipment),
    /// а вкладена скриня розкрита у власні рядки, тож клієнт показує рівно те, що отримав гравець.
    /// </summary>
    /// <param name="Type">Тип нагороди: Item, Equipment, Gems, Resource, HeroShards…</param>
    /// <param name="Key">Ключ предмета, ресурсу чи героя; null для gems.</param>
    /// <param name="Source">Скриня, з якої випав рядок: та, що відкривали, або вкладена.</param>
    /// <param name="WasPity">Рядок віддала гарантія.</param>
    public record LootboxDrop(string Type, string? Key, int Amount, string Source, bool WasPity);

    /// <summary>Підсумок відкриття.</summary>
    /// <param name="Drops">У порядку відкриттів.</param>
    public record LootboxOpening(string ItemKey, IReadOnlyList<LootboxDrop> Drops);

    /// <summary>Покупка з відкриттям — разом із залишком gems, щоб клієнт не перепитував гаманець.</summary>
    public record LootboxPurchase(string ItemKey, IReadOnlyList<LootboxDrop> Drops, int GemBalance);
}
