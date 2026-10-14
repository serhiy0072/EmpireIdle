import type { ReactElement } from "react";
import { rarityKey } from "../../lib/rarity";

/**
 * Іконка предмета: процедурний SVG за ключем і типом, рамка за рідкістю.
 * Ключ підказує форму (sword, bow, staff, amulet…), тип — запасний варіант
 * для незнайомого ключа. Справжній арт підключиться через маніфест, як у героїв.
 */
interface Props {
  itemKey: string;
  type: string;
  rarity: string | number | undefined;
  size?: number;
  className?: string;
  /** Без власного тла й рамки — коли рамку рідкості малює плитка навколо. */
  bare?: boolean;
  /** Заточка артефакта: «+N» у правому верхньому куті, 0 — без позначки. */
  mastery?: number;
}

const FRAME: Record<string, { stroke: string; back: string }> = {
  Common: { stroke: "#94a3b8", back: "#f1f5f9" },
  Rare: { stroke: "#38bdf8", back: "#e0f2fe" },
  Unique: { stroke: "#f59e0b", back: "#fef3c7" },
};

/** Ящик ресурсу: бічні грані кольору ресурсу, на кришці — збільшений знак ресурсу. */
function crate(color: string, mark: ReactElement): ReactElement {
  return (
    <g>
      <path d="M14 36 L50 18 L86 36 L86 70 L50 88 L14 70 Z" fill="#e7d3b0" stroke="#78350f" strokeWidth={2.5} />
      <path d="M14 36 L50 54 L50 88 L14 70 Z" fill={color} opacity={0.9} />
      <path d="M50 54 L86 36 L86 70 L50 88 Z" fill={color} opacity={0.65} />
      <path d="M50 54 L86 36 M50 54 L14 36 M50 54 L50 88" stroke="#78350f" strokeWidth={2.5} />
      <g transform="translate(50 36) scale(1.35) translate(-50 -33)">{mark}</g>
    </g>
  );
}

const woodMark = (
  <g stroke="#78350f" strokeWidth={1.5}>
    <rect x={38} y={26} width={24} height={7} rx={3.5} fill="#d97706" />
    <rect x={40} y={33} width={24} height={7} rx={3.5} fill="#b45309" />
  </g>
);
const foodMark = (
  <g>
    <ellipse cx={50} cy={32} rx={11} ry={7} fill="#f87171" stroke="#7f1d1d" strokeWidth={1.5} />
    <rect x={58} y={30} width={8} height={4} rx={2} fill="#fef3c7" stroke="#7f1d1d" strokeWidth={1} />
  </g>
);
const goldMark = (
  <g stroke="#854d0e" strokeWidth={1.5}>
    <circle cx={45} cy={33} r={7} fill="#facc15" />
    <circle cx={55} cy={30} r={7} fill="#fde047" />
  </g>
);
const ironMark = <path d="M38 38 L42 28 L62 28 L66 38 Z" fill="#cbd5e1" stroke="#334155" strokeWidth={1.5} />;

const SHAPES: { test: RegExp; draw: () => ReactElement }[] = [
  {
    test: /sword|blade/,
    draw: () => (
      <g>
        <path d="M30 70 L66 34" stroke="#cbd5e1" strokeWidth={9} strokeLinecap="round" />
        <path d="M30 70 L66 34" stroke="#f8fafc" strokeWidth={3} strokeLinecap="round" />
        <path d="M24 60 L40 76" stroke="#b45309" strokeWidth={6} strokeLinecap="round" />
        <circle cx={22} cy={78} r={4} fill="#78350f" />
      </g>
    ),
  },
  {
    test: /lance|spear|pike/,
    draw: () => (
      <g>
        <path d="M24 76 L64 36" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <path d="M60 40 L78 22 L72 44 Z" fill="#cbd5e1" stroke="#475569" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /bow|arrow/,
    draw: () => (
      <g>
        <path d="M34 22 Q76 50 34 78" fill="none" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <path d="M34 22 L34 78" stroke="#e5e7eb" strokeWidth={1.5} />
        <path d="M34 50 L70 50" stroke="#475569" strokeWidth={2.5} />
        <path d="M70 50 L62 44 M70 50 L62 56" stroke="#475569" strokeWidth={2.5} strokeLinecap="round" />
      </g>
    ),
  },
  {
    test: /staff|wand|rod/,
    draw: () => (
      <g>
        <path d="M32 78 L60 30" stroke="#78350f" strokeWidth={5} strokeLinecap="round" />
        <circle cx={62} cy={26} r={9} fill="#fb923c" opacity={0.9} />
        <circle cx={62} cy={26} r={4} fill="#fef3c7" />
      </g>
    ),
  },
  {
    test: /necklace|amulet|pendant/,
    draw: () => (
      <g>
        <path d="M30 22 Q50 60 70 22" fill="none" stroke="#b45309" strokeWidth={3} />
        <path d="M50 44 L62 58 L50 76 L38 58 Z" fill="#a78bfa" stroke="#5b21b6" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /ring/,
    draw: () => (
      <g>
        <circle cx={50} cy={56} r={18} fill="none" stroke="#fbbf24" strokeWidth={7} />
        <circle cx={50} cy={34} r={7} fill="#f87171" stroke="#991b1b" strokeWidth={1.5} />
      </g>
    ),
  },
  {
    test: /crown/,
    draw: () => (
      <g>
        <path d="M26 66 L30 34 L42 50 L50 28 L58 50 L70 34 L74 66 Z" fill="#fbbf24" stroke="#b45309" strokeWidth={2} strokeLinejoin="round" />
        <rect x={26} y={64} width={48} height={8} rx={2} fill="#d97706" stroke="#b45309" strokeWidth={1.5} />
        <circle cx={50} cy={58} r={4} fill="#f87171" stroke="#991b1b" strokeWidth={1} />
      </g>
    ),
  },
  {
    test: /belt/,
    draw: () => (
      <g>
        <rect x={16} y={42} width={68} height={16} rx={4} fill="#92400e" stroke="#78350f" strokeWidth={2} />
        <rect x={40} y={38} width={20} height={24} rx={3} fill="none" stroke="#fbbf24" strokeWidth={4} />
        <line x1={50} y1={42} x2={50} y2={58} stroke="#fbbf24" strokeWidth={3} />
      </g>
    ),
  },
  {
    // Книга вміння атаки: червона обкладинка з клинком. Стоїть до «attack|war» — інакше
    // skill_book_warrior_* ловив би хрест атаки
    test: /skill_book.*attack/,
    draw: () => (
      <g>
        <path d="M28 24 L66 24 Q72 24 72 30 L72 78 L34 78 Q28 78 28 72 Z" fill="#dc2626" stroke="#7f1d1d" strokeWidth={2.5} />
        <path d="M34 72 L72 72 L72 78 L34 78 Q28 78 28 72 Q28 66 34 72 Z" fill="#fef3c7" />
        <path d="M50 32 L50 58 M43 52 L57 52" stroke="#fef3c7" strokeWidth={4} strokeLinecap="round" />
      </g>
    ),
  },
  {
    // Книга вміння захисту: синя обкладинка зі щитом
    test: /skill_book.*defense/,
    draw: () => (
      <g>
        <path d="M28 24 L66 24 Q72 24 72 30 L72 78 L34 78 Q28 78 28 72 Z" fill="#2563eb" stroke="#1e3a8a" strokeWidth={2.5} />
        <path d="M34 72 L72 72 L72 78 L34 78 Q28 78 28 72 Q28 66 34 72 Z" fill="#fef3c7" />
        <path d="M50 32 L60 36 L58 50 Q55 57 50 60 Q45 57 42 50 L40 36 Z" fill="#fef3c7" />
      </g>
    ),
  },
  {
    // Універсальний уламок героя: кристал
    test: /shard/,
    draw: () => (
      <g>
        <path d="M50 18 L70 40 L60 80 L40 80 L30 40 Z" fill="#c4b5fd" stroke="#5b21b6" strokeWidth={2.5} strokeLinejoin="round" />
        <path d="M50 18 L56 40 L50 80 L44 40 Z" fill="#ede9fe" />
      </g>
    ),
  },
  {
    // Гаєчка — досвід рівня артефакта
    test: /wrench/,
    draw: () => (
      <g>
        <path d="M30 70 L56 44" stroke="#64748b" strokeWidth={10} strokeLinecap="round" />
        <path d="M54 30 Q66 20 76 30 L66 36 L64 44 L72 46 L78 38 Q80 54 62 52 Z" fill="#94a3b8" stroke="#334155" strokeWidth={2.5} strokeLinejoin="round" />
      </g>
    ),
  },
  {
    // Камінь для заточування: брусок-точило з іскрою
    test: /whetstone/,
    draw: () => (
      <g>
        <path d="M22 62 L60 30 Q66 26 72 32 L78 40 Q82 46 76 50 L38 80 Q32 84 27 78 L20 70 Q17 65 22 62 Z" fill="#94a3b8" stroke="#334155" strokeWidth={2.5} strokeLinejoin="round" />
        <path d="M30 66 L64 38" stroke="#e2e8f0" strokeWidth={3} strokeLinecap="round" />
        <path d="M76 18 L78 26 L86 28 L78 30 L76 38 L74 30 L66 28 L74 26 Z" fill="#fde047" />
      </g>
    ),
  },
  {
    // Ремкомплект: скринька з хрестом
    test: /repair/,
    draw: () => (
      <g>
        <rect x={24} y={36} width={52} height={38} rx={5} fill="#f59e0b" stroke="#78350f" strokeWidth={2.5} />
        <path d="M40 36 L40 28 L60 28 L60 36" fill="none" stroke="#78350f" strokeWidth={3} />
        <path d="M50 44 L50 66 M39 55 L61 55" stroke="#fffbeb" strokeWidth={6} strokeLinecap="round" />
      </g>
    ),
  },
  {
    // Ключ скрині: стоїть до «chest», бо chest_key_* — це самі скрині, а не ключі
    test: /^key_/,
    draw: () => (
      <g>
        <circle cx={36} cy={50} r={14} fill="none" stroke="#b45309" strokeWidth={7} />
        <path d="M50 50 L80 50 M70 50 L70 62 M78 50 L78 58" stroke="#b45309" strokeWidth={7} strokeLinecap="round" />
        <circle cx={36} cy={50} r={5} fill="#fde68a" />
      </g>
    ),
  },
  {
    // Прискорення — подвійний шеврон, як у грі: тривалість пише плитка, а не іконка
    test: /speedup/,
    draw: () => (
      <g>
        <path d="M22 26 L46 50 L22 74 L34 74 L58 50 L34 26 Z" fill="#e0f2fe" stroke="#0369a1" strokeWidth={2.5} strokeLinejoin="round" />
        <path d="M46 26 L70 50 L46 74 L58 74 L82 50 L58 26 Z" fill="#bae6fd" stroke="#0369a1" strokeWidth={2.5} strokeLinejoin="round" />
      </g>
    ),
  },
  {
    test: /essence/,
    draw: () => (
      <g>
        <path d="M50 22 Q70 44 62 62 Q56 74 50 78 Q44 74 38 62 Q30 44 50 22 Z" fill="#a78bfa" stroke="#5b21b6" strokeWidth={2} />
        <path d="M50 42 Q58 52 54 62 Q52 68 50 70 Q48 68 46 62 Q42 52 50 42 Z" fill="#fef3c7" />
      </g>
    ),
  },
  {
    // Банка досвіду: глечик із золотим сяйвом — досвід іде в спільний запас героїв
    test: /hero_xp/,
    draw: () => (
      <g>
        <rect x={40} y={20} width={20} height={8} rx={2} fill="#92400e" />
        <path d="M38 28 L62 28 L60 36 Q74 44 72 62 Q70 80 50 80 Q30 80 28 62 Q26 44 40 36 Z" fill="#fde68a" stroke="#b45309" strokeWidth={2.5} />
        <path d="M33 58 Q50 52 67 58 Q66 76 50 76 Q34 76 33 58 Z" fill="#f59e0b" />
        <path d="M50 46 L53 54 L61 54 L55 59 L57 67 L50 62 L43 67 L45 59 L39 54 L47 54 Z" fill="#fffbeb" />
      </g>
    ),
  },
  {
    // Банка енергії данжів: колба з блискавкою
    test: /energy/,
    draw: () => (
      <g>
        <rect x={42} y={18} width={16} height={8} rx={2} fill="#475569" />
        <path d="M44 26 L56 26 L56 40 L72 70 Q74 80 64 80 L36 80 Q26 80 28 70 L44 40 Z" fill="#cffafe" stroke="#0e7490" strokeWidth={2.5} strokeLinejoin="round" />
        <path d="M34 64 L66 64 L70 72 Q70 77 64 77 L36 77 Q30 77 30 72 Z" fill="#22d3ee" />
        <path d="M54 44 L44 60 L51 60 L46 74 L58 56 L51 56 Z" fill="#facc15" stroke="#a16207" strokeWidth={1} strokeLinejoin="round" />
      </g>
    ),
  },
  // Ящики ресурсів: той самий ящик, але дошки свого кольору й знак ресурсу на кришці — їх видно з першого погляду
  { test: /crate_wood/, draw: () => crate("#b45309", woodMark) },
  { test: /crate_food/, draw: () => crate("#65a30d", foodMark) },
  { test: /crate_gold/, draw: () => crate("#ca8a04", goldMark) },
  { test: /crate_iron/, draw: () => crate("#64748b", ironMark) },
  {
    test: /crate_mixed/,
    draw: () =>
      crate(
        "#7c3aed",
        // Усі чотири знаки дрібніше, квадратом навколо центру кришки
        <g>
          <g transform="translate(42 28) scale(0.5) translate(-50 -33)">{goldMark}</g>
          <g transform="translate(58 28) scale(0.5) translate(-50 -33)">{woodMark}</g>
          <g transform="translate(42 39) scale(0.5) translate(-50 -33)">{foodMark}</g>
          <g transform="translate(58 39) scale(0.5) translate(-50 -33)">{ironMark}</g>
        </g>,
      ),
  },
  {
    test: /crate|chest|box/,
    draw: () => (
      <g>
        <path d="M26 42 L50 30 L74 42 L74 70 L50 82 L26 70 Z" fill="#d6b98c" stroke="#78350f" strokeWidth={2} />
        <path d="M50 54 L74 42 M50 54 L26 42 M50 54 L50 82" stroke="#78350f" strokeWidth={2} />
      </g>
    ),
  },
  {
    test: /teleport|portal/,
    draw: () => (
      <g>
        <ellipse cx={50} cy={50} rx={16} ry={26} fill="#7dd3fc" stroke="#0369a1" strokeWidth={3} />
        <ellipse cx={50} cy={50} rx={7} ry={14} fill="#e0f2fe" />
      </g>
    ),
  },
  {
    // Корм для звірів: миска з кісткою
    test: /feed/,
    draw: () => (
      <g>
        <path d="M24 56 L76 56 Q74 76 50 78 Q26 76 24 56 Z" fill="#d97706" stroke="#78350f" strokeWidth={2} />
        <path d="M36 50 L64 40" stroke="#fef3c7" strokeWidth={6} strokeLinecap="round" />
        <circle cx={34} cy={51} r={5} fill="#fef3c7" />
        <circle cx={66} cy={39} r={5} fill="#fef3c7" />
      </g>
    ),
  },
  {
    // Завіса туману: хмара над прихованим оком
    test: /veil|fog/,
    draw: () => (
      <g>
        <path d="M28 58 Q50 40 72 58 Q50 76 28 58 Z" fill="#e2e8f0" stroke="#475569" strokeWidth={2} />
        <circle cx={50} cy={58} r={6} fill="#475569" />
        <path d="M26 44 Q30 32 42 34 Q48 24 60 30 Q72 28 74 42 Z" fill="#94a3b8" />
      </g>
    ),
  },
  {
    test: /attack|war/,
    draw: () => (
      <g>
        <path d="M30 30 L70 70 M70 30 L30 70" stroke="#dc2626" strokeWidth={7} strokeLinecap="round" />
      </g>
    ),
  },
  {
    test: /defense|fortif|shield/,
    draw: () => (
      <g>
        <path d="M50 22 L72 32 L68 60 Q60 74 50 80 Q40 74 32 60 L28 32 Z" fill="#60a5fa" stroke="#1e40af" strokeWidth={2} />
      </g>
    ),
  },
  {
    test: /boost|production|rush/,
    draw: () => (
      <g>
        <path d="M50 24 L70 48 L58 48 L58 76 L42 76 L42 48 L30 48 Z" fill="#4ade80" stroke="#15803d" strokeWidth={2} />
      </g>
    ),
  },
];

const BY_TYPE: Record<string, RegExp> = {
  equipment: /sword/,
  boost: /boost/,
  resources: /crate/,
  teleport: /teleport/,
  evolution: /essence/,
  feed: /feed/,
  scoutveil: /veil/,
  heroxp: /hero_xp/,
  energy: /energy/,
  lootbox: /chest/,
  weaponchest: /chest/,
  universalshard: /shard/,
  wrench: /wrench/,
  repairkit: /repair/,
  whetstone: /whetstone/,
  chestkey: /^key_/,
};

export default function ItemIcon({ itemKey, type, rarity, size = 48, className = "", bare = false, mastery = 0 }: Props): ReactElement {
  const frame = FRAME[rarityKey(rarity)] ?? (FRAME.Common as { stroke: string; back: string });
  const key = itemKey.toLowerCase();
  const shape = SHAPES.find((candidate) => candidate.test.test(key)) ?? SHAPES.find((candidate) => candidate.test.test(BY_TYPE[type]?.source ?? ""));

  return (
    <svg viewBox="0 0 100 100" width={size} height={size} className={`shrink-0 rounded-lg ${className}`} aria-hidden>
      {!bare && <rect width={100} height={100} rx={12} fill={frame.back} />}
      {shape?.draw()}
      {!bare && <rect x={2} y={2} width={96} height={96} rx={11} fill="none" stroke={frame.stroke} strokeWidth={3} />}
      {/* Заточка видна на самому значку — як на референсах, без підпису поруч */}
      {mastery > 0 && (
        <g>
          <rect x={mastery >= 10 ? 46 : 60} y={2} width={mastery >= 10 ? 52 : 38} height={34} rx={8} fill="#0f172a" opacity={0.85} />
          <text x={mastery >= 10 ? 72 : 79} y={28} textAnchor="middle" fontSize={27} fontWeight={800} fill="#fde68a">
            +{mastery}
          </text>
        </g>
      )}
    </svg>
  );
}
