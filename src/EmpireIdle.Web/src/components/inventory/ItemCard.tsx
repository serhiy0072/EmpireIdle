import { useState } from "react";
import { Link } from "react-router-dom";
import type { InventoryItemResponse, UseItemRequest } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import GiftPanel from "./GiftPanel";
import ItemIcon from "./ItemIcon";
import { t } from "i18next";

interface Props {
  playerId: string;
  item: InventoryItemResponse;
  busy: boolean;
  onUse: (request: UseItemRequest) => void;
}

const TYPES = ["resources", "boost", "teleport", "evolution", "feed", "scoutveil", "energy", "heroxp", "chestkey"] as const;

function typeLabel(type: string): string {
  return (TYPES as readonly string[]).includes(type) ? t(`items:type.${type as (typeof TYPES)[number]}`) : type;
}

/**
 * Стаковий предмет. Ящики й бусти вживаються відразу, телепорт веде на мапу —
 * місце обирають там, есенції еволюції витрачає сам герой — їм кнопки не треба.
 */
export default function ItemCard({ playerId, item, busy, onUse }: Props) {
  const catalog = useCatalog();
  const [count, setCount] = useState(1);
  const [confirming, setConfirming] = useState(false);

  const giftable = catalog.item(item.itemKey)?.giftable === true;

  // Випадковий і до лідера клітини не потребують — гра обирає її сама (GDD §8.9)
  const teleportScope = item.type === "teleport" ? catalog.item(item.itemKey)?.teleportScope : undefined;
  const gamePicksCell = teleportScope === "Random" || teleportScope === "ClanLeader";

  const usable = item.type === "resources" || item.type === "boost" || item.type === "scoutveil" || item.type === "energy" || item.type === "heroxp";
  // «Усі» — весь стек: понад стелю сервера useUseItem шле кількома запитами
  const limit = item.count;
  const safeCount = Math.min(limit, Math.max(1, count));

  const use = () => onUse({ itemKey: item.itemKey, count: safeCount, targetX: null, targetY: null });

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex gap-3">
        <ItemIcon itemKey={item.itemKey} type={item.type} rarity={item.rarity} size={48} />

        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-medium text-slate-800">{item.displayName}</span>
            <span className="text-sm text-slate-600">×{item.count}</span>
          </div>

          <div className="mt-1 flex items-center gap-1 text-xs">
            <span className={`rounded px-2 py-0.5 ${rarityStyle(item.rarity)}`}>{rarityLabel(item.rarity)}</span>
            <span className="text-slate-500">{typeLabel(item.type)}</span>
          </div>
        </div>
      </div>

      {item.description !== "" && <p className="mt-2 text-sm text-slate-600">{item.description}</p>}

      {usable && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {limit > 1 && (
            <>
              <input
                type="number"
                min={1}
                max={limit}
                value={safeCount}
                onChange={(event) => setCount(Number(event.target.value))}
                className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-sm"
              />
              <button type="button" onClick={() => setCount(limit)} className="text-sm text-sky-700 hover:underline">
                {t("items:item.all")}
              </button>
            </>
          )}

          <button
            type="button"
            onClick={use}
            disabled={busy}
            className="ml-auto rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {t("items:item.use")}
          </button>
        </div>
      )}

      {item.type === "teleport" && (
        <div className="mt-3 flex flex-wrap justify-end gap-2">
          {giftable && <GiftPanel playerId={playerId} item={item} />}
          {gamePicksCell ? (
            confirming ? (
              // Переїзд не скасувати, а місце обирає гра — тож другий клік, а не випадковий перший
              <div className="flex w-full flex-wrap items-center justify-end gap-2 text-sm">
                <span className="text-slate-600">{t("items:item.teleportConfirm")}</span>
                <button
                  type="button"
                  onClick={() => {
                    setConfirming(false);
                    onUse({ itemKey: item.itemKey, count: 1, targetX: null, targetY: null });
                  }}
                  disabled={busy}
                  className="rounded-lg bg-emerald-600 px-3 py-1 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  {t("items:item.yes")}
                </button>
                <button
                  type="button"
                  onClick={() => setConfirming(false)}
                  className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50"
                >
                  {t("items:item.no")}
                </button>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => setConfirming(true)}
                disabled={busy}
                className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
              >
                {teleportScope === "ClanLeader" ? t("items:item.teleportLeader") : t("items:item.teleportRandom")}
              </button>
            )
          ) : (
            <Link
              to={`/map?teleport=${encodeURIComponent(item.itemKey)}`}
              className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700"
            >
              {t("items:item.pickOnMap")}
            </Link>
          )}
        </div>
      )}

      {item.type === "evolution" && (
        <p className="mt-2 text-xs text-slate-500">{t("items:item.evolutionHint")}</p>
      )}

      {item.type === "feed" && (
        <Link to="/beasts" className="mt-2 block text-xs text-emerald-700 hover:underline">
          {t("items:item.feedHint")}
        </Link>
      )}
    </div>
  );
}
