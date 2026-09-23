import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import type { ShopItemView } from "../lib/apiTypes";
import { useBuyShopItem, useCheckout, useShop } from "../lib/queries/shop";
import { useWallet } from "../lib/queries/wallet";
import { rarityLabel, rarityStyle } from "../lib/rarity";

const TYPE_LABELS: Record<string, string> = {
  evolution: "Еволюція героя",
  boost: "Буст",
  resources: "Ресурси",
  teleport: "Телепорт",
};

function price(cents: number, currency: string): string {
  return new Intl.NumberFormat("uk-UA", { style: "currency", currency: currency.toUpperCase() }).format(cents / 100);
}

interface OfferProps {
  offer: ShopItemView;
  gems: number;
  busy: boolean;
  onBuy: (count: number) => void;
}

function Offer({ offer, gems, busy, onBuy }: OfferProps) {
  const [count, setCount] = useState(1);
  const safeCount = Math.min(offer.maxPerPurchase, Math.max(1, count));
  const total = offer.priceGems * safeCount;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex gap-3">
        <ItemIcon itemKey={offer.itemKey} type={offer.type} rarity={offer.rarity} size={48} />
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-medium text-slate-800">{offer.displayName}</span>
            <span className="rounded-full bg-violet-100 px-2 py-0.5 text-xs text-violet-800">💎 {offer.priceGems}</span>
          </div>
          <div className="mt-1 flex items-center gap-1 text-xs">
            <span className={`rounded px-2 py-0.5 ${rarityStyle(offer.rarity)}`}>{rarityLabel(offer.rarity)}</span>
            <span className="text-slate-500">{TYPE_LABELS[offer.type] ?? offer.type}</span>
          </div>
        </div>
      </div>

      {offer.description !== "" && <p className="mt-2 text-sm text-slate-600">{offer.description}</p>}

      <div className="mt-3 flex items-center gap-2">
        {offer.maxPerPurchase > 1 && (
          <input
            type="number"
            min={1}
            max={offer.maxPerPurchase}
            value={safeCount}
            onChange={(event) => setCount(Number(event.target.value))}
            className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-sm"
          />
        )}
        <button
          type="button"
          onClick={() => onBuy(safeCount)}
          disabled={busy || gems < total}
          className="ml-auto rounded-lg bg-violet-600 px-3 py-1 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
        >
          {gems < total ? "Не вистачає 💎" : `Купити за ${total.toLocaleString("uk-UA")} 💎`}
        </button>
      </div>
    </div>
  );
}

export default function ShopPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const shop = useShop();
  const wallet = useWallet(playerId);
  const buy = useBuyShopItem(playerId);
  const checkout = useCheckout(playerId);

  if (shop.isPending) {
    return <p className="text-slate-500">Завантаження крамниці…</p>;
  }

  if (shop.isError) {
    return <ErrorBanner error={shop.error} />;
  }

  const gems = wallet.data?.gemBalance ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Крамниця</h1>
        <p className="text-sm text-slate-500">💎 {gems.toLocaleString("uk-UA")}</p>
      </div>

      <ErrorBanner error={buy.error ?? checkout.error} />

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">За самоцвіти</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          {shop.data.items.map((offer) => (
            <Offer
              key={offer.itemKey}
              offer={offer}
              gems={gems}
              busy={buy.isPending}
              onBuy={(count) => buy.mutate({ itemKey: offer.itemKey, count })}
            />
          ))}
        </div>
      </section>

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Самоцвіти</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {shop.data.gemPacks.map((pack) => (
            <div key={pack.key} className="flex flex-col rounded-xl border border-violet-200 bg-white p-3">
              <span className="font-medium text-slate-800">{pack.displayName}</span>
              <span className="mt-1 text-2xl font-semibold text-violet-700">💎 {pack.gems.toLocaleString("uk-UA")}</span>
              {pack.bonusPercent > 0 && <span className="text-xs text-emerald-700">+{pack.bonusPercent}% бонус</span>}
              {/* Оплата йде на сторінку Stripe: тут лише перехід, жодних платіжних даних */}
              <button
                type="button"
                onClick={() =>
                  checkout.mutate(pack.key, {
                    onSuccess: (result) => window.location.assign(result.checkoutUrl),
                  })
                }
                disabled={checkout.isPending}
                className="mt-3 rounded-lg border border-violet-300 px-3 py-1.5 text-sm font-medium text-violet-800 hover:bg-violet-50 disabled:opacity-50"
              >
                {price(pack.priceCents, shop.data.currency)}
              </button>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
