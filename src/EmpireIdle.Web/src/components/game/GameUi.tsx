import type { ReactNode } from "react";
import { rarityKey } from "../../lib/rarity";

/**
 * Ігрова «шкірка» екранів героя, рюкзака й табору (рішення 08.10.2026): темно-синє тло,
 * плитки з рамкою рідкості, великі вкладки. Решта застосунку поки світла — переходить поступово.
 */

/** Темно-синій екран із заголовком; дії праворуч від назви. */
export function GameScreen({ title, actions, children }: { title: ReactNode; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="overflow-hidden rounded-2xl bg-gradient-to-b from-[#24467f] to-[#16305e] text-white shadow-lg">
      <header className="flex items-center justify-between gap-3 px-4 pt-3">
        <h1 className="text-lg font-semibold tracking-wide drop-shadow">{title}</h1>
        {actions}
      </header>
      <div className="p-3">{children}</div>
    </section>
  );
}

export interface GameTab<T extends string> {
  key: T;
  label: string;
  /** Червона крапка — тут є що зробити. */
  alert?: boolean;
}

/** Вкладки «картками», як у грі: активна світла й трохи вища. */
export function GameTabs<T extends string>({
  tabs,
  value,
  onChange,
}: {
  tabs: GameTab<T>[];
  value: T;
  onChange: (next: T) => void;
}) {
  return (
    <nav className="flex gap-1">
      {tabs.map((tab) => {
        const active = tab.key === value;

        return (
          <button
            key={tab.key}
            type="button"
            onClick={() => onChange(tab.key)}
            className={`relative flex-1 rounded-t-xl px-2 text-sm font-semibold transition ${
              active
                ? "bg-[#e8eef8] py-2.5 text-[#1d3b6f]"
                : "mt-1 bg-[#5b85c4] py-2 text-white hover:bg-[#6a93d0]"
            }`}
          >
            {tab.label}
            {tab.alert === true && <span className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full bg-rose-500" />}
          </button>
        );
      })}
    </nav>
  );
}

/** Панель під вкладками — продовження активної вкладки. */
export function GamePanel({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`rounded-b-xl rounded-tr-xl bg-[#1d3b6f] p-3 ring-1 ring-[#335c9a] ${className}`}>{children}</div>;
}

const TILE_FRAMES: Record<string, string> = {
  Common: "from-slate-400 to-slate-600 ring-slate-300",
  Rare: "from-sky-500 to-blue-700 ring-sky-300",
  Unique: "from-amber-400 to-orange-600 ring-amber-200",
};

interface TileProps {
  rarity: string | number | undefined;
  children: ReactNode;
  /** Число в правому нижньому куті — кількість у стеку. */
  count?: number;
  /** Напис угорі по центру (тривалість прискорення, «+заточка»). */
  top?: ReactNode;
  /** Значок у лівому верхньому куті (роль). */
  corner?: ReactNode;
  /** Низ по центру (зірки героя). */
  bottom?: ReactNode;
  /** Лівий нижній кут (міні-портрет носія). */
  bottomLeft?: ReactNode;
  /** Правий нижній кут замість кількості (рівень). */
  bottomRight?: ReactNode;
  selected?: boolean;
  dimmed?: boolean;
  onClick?: () => void;
  title?: string;
  /** Якір навчання (data-tutorial): туторіал підсвічує саме цю плитку. */
  tutorial?: string;
}

/** Квадратна плитка предмета чи героя з рамкою кольору рідкості. */
export function Tile({ rarity, children, count, top, corner, bottom, bottomLeft, bottomRight, selected, dimmed, onClick, title, tutorial }: TileProps) {
  const frame = TILE_FRAMES[rarityKey(rarity)] ?? TILE_FRAMES.Common;

  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      data-tutorial={tutorial}
      className={`relative aspect-square w-full overflow-hidden rounded-xl bg-gradient-to-b p-1 ring-2 transition ${frame} ${
        selected === true ? "outline outline-3 outline-offset-2 outline-yellow-300" : ""
      } ${dimmed === true ? "opacity-50" : "hover:brightness-110"}`}
    >
      <span className="flex h-full w-full items-center justify-center">{children}</span>
      {top !== undefined && (
        <span className="absolute inset-x-0 top-0.5 text-center text-xs font-bold text-white [text-shadow:0_1px_2px_#000]">
          {top}
        </span>
      )}
      {corner !== undefined && <span className="absolute left-0.5 top-0.5">{corner}</span>}
      {bottom !== undefined && <span className="absolute inset-x-0 bottom-0.5 flex justify-center">{bottom}</span>}
      {bottomLeft !== undefined && <span className="absolute bottom-0.5 left-0.5">{bottomLeft}</span>}
      {(bottomRight !== undefined || count !== undefined) && (
        <span className="absolute bottom-0.5 right-1 text-xs font-bold text-white [text-shadow:0_1px_2px_#000]">
          {bottomRight ?? count?.toLocaleString("uk-UA")}
        </span>
      )}
    </button>
  );
}

/** Сітка плиток: 4 в ряд на телефоні, більше на ширшому екрані. */
export function TileGrid({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-4 gap-2.5 sm:grid-cols-6 lg:grid-cols-8">{children}</div>;
}

const ROLE_GLYPHS: Record<string, ReactNode> = {
  // Воїн — схрещені мечі
  warrior: (
    <path d="M7 7 L17 17 M17 7 L7 17 M6 9 L9 6 M15 18 L18 15 M18 9 L15 6 M9 18 L6 15" stroke="#fff" strokeWidth={2} strokeLinecap="round" />
  ),
  // Лицар — щит
  knight: <path d="M12 5 L18 7.5 L17 13 Q15.5 17 12 19 Q8.5 17 7 13 L6 7.5 Z" fill="none" stroke="#fff" strokeWidth={2} strokeLinejoin="round" />,
  // Лучник — лук і стріла
  archer: (
    <g stroke="#fff" strokeWidth={2} strokeLinecap="round" fill="none">
      <path d="M8 5 Q18 12 8 19" />
      <path d="M6 12 L18 12 M15 9.5 L18 12 L15 14.5" />
    </g>
  ),
};

const ROLE_BACKS: Record<string, string> = {
  warrior: "#c2410c",
  knight: "#1d4ed8",
  archer: "#15803d",
};

const ROLE_NAMES: Record<string, string> = { warrior: "Воїн · піхота", knight: "Лицар · кіннота", archer: "Лучник · стрільці" };

/** Значок ролі героя на щитку — той самий, що на плитках героїв і спорядження. */
export function RoleBadge({ role, size = 20 }: { role: string | undefined; size?: number }) {
  if (role === undefined) return null;

  return (
    <svg viewBox="0 0 24 24" width={size} height={size} aria-label={ROLE_NAMES[role] ?? role} className="drop-shadow">
      <title>{ROLE_NAMES[role] ?? role}</title>
      <path d="M12 1.5 L21.5 5 L20.5 14 Q18 20 12 22.5 Q6 20 3.5 14 L2.5 5 Z" fill={ROLE_BACKS[role] ?? "#475569"} stroke="#fff" strokeWidth={1.2} />
      {ROLE_GLYPHS[role]}
    </svg>
  );
}

/** Точка на колі навколо центру зірки (12, 12); кут від верху за годинниковою стрілкою. */
function polar(radius: number, degrees: number): string {
  const radians = ((degrees - 90) * Math.PI) / 180;
  return `${(12 + radius * Math.cos(radians)).toFixed(2)} ${(12 + radius * Math.sin(radians)).toFixed(2)}`;
}

/**
 * Шестипроменева зірка, де кожен промінь — частинка (GDD §6.1: зірка з шести частинок).
 * Промені заповнюються за годинниковою стрілкою від верхнього; кількість променів — partsPerStar.
 */
function HeroStar({ filled, parts, size }: { filled: number; parts: number; size: number }) {
  const step = 360 / parts;
  const ray = (index: number) => {
    const angle = index * step;
    return `M12 12 L${polar(4.6, angle - step / 2)} L${polar(11, angle)} L${polar(4.6, angle + step / 2)} Z`;
  };

  return (
    <svg viewBox="0 0 24 24" width={size} height={size} aria-hidden className="drop-shadow">
      {Array.from({ length: parts }, (_, index) => (
        <path
          key={index}
          d={ray(index)}
          fill={index < filled ? "#5eead4" : "#1e293b"}
          stroke={filled > 0 ? "#ccfbf1" : "#64748b"}
          strokeWidth={0.8}
          strokeLinejoin="round"
        />
      ))}
    </svg>
  );
}

/** П'ять зірок героя (GDD §6.1): заповнені промені — бірюзові, неповна зірка показує частинки. */
export function StarRow({ starParts, partsPerStar, maxStars, size = 28 }: { starParts: number; partsPerStar: number; maxStars: number; size?: number }) {
  return (
    <div className={`flex items-center ${size < 16 ? "gap-px" : "gap-1.5"}`} title={`Зірки: ${Math.floor(starParts / partsPerStar)} з ${maxStars}`}>
      {Array.from({ length: maxStars }, (_, index) => (
        <HeroStar
          key={index}
          filled={Math.min(partsPerStar, Math.max(0, starParts - index * partsPerStar))}
          parts={partsPerStar}
          size={size}
        />
      ))}
    </div>
  );
}

/** Світла кнопка дії на темному тлі; primary — синя, як «Зняти все». */
export function GameButton({
  children,
  onClick,
  disabled,
  variant = "primary",
  title,
}: {
  children: ReactNode;
  onClick: () => void;
  disabled?: boolean;
  variant?: "primary" | "secondary" | "accent";
  title?: string;
}) {
  const styles = {
    primary: "bg-gradient-to-b from-sky-400 to-blue-600 ring-sky-200",
    secondary: "bg-gradient-to-b from-slate-500 to-slate-700 ring-slate-300",
    accent: "bg-gradient-to-b from-amber-400 to-orange-600 ring-amber-200",
  } as const;

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      className={`rounded-xl px-4 py-2 text-sm font-semibold text-white shadow ring-1 [text-shadow:0_1px_1px_#0006] hover:brightness-110 disabled:opacity-50 ${styles[variant]}`}
    >
      {children}
    </button>
  );
}
