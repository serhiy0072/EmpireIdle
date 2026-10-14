import type { BannerDropResult } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { rarityKey } from "../../lib/rarity";
import ItemIcon from "../inventory/ItemIcon";
import { RewardsWindow, type DropsAgain } from "../lootboxes/LootboxPanels";

const FRAMES: Record<string, string> = {
  Common: "ring-white/30",
  Rare: "ring-sky-400",
  Unique: "ring-amber-400",
};

/** Плитка випаду банера: предмет — його значком, осколки й герої — пазлом; рамка — рідкість лота. */
function BannerTile({ drop }: { drop: BannerDropResult }) {
  const catalog = useCatalog();
  // Лот предмета в конфігу банера — «loot_<ключ предмета>»: значок береться з самого предмета
  const item = catalog.item(drop.dropKey.replace(/^loot_/, ""));
  const frame = FRAMES[rarityKey(drop.rarity)] ?? FRAMES.Common;

  return (
    <div title={drop.displayName} className="relative flex w-20 flex-col items-center gap-1">
      <div className={`relative rounded-xl ring-2 ${frame}`}>
        {item !== null ? (
          <ItemIcon itemKey={item.key} type={item.type} rarity={item.rarity} size={64} />
        ) : (
          <div className="flex h-16 w-16 items-center justify-center rounded-xl bg-slate-100 text-3xl">
            {drop.dropKey.startsWith("hero_") ? "🧩" : "🎁"}
          </div>
        )}
        {drop.wasPity && <span className="absolute -left-1 -top-1 text-sm">★</span>}
      </div>
      <span className="line-clamp-2 text-center text-[11px] leading-tight text-sky-50">{drop.displayName}</span>
    </div>
  );
}

interface Props {
  drops: BannerDropResult[];
  onClose: () => void;
  again?: DropsAgain;
  pending?: boolean;
}

/** Що випало з банера — те саме вікно «Нагороди», що й у скринь: гравець бачить результат там, де клікнув. */
export default function BannerDrops({ drops, onClose, again, pending }: Props) {
  return (
    <RewardsWindow onClose={onClose} again={again} pending={pending}>
      {drops.map((drop, index) => (
        <BannerTile key={index} drop={drop} />
      ))}
    </RewardsWindow>
  );
}
