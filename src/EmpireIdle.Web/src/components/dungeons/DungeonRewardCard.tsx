import type { ReactNode } from "react";
import type { DungeonRunView } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { Tile } from "../game/GameUi";
import ItemIcon from "../inventory/ItemIcon";

type Reward = NonNullable<DungeonRunView["reward"]>;

/** Значок ресурсу на плитці; невідомий ресурс — скриня. */
const RESOURCE_GLYPHS: Record<string, string> = { gold: "🪙", wood: "🪵", iron: "⛓️", food: "🌾" };

/** Плитка нагороди з підписом під нею — плитки однакові, тож назву тримаємо окремо. */
function RewardSlot({ caption, children }: { caption: string; children: ReactNode }) {
  return (
    <div className="flex w-20 flex-col items-center gap-1 text-center">
      {children}
      <span className="line-clamp-2 text-xs leading-tight text-slate-700">{caption}</span>
    </div>
  );
}

/** Нагорода зачищеного данжу: ресурси, досвід героїв і артефакт — плитками, як у рюкзаку. */
export default function DungeonRewardCard({ reward }: { reward: Reward }) {
  const catalog = useCatalog();

  return (
    <div className="overflow-hidden rounded-xl ring-1 ring-amber-300">
      <div className="bg-gradient-to-r from-amber-400 to-orange-500 px-3 py-1.5 text-sm font-bold text-white [text-shadow:0_1px_2px_#0006]">
        🏆 Нагорода
      </div>

      <div className="flex flex-wrap justify-center gap-3 bg-gradient-to-b from-amber-50 to-white p-3">
        {reward.resources.map((line) => (
          <RewardSlot key={line.resource} caption={catalog.resourceName(line.resource)}>
            <Tile rarity="Common" bottomRight={`+${line.amount.toLocaleString("uk-UA")}`}>
              <span className="text-3xl" aria-hidden>
                {RESOURCE_GLYPHS[line.resource] ?? "📦"}
              </span>
            </Tile>
          </RewardSlot>
        ))}

        {reward.heroExperience > 0 && (
          <RewardSlot caption="Досвід героїв">
            <Tile rarity="Rare" bottomRight={`+${reward.heroExperience.toLocaleString("uk-UA")}`}>
              <span className="text-3xl" aria-hidden>
                ⭐
              </span>
            </Tile>
          </RewardSlot>
        )}

        {reward.artifacts.map((artifact) => (
          <RewardSlot key={artifact.itemKey} caption={artifact.displayName}>
            <Tile rarity={artifact.rarity}>
              <ItemIcon itemKey={artifact.itemKey} type="equipment" rarity={artifact.rarity} size={56} bare />
            </Tile>
          </RewardSlot>
        ))}
      </div>
    </div>
  );
}
