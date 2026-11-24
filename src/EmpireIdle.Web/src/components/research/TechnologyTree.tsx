import { useLayoutEffect, useMemo, useRef, useState } from "react";
import type { TechnologyStat } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { statIcon, technologyName, tierNumeral } from "../../lib/technologies";
import { t } from "i18next";

/** Вузол дерева, як його віддають і особисті, і кланові технології. */
export interface TreeNode {
  tier: number;
  node: string;
  tab: string;
  row: number;
  requires: string[];
  level: number;
  maxLevel: number;
  availability: string;
  effects: { stat: TechnologyStat }[];
}

interface TierTreeProps<TNode extends TreeNode> {
  nodes: TNode[];
  tier: number;
  tab: string;
  open: boolean;
  /** Підпис біля замкненого тіру; без нього — лише замок. */
  lockedHint?: string;
  /** Вузол, що зараз досліджується: плитка пульсує. */
  activeKey?: string | null;
  /** Вузол із міткою керівництва клану: на плитці зірка. */
  markedKey?: string | null;
  onPick: (node: TNode) => void;
}

/** Лінія від передумови до вузла в координатах контейнера тіру. */
interface Link {
  key: string;
  path: string;
  /** Передумову вивчено хоча б на рівень — вузол уже не чекає на неї. */
  met: boolean;
}

/**
 * Лінії передумов: від низу плитки передумови (під підписом) до верху плитки вузла, ламаною «деревом».
 * Позиції міряються після розкладки, бо ряди переносяться flex-wrap і залежать від ширини.
 */
function useLinks(nodes: TreeNode[]) {
  const container = useRef<HTMLDivElement>(null);
  const tiles = useRef(new Map<string, HTMLElement>());
  const [links, setLinks] = useState<Link[]>([]);

  useLayoutEffect(() => {
    const box = container.current;
    if (box === null) return;

    const measure = () => {
      const origin = box.getBoundingClientRect();
      const levels = new Map(nodes.map((node) => [node.node, node.level]));
      const next: Link[] = [];

      for (const node of nodes) {
        const to = tiles.current.get(node.node)?.getBoundingClientRect();
        if (to === undefined) continue;

        for (const required of node.requires) {
          const from = tiles.current.get(required)?.getBoundingClientRect();
          if (from === undefined) continue;

          const x1 = from.left + from.width / 2 - origin.left;
          const y1 = from.bottom - origin.top;
          const x2 = to.left + to.width / 2 - origin.left;
          const y2 = to.top - origin.top;
          const mid = (y1 + y2) / 2;

          next.push({
            key: `${required}>${node.node}`,
            path: `M${x1} ${y1} V${mid} H${x2} V${y2}`,
            met: (levels.get(required) ?? 0) >= 1,
          });
        }
      }

      setLinks(next);
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(box);
    return () => observer.disconnect();
  }, [nodes]);

  const register = (key: string) => (element: HTMLElement | null) => {
    if (element === null) tiles.current.delete(key);
    else tiles.current.set(key, element);
  };

  return { container, links, register };
}

/**
 * Один тір вкладки: ряди вузлів згори вниз, у ряду — поруч, передумови з'єднані лініями.
 * Малюється на темно-синьому тлі дерева, тож текст білий.
 */
export function TierTree<TNode extends TreeNode>({ nodes: all, tier, tab, open, lockedHint, activeKey, markedKey, onPick }: TierTreeProps<TNode>) {
  const nodes = useMemo(() => all.filter((node) => node.tier === tier && node.tab === tab), [all, tier, tab]);
  const rows = [...new Set(nodes.map((node) => node.row))].sort((a, b) => a - b);
  const { container, links, register } = useLinks(nodes);

  if (nodes.length === 0) return null;

  return (
    <section className={open ? "" : "opacity-60"}>
      <div className="mb-3 flex items-center gap-2">
        <span className="h-px flex-1 bg-sky-300/40" />
        <span className="text-sm font-semibold">{t("technologies:page.tier", { tier: tierNumeral(tier) })}</span>
        {!open && <span className="text-xs text-white/70">🔒 {lockedHint}</span>}
        <span className="h-px flex-1 bg-sky-300/40" />
      </div>
      <div ref={container} className="relative space-y-6">
        <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible" aria-hidden>
          {links.map((link) => (
            <path
              key={link.key}
              d={link.path}
              fill="none"
              stroke={link.met ? "#7dd3fc" : "#94a3b8"}
              strokeWidth={3}
              strokeLinejoin="round"
              strokeDasharray={link.met ? undefined : "6 4"}
              opacity={link.met ? 0.95 : 0.75}
            />
          ))}
        </svg>
        {rows.map((row) => (
          <div key={row} className="relative flex flex-wrap justify-center gap-4">
            {nodes
              .filter((node) => node.row === row)
              .map((node) => (
                <NodeTile
                  key={node.node}
                  node={node}
                  active={activeKey === `${node.tier}:${node.node}`}
                  marked={markedKey === `${node.tier}:${node.node}`}
                  tileRef={register(node.node)}
                  onClick={() => onPick(node)}
                />
              ))}
          </div>
        ))}
      </div>
    </section>
  );
}

interface NodeTileProps {
  node: TreeNode;
  active: boolean;
  marked: boolean;
  /** Плитка — точка кріплення ліній передумов. */
  tileRef: (element: HTMLElement | null) => void;
  onClick: () => void;
}

function NodeTile({ node, active, marked, tileRef, onClick }: NodeTileProps) {
  const maxed = node.level >= node.maxLevel;
  const available = node.availability === "Available";

  return (
    <button ref={tileRef} type="button" onClick={onClick} className="flex w-24 flex-col items-center gap-1 text-center">
      <span
        className={`relative flex h-16 w-16 items-center justify-center rounded-2xl text-2xl shadow ring-2 ${
          maxed
            ? "bg-gradient-to-b from-amber-300 to-orange-500 ring-amber-200"
            : available
              ? "bg-gradient-to-b from-sky-300 to-blue-600 ring-sky-200"
              : "bg-gradient-to-b from-slate-400 to-slate-600 ring-slate-300 grayscale"
        } ${active ? "animate-pulse" : ""} ${marked ? "outline-3 outline-offset-2 outline-yellow-300" : ""}`}
      >
        {iconFor(node)}
        {marked && (
          <span className="absolute -top-2 -right-2 text-base" title={t("technologies:clan.markedTitle")}>
            ⭐
          </span>
        )}
        <span className="absolute -bottom-2 rounded-full bg-[#16305e] px-1.5 text-[10px] font-bold ring-1 ring-white/40">
          {maxed ? t("technologies:page.max") : `${formatNumber(node.level)}/${formatNumber(node.maxLevel)}`}
        </span>
      </span>
      <span className="mt-1 text-xs leading-tight">
        {technologyName(node.node)} {tierNumeral(node.tier)}
      </span>
    </button>
  );
}

/** Значок за першим ефектом вузла. */
function iconFor(node: TreeNode): string {
  return statIcon(node.effects[0]?.stat);
}
