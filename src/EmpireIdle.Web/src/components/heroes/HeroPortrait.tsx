import type { ReactElement } from "react";
import { heroArtUrl } from "./heroArt";
import { portraitFor } from "./heroSprites";
import { t } from "i18next";

/**
 * Портрет героя: спрайт із маніфесту або процедурний (heroArt) — у кожного героя свої риси, клас
 * видно з тла й значка, ранг — з рамки, тір — з оздоблення. Коли з'явиться арт, міняється лише маніфест.
 */
interface Props {
  heroKey: string;
  /** Клас із каталогу: warrior, knight, archer. Невідомий — без значка класу. */
  heroClass: string | undefined;
  /** Ранг із каталогу: Common, Rare, Unique. */
  rank: string | undefined;
  tier: number;
  /** Сторона квадрата в пікселях. */
  size?: number;
  className?: string;
}

const RANK_GLOW: Record<string, string> = {
  Rare: "rgba(56, 189, 248, 0.35)",
  Unique: "rgba(245, 158, 11, 0.45)",
};

export default function HeroPortrait({ heroKey, heroClass, rank, tier, size = 56, className = "" }: Props): ReactElement {
  const sprite = portraitFor(heroKey, tier);
  const glow = RANK_GLOW[rank ?? ""] ?? "transparent";

  return (
    <span className={`relative inline-block shrink-0 ${className}`} style={{ width: size, height: size }}>
      <img
        src={sprite ?? heroArtUrl(heroKey, heroClass, rank, tier)}
        alt=""
        width={size}
        height={size}
        className="rounded-lg object-cover"
        style={{ boxShadow: `0 0 0 3px ${glow}` }}
      />
      {/* Тір вище першого — значком у куті, щоб героя на сітці було видно без відкриття картки */}
      {tier > 1 && (
        <span
          className="absolute -right-1 -top-1 rounded bg-amber-400 px-1 text-[10px] font-bold leading-tight text-slate-900 ring-1 ring-amber-700"
          title={t("heroes:portrait.tier", { tier })}
        >
          T{tier}
        </span>
      )}
    </span>
  );
}
