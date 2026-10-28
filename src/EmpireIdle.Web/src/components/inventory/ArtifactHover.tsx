import { useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { useCatalog } from "../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { statLabel, statValue } from "../../lib/statNames";
import ArtifactSetInfo from "./ArtifactSetInfo";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

/** Що відомо про артефакт: екземпляр має рівень і роллені стати, нагорода данжу — лише ключ і рідкість. */
export interface HoveredArtifact {
  itemKey: string;
  rarity: string;
  level?: number;
  power?: number;
  stats?: Record<string, number>;
  isBroken?: boolean;
}

interface Props {
  artifact: HoveredArtifact;
  /** Ключі предметів, вдягнених на того самого героя, — для лічильника набору; порожньо — ні на кому. */
  wornKeys?: string[];
  children: ReactNode;
}

const WIDTH = 288;
const GAP = 8;

/**
 * Підказка при наведенні на артефакт: назва, рідкість, рівень, сила, показники й бонус набору. Малюється
 * порталом поверх сторінки — сітки й модалки з overflow її не обрізають; стає праворуч від плитки, а біля
 * правого краю — ліворуч.
 */
export default function ArtifactHover({ artifact, wornKeys = [], children }: Props) {
  const catalog = useCatalog();
  const [anchor, setAnchor] = useState<DOMRect | null>(null);

  const left = anchor === null ? 0 : anchor.right + GAP + WIDTH <= window.innerWidth ? anchor.right + GAP : Math.max(GAP, anchor.left - GAP - WIDTH);
  const top = anchor === null ? 0 : Math.max(GAP, Math.min(anchor.top, window.innerHeight - 420));
  const stats = Object.entries(artifact.stats ?? {});

  return (
    <div onMouseEnter={(event) => setAnchor(event.currentTarget.getBoundingClientRect())} onMouseLeave={() => setAnchor(null)}>
      {children}
      {anchor !== null &&
        createPortal(
          <div
            role="tooltip"
            className="pointer-events-none fixed z-[60] max-h-[80vh] space-y-2 overflow-hidden rounded-xl bg-white p-3 text-slate-800 shadow-2xl ring-2 ring-sky-300"
            style={{ left, top, width: WIDTH }}
          >
            <div>
              <p className="font-semibold">{catalog.itemName(artifact.itemKey)}</p>
              <div className="flex flex-wrap items-center gap-2 text-xs">
                <span className={`rounded px-1.5 py-0.5 ${rarityStyle(artifact.rarity)}`}>{rarityLabel(artifact.rarity)}</span>
                {artifact.level !== undefined && <span className="text-slate-600">{t("items:artifact.level", { level: artifact.level })}</span>}
                {artifact.power !== undefined && <span className="text-slate-600">⚔ {formatNumber(Math.round(artifact.power))}</span>}
                {artifact.isBroken === true && <span className="rounded bg-rose-100 px-1.5 text-rose-700">{t("items:artifact.broken")}</span>}
              </div>
            </div>

            {stats.length > 0 && (
              <dl className="divide-y divide-slate-100 rounded-lg bg-slate-50 text-sm">
                {stats.map(([stat, value]) => (
                  <div key={stat} className="flex justify-between px-2 py-1">
                    <dt className="text-slate-600">{statLabel(stat)}</dt>
                    <dd className="font-semibold tabular-nums">{statValue(stat, value)}</dd>
                  </div>
                ))}
              </dl>
            )}

            <ArtifactSetInfo itemKey={artifact.itemKey} wornKeys={wornKeys} />
          </div>,
          document.body,
        )}
    </div>
  );
}
