import type { ReactElement } from "react";
import { portraitFor } from "./heroSprites";

/**
 * Портрет героя: спрайт із маніфесту або процедурний бюст.
 * Клас задає атрибути (шолом, капюшон, капелюх, зброя), ключ героя — палітру,
 * ранг — рамку, тір — оздоблення. Коли з'явиться арт, міняється лише маніфест.
 */
interface Props {
  heroKey: string;
  /** Клас із каталогу: warrior, archer, knight, mage. Невідомий — базовий бюст. */
  heroClass: string | undefined;
  /** Ранг із каталогу: Common, Rare, Unique. */
  rank: string | undefined;
  tier: number;
  /** Сторона квадрата в пікселях. */
  size?: number;
  className?: string;
}

const RANK_FRAME: Record<string, { stroke: string; glow: string }> = {
  Common: { stroke: "#94a3b8", glow: "transparent" },
  Rare: { stroke: "#38bdf8", glow: "rgba(56, 189, 248, 0.35)" },
  Unique: { stroke: "#f59e0b", glow: "rgba(245, 158, 11, 0.45)" },
};

const CLASS_BACK: Record<string, [string, string]> = {
  warrior: ["#fecaca", "#b91c1c"],
  archer: ["#bbf7d0", "#15803d"],
  knight: ["#dbeafe", "#1e40af"],
  mage: ["#e9d5ff", "#6d28d9"],
};

const SKINS = ["#fcd9b6", "#f1c27d", "#e0ac69", "#c68642", "#8d5524"];
const HAIRS = ["#1f2937", "#4a2c17", "#8b5a2b", "#d4a017", "#b91c1c", "#e5e7eb"];

/** Стабільний хеш ключа: один герой — завжди та сама зовнішність. */
function hash(text: string, salt: number): number {
  let h = 2166136261 ^ salt;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0) / 4294967296;
}

function pick<T>(items: readonly T[], value: number): T {
  return items[Math.floor(value * items.length) % items.length] as T;
}

function Warrior({ hair }: { hair: string }): ReactElement {
  return (
    <g>
      {/* Пов'язка на чолі й шрам */}
      <path d="M30 40 Q50 32 70 40 L70 46 Q50 38 30 46 Z" fill="#7f1d1d" />
      <path d="M58 52 l4 8" stroke="#b45309" strokeWidth={1.5} strokeLinecap="round" />
      <path d="M30 40 Q34 26 50 25 Q66 26 70 40 Q60 33 50 33 Q40 33 30 40 Z" fill={hair} />
      {/* Щит зліва, руків'я меча справа */}
      <path d="M12 74 Q10 100 26 104 L26 78 Z" fill="#78350f" stroke="#451a03" strokeWidth={1.5} />
      <circle cx={19} cy={88} r={3} fill="#fbbf24" />
      <rect x={82} y={62} width={4} height={30} rx={1} fill="#cbd5e1" transform="rotate(-20 84 77)" />
      <rect x={76} y={86} width={16} height={4} rx={1} fill="#b45309" transform="rotate(-20 84 88)" />
    </g>
  );
}

function Archer({ hair }: { hair: string }): ReactElement {
  return (
    <g>
      <path d="M30 40 Q34 26 50 25 Q66 26 70 40 Q60 33 50 33 Q40 33 30 40 Z" fill={hair} />
      {/* Капюшон */}
      <path d="M22 60 Q22 18 50 16 Q78 18 78 60 L68 58 Q66 34 50 32 Q34 34 32 58 Z" fill="#166534" />
      {/* Лук і сагайдак */}
      <path d="M86 50 Q100 78 86 106" fill="none" stroke="#78350f" strokeWidth={3} strokeLinecap="round" />
      <path d="M86 50 L86 106" stroke="#e5e7eb" strokeWidth={1} />
      <rect x={8} y={62} width={10} height={30} rx={3} fill="#78350f" transform="rotate(15 13 77)" />
      <path d="M10 62 l3 -8 M14 62 l2 -9 M17 63 l3 -7" stroke="#fef3c7" strokeWidth={1.5} strokeLinecap="round" />
    </g>
  );
}

function Knight(): ReactElement {
  return (
    <g>
      {/* Закритий шолом із прорізом і плюмажем */}
      <path d="M31 62 Q31 26 50 24 Q69 26 69 62 Z" fill="#94a3b8" stroke="#475569" strokeWidth={1.5} />
      <rect x={36} y={46} width={28} height={5} rx={2} fill="#0f172a" />
      <path d="M50 24 Q50 8 66 12 Q60 18 62 30" fill="#dc2626" />
      {/* Наплічники */}
      <path d="M8 84 Q14 66 32 70 L30 84 Z" fill="#94a3b8" stroke="#475569" strokeWidth={1.5} />
      <path d="M92 84 Q86 66 68 70 L70 84 Z" fill="#94a3b8" stroke="#475569" strokeWidth={1.5} />
    </g>
  );
}

function Mage({ accent }: { accent: string }): ReactElement {
  return (
    <g>
      {/* Гострий капелюх із крисами */}
      <path d="M22 44 Q50 38 78 44 Q50 50 22 44 Z" fill="#4c1d95" />
      <path d="M32 42 Q46 8 58 4 Q58 26 68 42 Z" fill="#5b21b6" />
      <circle cx={57} cy={14} r={2.5} fill="#fde68a" />
      {/* Посох зі сферою */}
      <rect x={84} y={40} width={3} height={64} rx={1} fill="#78350f" />
      <circle cx={85.5} cy={36} r={7} fill={accent} opacity={0.9} />
      <circle cx={85.5} cy={36} r={3} fill="#fef3c7" />
    </g>
  );
}

export default function HeroPortrait({ heroKey, heroClass, rank, tier, size = 56, className = "" }: Props): ReactElement {
  const sprite = portraitFor(heroKey, tier);
  const frame = RANK_FRAME[rank ?? ""] ?? (RANK_FRAME.Common as { stroke: string; glow: string });
  const [light, dark] = CLASS_BACK[heroClass ?? ""] ?? ["#e2e8f0", "#475569"];
  const skin = pick(SKINS, hash(heroKey, 1));
  const hair = pick(HAIRS, hash(heroKey, 2));
  const gradientId = `hero-${heroKey}`;

  if (sprite !== null) {
    return (
      <img
        src={sprite}
        alt=""
        width={size}
        height={size}
        className={`rounded-lg border-2 object-cover ${className}`}
        style={{ borderColor: frame.stroke, boxShadow: `0 0 0 3px ${frame.glow}` }}
      />
    );
  }

  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      className={`shrink-0 rounded-lg ${className}`}
      style={{ boxShadow: `0 0 0 3px ${frame.glow}` }}
      aria-hidden
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={light} />
          <stop offset="1" stopColor={dark} />
        </linearGradient>
      </defs>
      <rect width={100} height={100} rx={10} fill={`url(#${gradientId})`} />

      {/* Аура тіру 3 — за спиною, під бюстом */}
      {tier >= 3 && <circle cx={50} cy={56} r={40} fill="rgba(254, 240, 138, 0.35)" />}

      {/* Плечі й шия */}
      <path d="M10 100 Q14 70 50 68 Q86 70 90 100 Z" fill={dark} />
      <path d="M10 100 Q14 70 50 68 Q86 70 90 100 Z" fill="rgba(255,255,255,0.15)" />
      {tier >= 2 && <path d="M18 100 Q22 78 50 76 Q78 78 82 100 Z" fill="none" stroke="#fbbf24" strokeWidth={2} />}
      <rect x={43} y={58} width={14} height={14} fill={skin} />

      {/* Голова та обличчя */}
      <ellipse cx={50} cy={46} rx={19} ry={22} fill={skin} />
      <circle cx={43} cy={46} r={2.2} fill="#0f172a" />
      <circle cx={57} cy={46} r={2.2} fill="#0f172a" />
      <path d="M45 56 Q50 60 55 56" fill="none" stroke="#7c2d12" strokeWidth={1.5} strokeLinecap="round" />

      {heroClass === "warrior" && <Warrior hair={hair} />}
      {heroClass === "archer" && <Archer hair={hair} />}
      {heroClass === "knight" && <Knight />}
      {heroClass === "mage" && <Mage accent={light} />}
      {(heroClass === undefined || !(heroClass in CLASS_BACK)) && (
        <path d="M30 40 Q34 26 50 25 Q66 26 70 40 Q60 33 50 33 Q40 33 30 40 Z" fill={hair} />
      )}

      <rect x={1.5} y={1.5} width={97} height={97} rx={9} fill="none" stroke={frame.stroke} strokeWidth={3} />
      {rank === "Unique" && <rect x={5} y={5} width={90} height={90} rx={7} fill="none" stroke="#fde68a" strokeWidth={1} />}
    </svg>
  );
}
