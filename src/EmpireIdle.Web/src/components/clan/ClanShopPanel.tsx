import { useState, type ReactNode } from "react";
import type { ClanShopKind, ClanShopOfferView } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { useCatalog } from "../../lib/queries/catalog";
import { useBuyClanShopItem, useClaimClanFreeItem, useClanShop } from "../../lib/queries/clanTechnologies";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import ItemIcon from "../inventory/ItemIcon";
import { t } from "i18next";

interface Props {
  playerId: string;
  now: number;
}

type Showcase = "daily" | "weekly" | "free";

/** ClanShopKind на дроті. */
const KIND: Record<Exclude<Showcase, "free">, ClanShopKind> = { daily: "Daily", weekly: "Weekly" };

const button = "rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50";

/**
 * Клановий магазин: щоденна й щотижнева вітрини за кланові монети та безкоштовна щотижнева.
 * Поза кланом видно лише безкоштовну — купувати за монети може тільки учасник клану.
 */
export default function ClanShopPanel({ playerId, now }: Props) {
  const shop = useClanShop(playerId);
  const buy = useBuyClanShopItem(playerId);
  const claim = useClaimClanFreeItem(playerId);

  const [picked, setPicked] = useState<Showcase | null>(null);

  if (shop.isPending) return <p className="text-sm text-slate-500">{t("social:page.loadingShort")}</p>;
  if (shop.isError) return <ErrorBanner error={shop.error} />;

  const view = shop.data;
  const showcases: Showcase[] = view.inClan ? ["daily", "weekly", "free"] : ["free"];
  const showcase = picked !== null && showcases.includes(picked) ? picked : showcases[0]!;
  const busy = buy.isPending || claim.isPending;
  const latest = buy.submittedAt > claim.submittedAt ? buy : claim;
  const refreshesAt = showcase === "free" ? view.free.refreshesAt : view[showcase].refreshesAt;

  return (
    <div className="space-y-3">
      <ErrorBanner error={latest.error} />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <nav className="flex gap-1">
          {showcases.map((key) => (
            <button
              key={key}
              type="button"
              onClick={() => setPicked(key)}
              className={`rounded-lg px-3 py-1 text-sm ${showcase === key ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`}
            >
              {t(`technologies:shop.${key}`)}
            </button>
          ))}
        </nav>
        <p className="text-sm text-slate-600">
          {view.inClan && <span className="mr-3 font-medium text-amber-700">{t("technologies:shop.coins", { coins: formatNumber(view.coins) })}</span>}
          {t("technologies:shop.refreshes", { remaining: formatRemaining(refreshesAt, now) })}
        </p>
      </div>

      {!view.inClan && <p className="rounded-lg bg-sky-50 px-3 py-2 text-sm text-sky-800">{t("technologies:shop.outsideClan")}</p>}

      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {showcase === "free"
          ? view.free.items.map((item) => {
              const left = item.limit - item.claimed;
              const locked = item.requiresClan && !view.inClan;

              return (
                <ShopCard key={item.itemKey} itemKey={item.itemKey} countLine={t("technologies:shop.left", { left, limit: item.limit })}>
                  {locked ? (
                    <span className="text-xs text-slate-500">{t("technologies:shop.clanOnly")}</span>
                  ) : (
                    <>
                      {left > 1 && (
                        <button
                          type="button"
                          disabled={busy}
                          onClick={() => claim.mutate({ itemKey: item.itemKey, quantity: left })}
                          className="text-sm text-sky-700 hover:underline disabled:opacity-50"
                        >
                          {t("technologies:shop.claimAll", { count: left })}
                        </button>
                      )}
                      <button
                        type="button"
                        disabled={busy || left <= 0}
                        onClick={() => claim.mutate({ itemKey: item.itemKey, quantity: 1 })}
                        className={button}
                      >
                        {t("technologies:shop.claim")}
                      </button>
                    </>
                  )}
                </ShopCard>
              );
            })
          : view[showcase].offers.map((offer) => (
              <PaidCard
                key={offer.itemKey}
                offer={offer}
                affordable={view.coins >= offer.price}
                busy={busy}
                onBuy={() => buy.mutate({ kind: KIND[showcase], itemKey: offer.itemKey, quantity: 1, expectedPrice: offer.price })}
              />
            ))}
      </div>
    </div>
  );
}

function PaidCard({ offer, affordable, busy, onBuy }: { offer: ClanShopOfferView; affordable: boolean; busy: boolean; onBuy: () => void }) {
  const left = offer.limit - offer.purchased;

  return (
    <ShopCard itemKey={offer.itemKey} countLine={t("technologies:shop.left", { left, limit: offer.limit })}>
      <span className="text-sm">
        {offer.discountPercent > 0 && (
          <>
            <span className="mr-1 rounded bg-rose-600 px-1 text-xs font-semibold text-white">−{offer.discountPercent}%</span>
            <s className="mr-1 text-slate-400">{formatNumber(offer.basePrice)}</s>
          </>
        )}
        <span className="font-semibold text-amber-700">🪙 {formatNumber(offer.price)}</span>
      </span>
      <button type="button" disabled={busy || left <= 0 || !affordable} onClick={onBuy} className={button}>
        {t("technologies:shop.buy")}
      </button>
    </ShopCard>
  );
}

function ShopCard({ itemKey, countLine, children }: { itemKey: string; countLine: string; children: ReactNode }) {
  const catalog = useCatalog();
  const item = catalog.item(itemKey);

  return (
    <div className="flex gap-3 rounded-xl border border-slate-200 bg-white p-3">
      <ItemIcon itemKey={itemKey} type={item?.type ?? ""} rarity={item?.rarity} size={48} />
      <div className="min-w-0 flex-1 space-y-1">
        <p className="truncate font-medium text-slate-800" title={item?.description}>
          {catalog.itemName(itemKey)}
        </p>
        <p className="text-xs text-slate-500">{countLine}</p>
        <div className="flex flex-wrap items-center justify-end gap-2">{children}</div>
      </div>
    </div>
  );
}
