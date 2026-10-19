/**
 * Процедурний портрет героя рядком SVG — один для карток (img) і для поля бою (canvas). У кожного героя
 * своя зовнішність: стать видно з облич і зачісок, клас — з тла, обладунку й значка в куті, а риси з
 * легенди (сокіл, полум'я, мовчання, крига) роблять героя впізнаваним серед інших того ж класу.
 */

type HairStyle = "short" | "long" | "braids" | "ponytail" | "bun" | "wild" | "bald";
type Beard = "stubble" | "short" | "full" | "mustache";
type Headgear =
  | "hood"
  | "furHood"
  | "helm"
  | "plumedHelm"
  | "hornedHelm"
  | "flameCrown"
  | "mirrorCirclet"
  | "iceCrown"
  | "sunCrown"
  | "featherCap";
type Mark = "scar" | "eyepatch" | "warPaint" | "mask" | "veil" | "falcon" | "lightning" | "frost" | "feather" | "apron";

interface Look {
  female: boolean;
  skin: string;
  hair: string;
  style: HairStyle;
  beard?: Beard;
  head?: Headgear;
  marks?: Mark[];
  /** Колір очей, коли вони світяться (маги); без нього — темні. */
  glow?: string;
  /** Колір одягу замість класового — мантії магів. */
  robe?: string;
}

const SKIN = { pale: "#fde3cf", fair: "#f6cfa8", tan: "#e0ac69", brown: "#b97a4a", dark: "#7a4a2a", stone: "#a8a29e", frost: "#dbeafe" };

/** Зовнішність кожного героя з heroes.json; рішення 19.10.2026 — у бою має бути видно, хто є хто. */
const LOOKS: Record<string, Look> = {
  warrior_bran: { female: false, skin: SKIN.tan, hair: "#3f2a1d", style: "short", beard: "full", marks: ["apron", "scar"] },
  archer_lyra: { female: true, skin: SKIN.fair, hair: "#e9c46a", style: "long", marks: ["feather"] },
  knight_ordwin: { female: false, skin: SKIN.fair, hair: "#9ca3af", style: "short", beard: "mustache", head: "helm", marks: ["scar"] },
  mage_iselle: { female: true, skin: SKIN.pale, hair: "#dc2626", style: "long", head: "flameCrown", glow: "#f97316", robe: "#9a3412" },
  warrior_hedda: { female: true, skin: SKIN.fair, hair: "#f4d06f", style: "braids", marks: ["warPaint"] },
  warrior_torvald: { female: false, skin: SKIN.fair, hair: "#b45309", style: "short", beard: "full", head: "hornedHelm" },
  warrior_maren: { female: true, skin: SKIN.brown, hair: "#1f2937", style: "ponytail", marks: ["warPaint", "scar"] },
  knight_aldric: { female: false, skin: SKIN.tan, hair: "#6b4226", style: "short", beard: "stubble", head: "plumedHelm" },
  knight_serah: { female: true, skin: SKIN.fair, hair: "#7c2d12", style: "long", head: "plumedHelm" },
  archer_finn: { female: false, skin: SKIN.fair, hair: "#d6b25e", style: "short", beard: "short", head: "furHood" },
  archer_ysolde: { female: true, skin: SKIN.pale, hair: "#4b5563", style: "long", head: "hood", marks: ["mask"] },
  archer_rook: { female: false, skin: SKIN.brown, hair: "#111827", style: "short", beard: "stubble", head: "featherCap", marks: ["eyepatch"] },
  mage_calder: { female: false, skin: SKIN.fair, hair: "#f1f5f9", style: "wild", beard: "stubble", marks: ["lightning"], glow: "#facc15", robe: "#1e3a8a" },
  mage_nerys: { female: true, skin: SKIN.dark, hair: "#e5e7eb", style: "bun", head: "mirrorCirclet", glow: "#67e8f9", robe: "#0e7490" },
  warrior_gunnar: { female: false, skin: SKIN.stone, hair: "#57534e", style: "bald", beard: "full", marks: ["scar"] },
  knight_elowen: { female: true, skin: SKIN.fair, hair: "#fbbf24", style: "long", head: "sunCrown" },
  archer_kestrel: { female: true, skin: SKIN.tan, hair: "#92400e", style: "ponytail", marks: ["falcon", "feather"] },
  mage_vael: { female: false, skin: SKIN.pale, hair: "#1f2937", style: "short", head: "hood", marks: ["veil"], glow: "#c084fc", robe: "#3b0764" },
  mage_sabine: { female: true, skin: SKIN.frost, hair: "#bae6fd", style: "long", head: "iceCrown", marks: ["frost"], glow: "#38bdf8", robe: "#075985" },
};

const CLASS_BACK: Record<string, [string, string]> = {
  warrior: ["#fecaca", "#991b1b"],
  archer: ["#bbf7d0", "#166534"],
  knight: ["#dbeafe", "#1e40af"],
};

const CLASS_GEAR: Record<string, string> = { warrior: "#7f1d1d", archer: "#14532d", knight: "#64748b" };

const RANK_FRAME: Record<string, string> = { Common: "#94a3b8", Rare: "#38bdf8", Unique: "#f59e0b" };

const HAIRS = ["#1f2937", "#4a2c17", "#8b5a2b", "#d4a017", "#b91c1c"];
const SKINS = [SKIN.pale, SKIN.fair, SKIN.tan, SKIN.brown, SKIN.dark];

/** Стабільний хеш ключа: герой без опису (новий у конфігу) однаково виглядає щоразу. */
function hash(text: string, salt: number): number {
  let h = 2166136261 ^ salt;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0) / 4294967296;
}

function fallback(heroKey: string): Look {
  const female = hash(heroKey, 3) < 0.5;
  return {
    female,
    skin: SKINS[Math.floor(hash(heroKey, 1) * SKINS.length)] ?? SKIN.fair,
    hair: HAIRS[Math.floor(hash(heroKey, 2) * HAIRS.length)] ?? "#1f2937",
    style: female ? "long" : "short",
    beard: female ? undefined : "stubble",
  };
}

function hairBack(look: Look): string {
  const h = look.hair;
  switch (look.style) {
    case "long":
      return `<path d="M29 40 Q26 72 33 86 L67 86 Q74 72 71 40 Z" fill="${h}"/>`;
    case "braids":
      return (
        `<path d="M32 48 Q27 66 31 84" stroke="${h}" stroke-width="7" fill="none" stroke-linecap="round"/>` +
        `<path d="M68 48 Q73 66 69 84" stroke="${h}" stroke-width="7" fill="none" stroke-linecap="round"/>` +
        `<rect x="27" y="76" width="8" height="3" fill="#7f1d1d"/><rect x="65" y="76" width="8" height="3" fill="#7f1d1d"/>`
      );
    case "ponytail":
      return `<path d="M64 30 Q86 38 78 74" stroke="${h}" stroke-width="8" fill="none" stroke-linecap="round"/>`;
    case "bun":
      return `<circle cx="50" cy="21" r="8" fill="${h}"/>`;
    default:
      return "";
  }
}

function hairFront(look: Look): string {
  const h = look.hair;
  switch (look.style) {
    case "bald":
      return `<path d="M38 30 Q44 27 48 29" stroke="rgba(255,255,255,0.5)" stroke-width="2" fill="none" stroke-linecap="round"/>`;
    case "wild":
      return `<path d="M29 44 L26 30 L34 34 L33 20 L42 28 L46 14 L52 26 L60 15 L62 28 L71 20 L68 34 L75 30 L71 44 Q62 33 50 33 Q38 33 29 44 Z" fill="${h}"/>`;
    case "short":
      return `<path d="M31 41 Q32 25 50 24 Q68 25 69 41 Q62 33 50 33 Q38 33 31 41 Z" fill="${h}"/>`;
    default:
      // Жіночі зачіски — пасмо набік, щоб відрізнялись від чоловічих коротких
      return `<path d="M30 46 Q30 23 50 22 Q70 23 70 44 Q64 30 52 31 Q42 36 30 46 Z" fill="${h}"/>`;
  }
}

function face(look: Look): string {
  const eye = look.glow ?? "#0f172a";
  const eyes = `<circle cx="43" cy="46" r="2.3" fill="${eye}"/><circle cx="57" cy="46" r="2.3" fill="${eye}"/>`;
  const nose = `<path d="M50 48 L48.5 53 L51 53" fill="none" stroke="rgba(124,45,18,0.5)" stroke-width="1"/>`;

  if (look.female)
    return (
      eyes +
      // Вії й губи — головна жіноча ознака на маленькому портреті
      `<path d="M39.5 44 l-2 -2 M41.5 43.2 l-1 -2.4 M60.5 44 l2 -2 M58.5 43.2 l1 -2.4" stroke="#0f172a" stroke-width="1" stroke-linecap="round"/>` +
      `<path d="M40 40.5 Q43 39 46 40.5 M54 40.5 Q57 39 60 40.5" stroke="${look.hair}" stroke-width="1" fill="none"/>` +
      nose +
      `<path d="M45.5 56.5 Q50 55 54.5 56.5 Q50 60 45.5 56.5 Z" fill="#be123c"/>`
    );

  return (
    eyes +
    `<path d="M38.5 41 L47 40 M53 40 L61.5 41" stroke="${look.hair === "#f1f5f9" ? "#64748b" : look.hair}" stroke-width="2.4" stroke-linecap="round"/>` +
    nose +
    `<path d="M45 57 Q50 59 55 57" fill="none" stroke="#7c2d12" stroke-width="1.6" stroke-linecap="round"/>`
  );
}

function beard(look: Look): string {
  const h = look.hair;
  switch (look.beard) {
    case "stubble":
      return `<path d="M33 50 Q35 68 50 69 Q65 68 67 50 Q60 63 50 63 Q40 63 33 50 Z" fill="${h}" opacity="0.35"/>`;
    case "short":
      return `<path d="M33 50 Q35 70 50 71 Q65 70 67 50 Q60 63 50 63 Q40 63 33 50 Z" fill="${h}"/><path d="M42 55 Q50 52 58 55 Q50 57 42 55 Z" fill="${h}"/>`;
    case "full":
      return `<path d="M31 48 Q30 80 50 84 Q70 80 69 48 Q62 64 50 64 Q38 64 31 48 Z" fill="${h}"/><path d="M41 55 Q50 51 59 55 Q50 58 41 55 Z" fill="${h}"/>`;
    case "mustache":
      return `<path d="M40 56 Q45 51 50 54 Q55 51 60 56 Q55 55 50 57 Q45 55 40 56 Z" fill="${h}"/>`;
    default:
      return "";
  }
}

function headgear(look: Look, gear: string): string {
  switch (look.head) {
    case "hood":
      return `<path d="M21 64 Q20 15 50 13 Q80 15 79 64 L69 62 Q67 31 50 29 Q33 31 31 62 Z" fill="${look.robe ?? gear}"/>`;
    case "furHood":
      return (
        `<path d="M21 64 Q20 15 50 13 Q80 15 79 64 L69 62 Q67 31 50 29 Q33 31 31 62 Z" fill="#78350f"/>` +
        `<path d="M31 62 Q30 36 38 31 Q44 27 50 28 Q56 27 62 31 Q70 36 69 62" fill="none" stroke="#f5f5f4" stroke-width="5" stroke-dasharray="3 2"/>`
      );
    case "helm":
    case "plumedHelm":
    case "hornedHelm": {
      // Відкритий шолом: обличчя видно, тож стать і риси читаються й у лицарів
      const helm =
        `<path d="M29 44 Q29 20 50 19 Q71 20 71 44 L66 45 Q65 30 50 29 Q35 30 34 45 Z" fill="#94a3b8" stroke="#475569" stroke-width="1.5"/>` +
        `<rect x="48.5" y="29" width="3" height="16" rx="1" fill="#64748b"/>` +
        `<path d="M29 44 L28 58 L34 60 L34 45 Z M71 44 L72 58 L66 60 L66 45 Z" fill="#94a3b8" stroke="#475569" stroke-width="1"/>`;
      if (look.head === "plumedHelm") return helm + `<path d="M50 19 Q52 3 70 8 Q61 12 61 23" fill="#dc2626"/>`;
      if (look.head === "hornedHelm")
        return helm + `<path d="M31 30 Q16 24 18 8 Q24 22 35 24 Z M69 30 Q84 24 82 8 Q76 22 65 24 Z" fill="#f5f5f4" stroke="#78716c" stroke-width="1"/>`;
      return helm;
    }
    case "flameCrown":
      return (
        `<path d="M32 34 Q50 28 68 34 L68 37 Q50 31 32 37 Z" fill="#fbbf24"/>` +
        `<path d="M38 33 Q35 22 41 16 Q41 25 45 26 Q44 15 51 9 Q51 21 56 24 Q58 16 63 15 Q61 24 63 32 Z" fill="#f97316" opacity="0.9"/>` +
        `<path d="M44 30 Q44 22 49 18 Q49 25 53 27 Q55 22 58 21 Q57 27 58 31 Z" fill="#fde047"/>`
      );
    case "mirrorCirclet":
      return `<path d="M32 35 Q50 29 68 35 L68 38 Q50 32 32 38 Z" fill="#e5e7eb"/><path d="M50 22 L56 30 L50 36 L44 30 Z" fill="#a5f3fc" stroke="#e5e7eb" stroke-width="1.5"/>`;
    case "iceCrown":
      return (
        `<path d="M32 35 Q50 29 68 35 L68 38 Q50 32 32 38 Z" fill="#e0f2fe"/>` +
        `<path d="M36 34 L38 22 L42 33 L46 16 L50 32 L54 16 L58 33 L62 22 L64 34 Z" fill="#7dd3fc" stroke="#e0f2fe" stroke-width="1"/>`
      );
    case "sunCrown":
      return (
        `<g stroke="#fde68a" stroke-width="2" stroke-linecap="round"><path d="M50 22 L50 12 M40 25 L35 16 M60 25 L65 16 M33 31 L25 26 M67 31 L75 26"/></g>` +
        `<path d="M32 35 Q50 28 68 35 L68 38 Q50 31 32 38 Z" fill="#f59e0b"/><circle cx="50" cy="32" r="3.5" fill="#fef3c7"/>`
      );
    case "featherCap":
      return (
        `<path d="M29 40 Q30 22 50 21 Q70 22 72 38 Q60 33 50 33 Q38 33 29 40 Z" fill="#7c3aed"/>` +
        `<path d="M70 30 Q88 16 92 4 Q84 22 72 34 Z" fill="#facc15"/><path d="M66 29 Q84 18 86 8" stroke="#16a34a" stroke-width="2" fill="none"/>`
      );
    default:
      return "";
  }
}

function marks(look: Look): string {
  return (look.marks ?? [])
    .map((mark) => {
      switch (mark) {
        case "scar":
          return `<path d="M58 49 L63 57" stroke="#9f1239" stroke-width="1.6" stroke-linecap="round"/><path d="M59 52 l2.5 -1 M60.5 54.5 l2.5 -1" stroke="#9f1239" stroke-width="1"/>`;
        case "eyepatch":
          return `<path d="M33 40 L67 51" stroke="#0f172a" stroke-width="1.5"/><ellipse cx="57" cy="46" rx="5" ry="4" fill="#0f172a"/>`;
        case "warPaint":
          return `<path d="M38 50 L46 50 M38 53 L45 53 M54 50 L62 50 M55 53 L62 53" stroke="#1d4ed8" stroke-width="1.8" stroke-linecap="round"/>`;
        case "mask":
          return `<path d="M32 52 Q50 50 68 52 L66 64 Q50 70 34 64 Z" fill="#1f2937"/>`;
        case "veil":
          return `<path d="M31 51 Q50 48 69 51 L67 68 Q50 74 33 68 Z" fill="#1e1b4b"/><path d="M36 56 Q50 54 64 56" stroke="#4c1d95" stroke-width="1"/>`;
        case "falcon":
          return (
            `<path d="M70 72 Q74 58 84 60 Q90 62 88 70 Q86 78 74 80 Z" fill="#78350f"/>` +
            `<path d="M84 60 L92 62 L86 65 Z" fill="#facc15"/><circle cx="85" cy="63" r="1.4" fill="#0f172a"/>` +
            `<path d="M72 74 Q66 70 62 76" stroke="#92400e" stroke-width="3" fill="none"/>`
          );
        case "lightning":
          return `<path d="M80 10 L71 30 L78 30 L68 52 L86 24 L78 24 L86 10 Z" fill="#facc15" stroke="#a16207" stroke-width="1"/>`;
        case "frost":
          return `<g stroke="#e0f2fe" stroke-width="1.5" stroke-linecap="round"><path d="M18 22 l0 10 M13 27 l10 0 M14.5 23.5 l7 7 M21.5 23.5 l-7 7"/><path d="M84 42 l0 8 M80 46 l8 0"/></g>`;
        case "feather":
          return `<path d="M32 30 Q20 14 24 4 Q30 18 35 28 Z" fill="#f8fafc" stroke="#94a3b8" stroke-width="1"/>`;
        case "apron":
          return `<path d="M38 80 L62 80 L64 100 L36 100 Z" fill="#78350f"/><path d="M38 80 L44 70 M62 80 L56 70" stroke="#451a03" stroke-width="2"/>`;
        default:
          return "";
      }
    })
    .join("");
}

/** Значок класу в куті: меч — воїн, лук — стрілець, спис — лицар. */
function classBadge(heroClass: string | undefined): string {
  const icon =
    heroClass === "warrior"
      ? `<path d="M80 92 L90 78" stroke="#f8fafc" stroke-width="2.5" stroke-linecap="round"/><path d="M79 85 L85 91" stroke="#fbbf24" stroke-width="2.5" stroke-linecap="round"/>`
      : heroClass === "archer"
        ? `<path d="M80 77 Q93 85 80 93" stroke="#fbbf24" stroke-width="2" fill="none"/><path d="M80 77 L80 93 M78 85 L91 85" stroke="#f8fafc" stroke-width="1.5"/>`
        : heroClass === "knight"
          ? `<path d="M78 93 L91 77" stroke="#f8fafc" stroke-width="2" stroke-linecap="round"/><path d="M91 77 L86 79 L89 82 Z" fill="#cbd5e1"/><path d="M82 86 l3 3" stroke="#dc2626" stroke-width="3"/>`
          : "";

  return icon === "" ? "" : `<circle cx="85" cy="85" r="11" fill="#0f172a" stroke="#f8fafc" stroke-width="1.5"/>${icon}`;
}

/** Повний SVG портрета: тло класу, аура тіру, бюст із рисами героя, рамка рідкості, значок класу. */
export function heroSvg(heroKey: string, heroClass: string | undefined, rank: string | undefined, tier: number): string {
  const look = LOOKS[heroKey] ?? fallback(heroKey);
  const [light, dark] = CLASS_BACK[heroClass ?? ""] ?? ["#e2e8f0", "#475569"];
  const gear = CLASS_GEAR[heroClass ?? ""] ?? "#475569";
  const body = look.robe ?? gear;
  const frame = RANK_FRAME[rank ?? ""] ?? RANK_FRAME.Common;
  const id = `g-${heroKey.replace(/[^a-z0-9_-]/gi, "")}`;

  const shoulders =
    heroClass === "knight" && look.robe === undefined
      ? `<path d="M8 100 Q12 70 50 68 Q88 70 92 100 Z" fill="${body}"/><path d="M8 86 Q14 66 33 70 L30 86 Z M92 86 Q86 66 67 70 L70 86 Z" fill="#94a3b8" stroke="#475569" stroke-width="1.5"/>`
      : `<path d="M8 100 Q12 70 50 68 Q88 70 92 100 Z" fill="${body}"/>`;

  return (
    // Розмір у самому SVG: без нього canvas не знає, якого розміру картинка
    `<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">` +
    `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${light}"/><stop offset="1" stop-color="${dark}"/></linearGradient></defs>` +
    `<rect width="100" height="100" rx="10" fill="url(#${id})"/>` +
    (tier >= 3 ? `<circle cx="50" cy="54" r="40" fill="rgba(254,240,138,0.35)"/>` : "") +
    shoulders +
    hairBack(look) +
    (tier >= 2 ? `<path d="M18 100 Q22 78 50 76 Q78 78 82 100" fill="none" stroke="#fbbf24" stroke-width="2"/>` : "") +
    `<rect x="43" y="58" width="14" height="14" fill="${look.skin}"/>` +
    `<ellipse cx="50" cy="46" rx="${look.female ? 17.5 : 19}" ry="21.5" fill="${look.skin}"/>` +
    face(look) +
    beard(look) +
    hairFront(look) +
    headgear(look, gear) +
    marks(look) +
    `<rect x="1.5" y="1.5" width="97" height="97" rx="9" fill="none" stroke="${frame}" stroke-width="3"/>` +
    (rank === "Unique" ? `<rect x="5" y="5" width="90" height="90" rx="7" fill="none" stroke="#fde68a" stroke-width="1"/>` : "") +
    classBadge(heroClass) +
    `</svg>`
  );
}

/** Той самий портрет як адреса для img і для canvas. */
export function heroArtUrl(heroKey: string, heroClass: string | undefined, rank: string | undefined, tier: number): string {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(heroSvg(heroKey, heroClass, rank, tier))}`;
}
