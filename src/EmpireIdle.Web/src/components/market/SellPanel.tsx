import { useMemo, useState } from "react";
import { useCatalog } from "../../lib/queries/catalog";
import { useInventory } from "../../lib/queries/inventory";
import { MARKET_KIND, useListOnMarket, useMarketQuote, type MarketGoods } from "../../lib/queries/market";
import ErrorBanner from "../ErrorBanner";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

interface Props {
  playerId: string;
  /** Чи є вільний лот: без нього форма лише пояснює, чому продати не можна. */
  canList: boolean;
  onListed: () => void;
}

interface Option {
  id: string;
  label: string;
  goods: MarketGoods;
  /** Скільки штук є — лише для стакових предметів. */
  available?: number;
}

/**
 * Виставлення на ринок: вибір товару, діапазон ціни від сервера, податок.
 * Діапазон приходить із котирування, тож форма не пропустить ціну,
 * яку команда потім відхилить.
 */
export default function SellPanel({ playerId, canList, onListed }: Props) {
  const catalog = useCatalog();
  const inventory = useInventory(playerId);
  const list = useListOnMarket(playerId);

  const [selectedId, setSelectedId] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [price, setPrice] = useState<number | null>(null);

  const options = useMemo<Option[]>(() => {
    const equipment = (inventory.data?.equipment ?? [])
      .filter((item) => !item.isOnMarket)
      .map((item) => ({
        id: `e:${item.id}`,
        label: `${catalog.itemName(item.itemKey)}${item.level > 0 ? ` · ${t("items:artifact.level", { level: item.level })}` : ""}${
          item.equippedByHeroId != null ? ` ${t("items:sell.equipped")}` : ""
        }`,
        goods: { kind: MARKET_KIND.Equipment, equipmentId: item.id, quantity: 1 },
      }));

    const stacks = (inventory.data?.items ?? [])
      .filter((item) => item.count > 0 && catalog.item(item.itemKey)?.tradeable === true)
      .map((item) => ({
        id: `i:${item.itemKey}`,
        label: `${item.displayName} ${t("common:owned", { count: item.count })}`,
        goods: { kind: MARKET_KIND.Item, itemKey: item.itemKey, quantity: 1 },
        available: item.count,
      }));

    return [...equipment, ...stacks];
  }, [inventory.data, catalog]);

  const selected = options.find((option) => option.id === selectedId) ?? null;
  const safeQuantity = selected?.available !== undefined ? Math.min(selected.available, Math.max(1, quantity)) : 1;
  const goods: MarketGoods | null = selected === null ? null : { ...selected.goods, quantity: safeQuantity };

  const quote = useMarketQuote(playerId, goods);

  const chosenPrice = price ?? quote.data?.suggestedPrice ?? 0;
  const inRange = quote.data !== undefined && chosenPrice >= quote.data.minPrice && chosenPrice <= quote.data.maxPrice;
  const tax = quote.data === undefined ? 0 : Math.max(1, Math.ceil(chosenPrice * quote.data.taxShare));

  if (!canList) {
    return <p className="text-sm text-slate-500">{t("items:sell.full")}</p>;
  }

  if (options.length === 0) {
    return (
      <p className="text-sm text-slate-500">
        {t("items:sell.nothing")}
      </p>
    );
  }

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <ErrorBanner error={list.error ?? quote.error} />

      <label className="block text-sm">
        <span className="text-slate-600">{t("items:sell.what")}</span>
        <select
          value={selectedId}
          onChange={(event) => {
            setSelectedId(event.target.value);
            setQuantity(1);
            setPrice(null);
          }}
          className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-2 py-1"
        >
          <option value="">{t("items:sell.pick")}</option>
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </label>

      {selected?.available !== undefined && (
        <label className="block text-sm">
          <span className="text-slate-600">{t("items:sell.quantity", { count: selected.available })}</span>
          <input
            type="number"
            min={1}
            max={selected.available}
            value={safeQuantity}
            onChange={(event) => {
              setQuantity(Number(event.target.value));
              setPrice(null);
            }}
            className="mt-1 w-32 rounded-lg border border-slate-300 px-2 py-1"
          />
        </label>
      )}

      {quote.data !== undefined && (
        <>
          <label className="block text-sm">
            <span className="text-slate-600">
              {t("items:sell.price", { min: formatNumber(quote.data.minPrice), max: formatNumber(quote.data.maxPrice) })}
            </span>
            <input
              type="number"
              min={quote.data.minPrice}
              max={quote.data.maxPrice}
              value={chosenPrice}
              onChange={(event) => setPrice(Number(event.target.value))}
              className="mt-1 w-40 rounded-lg border border-slate-300 px-2 py-1"
            />
          </label>

          <p className="text-xs text-slate-500">
            {t("items:sell.tax", { tax: formatNumber(tax) })}
          </p>

          <button
            type="button"
            disabled={!inRange || list.isPending || goods === null}
            onClick={() =>
              goods !== null &&
              list.mutate(
                { ...goods, priceGold: chosenPrice },
                {
                  onSuccess: () => {
                    setSelectedId("");
                    setPrice(null);
                    onListed();
                  },
                },
              )
            }
            className="rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {t("items:sell.list")}
          </button>
        </>
      )}
    </div>
  );
}
