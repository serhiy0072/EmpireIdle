import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import ListingCard from "../components/market/ListingCard";
import SellPanel from "../components/market/SellPanel";
import { useSession } from "../hooks/useSession";
import {
  MARKET_KIND,
  PAGE_SIZE,
  useBuyListing,
  useCancelListing,
  useMarketListings,
  useMyMarket,
  type MarketKind,
} from "../lib/queries/market";

type Tab = "browse" | "mine" | "sell";

const TABS: { key: Tab; label: string }[] = [
  { key: "browse", label: "Вітрина" },
  { key: "mine", label: "Мої лоти" },
  { key: "sell", label: "Продати" },
];

const KINDS: { kind: MarketKind | null; label: string }[] = [
  { kind: null, label: "Усе" },
  { kind: MARKET_KIND.Equipment, label: "Спорядження" },
  { kind: MARKET_KIND.Hero, label: "Герої" },
  { kind: MARKET_KIND.Item, label: "Предмети" },
];

const pill = (active: boolean) =>
  `rounded-lg px-3 py-1 text-sm ${active ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`;

/**
 * Ринок гравців (GDD §8.8): вітрина, власні лоти й виставлення.
 * Ціни — у золоті, фіксовані; коридор ціни задає сервер.
 */
export default function MarketPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const [params, setParams] = useSearchParams();
  const tab = (TABS.find((t) => t.key === params.get("tab"))?.key ?? "browse") as Tab;
  const setTab = (next: Tab) => setParams({ tab: next }, { replace: true });

  const [kind, setKind] = useState<MarketKind | null>(null);
  const [page, setPage] = useState(1);

  const my = useMyMarket(playerId);
  const listings = useMarketListings(playerId, kind, page);
  const buy = useBuyListing(playerId);
  const cancel = useCancelListing(playerId);

  if (my.isPending) {
    return <p className="text-slate-500">Завантаження ринку…</p>;
  }

  if (my.isError) {
    return <ErrorBanner error={my.error} />;
  }

  const market = my.data;

  if (!market.isOpen) {
    return (
      <div className="space-y-2">
        <h1 className="text-xl font-medium text-slate-800">Ринок</h1>
        <p className="text-sm text-slate-600">
          {market.opensAtTownHall != null
            ? `Ринок відкриється, коли ратуша досягне ${market.opensAtTownHall} рівня.`
            : "У цьому світі ринку немає."}
        </p>
      </div>
    );
  }

  const pages = Math.max(1, Math.ceil((listings.data?.total ?? 0) / PAGE_SIZE));
  const busy = buy.isPending || cancel.isPending;
  const canList = market.activeListings < market.listingLimit;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Ринок</h1>
        <p className="text-sm text-slate-500">
          Лотів {market.activeListings}/{market.listingLimit} · податок {Math.round(market.taxShare * 100)}% · строк{" "}
          {market.listingHours} год
        </p>
      </div>

      <nav className="flex flex-wrap gap-1">
        {TABS.map((item) => (
          <button key={item.key} type="button" onClick={() => setTab(item.key)} className={pill(tab === item.key)}>
            {item.label}
          </button>
        ))}
      </nav>

      <ErrorBanner error={buy.error ?? cancel.error} />

      {tab === "browse" && (
        <section className="space-y-3">
          <div className="flex flex-wrap gap-1">
            {KINDS.map((item) => (
              <button
                key={item.label}
                type="button"
                onClick={() => {
                  setKind(item.kind);
                  setPage(1);
                }}
                className={pill(kind === item.kind)}
              >
                {item.label}
              </button>
            ))}
          </div>

          {listings.isError && <ErrorBanner error={listings.error} />}

          {listings.data?.listings.length === 0 ? (
            <p className="text-sm text-slate-500">Поки ніхто нічого не продає.</p>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {listings.data?.listings.map((listing) => (
                <ListingCard
                  key={listing.id}
                  listing={listing}
                  action={
                    listing.isOwn ? (
                      <span className="text-xs text-slate-400">ваш лот</span>
                    ) : (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => buy.mutate(listing.id)}
                        className="rounded-lg bg-emerald-600 px-3 py-1 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                      >
                        Купити
                      </button>
                    )
                  }
                />
              ))}
            </div>
          )}

          {pages > 1 && (
            <div className="flex items-center gap-2 text-sm">
              <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} className={pill(false)}>
                ←
              </button>
              <span className="text-slate-500">
                {page} / {pages}
              </span>
              <button type="button" disabled={page >= pages} onClick={() => setPage(page + 1)} className={pill(false)}>
                →
              </button>
            </div>
          )}
        </section>
      )}

      {tab === "mine" && (
        <section className="space-y-3">
          {market.listings.length === 0 ? (
            <p className="text-sm text-slate-500">У вас немає лотів.</p>
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {market.listings.map((listing) => (
                <ListingCard
                  key={listing.id}
                  listing={listing}
                  action={
                    listing.state === "Active" && (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => cancel.mutate(listing.id)}
                        className="rounded-lg border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        Зняти
                      </button>
                    )
                  }
                />
              ))}
            </div>
          )}
        </section>
      )}

      {tab === "sell" && <SellPanel playerId={playerId} canList={canList} onListed={() => setTab("mine")} />}
    </div>
  );
}
