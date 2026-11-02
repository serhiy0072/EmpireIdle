import { Link, NavLink, useMatch } from "react-router-dom";
import { NavIcon } from "./icons";
import { t } from "i18next";

/**
 * Розділи панелі — усі на виду, без меню «Ще»; на них же вказує навчання (data-tutorial="nav:…").
 * Військо, звіринець, данжі й ринок відкриваються з їхніх будівель у селі (рішення 19.10.2026),
 * чат — смужкою над панеллю (ChatTicker). Села на панелі немає (рішення 21.10.2026): туди веде
 * стрілка ← у HUD, а Esc — назад.
 */
const SECTIONS = [
  { to: "/heroes", label: "heroes", icon: "heroes" },
  { to: "/tower", label: "tower", icon: "tower" },
  { to: "/quests", label: "quests", icon: "quests" },
  { to: "/inventory", label: "inventory", icon: "inventory" },
  { to: "/clan", label: "clan", icon: "clan" },
  { to: "/mail", label: "mail", icon: "mail" },
  { to: "/rating", label: "rating", icon: "rating" },
] as const;

interface Props {
  /** Скільки чекає в кожному розділі (шлях → кількість), з useAttention. */
  attention: Record<string, number>;
}

/**
 * Нижня панель розділів, як у мобільних стратегіях (рішення 19.10.2026): значки з підписом,
 * активний розділ піднятий і світліший, мапа світу — окрема кругла кнопка в правому куті.
 */
export default function BottomNav({ attention }: Props) {
  const onMap = useMatch("/map") !== null;
  // Червона крапка з кількістю — щось чекає, щоб його забрали; гасне, щойно забрано
  const badge = (count: number) =>
    count > 0 && (
      <span className="absolute -right-1.5 -top-1 flex">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-rose-400 opacity-60" />
        <span className="relative min-w-4 rounded-full bg-rose-500 px-1 text-center text-[10px] font-bold leading-4 text-white shadow ring-1 ring-white">
          {count > 99 ? "99+" : count}
        </span>
      </span>
    );

  const tab = (active: boolean) =>
    `relative flex min-w-0 flex-1 flex-col items-center gap-0.5 rounded-t-xl px-0.5 pb-1.5 pt-1 text-[11px] font-semibold transition ${
      active ? "-mt-2 bg-gradient-to-b from-[#5b85c4] to-[#24467f] pt-2 text-white" : "text-sky-100/80 hover:text-white"
    }`;

  return (
    <nav className="relative shrink-0 border-t-2 border-[#5b85c4] bg-gradient-to-b from-[#1d3b6f] to-[#0f2147] [text-shadow:0_1px_1px_#0008]">
      <div className="relative mx-auto flex max-w-3xl items-end gap-0.5 pl-1 pr-20 sm:px-1">
        {SECTIONS.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            data-tutorial={`nav:${item.to}`}
            className={({ isActive }) => tab(isActive)}
          >
            <span className="relative">
              <NavIcon name={item.icon} />
              {badge(attention[item.to] ?? 0)}
            </span>
            <span className="w-full truncate text-center">{t(`common:nav.${item.label}`)}</span>
          </NavLink>
        ))}
      </div>
      {/* Мапа світу — кругла кнопка в правому куті екрана; на самій мапі вона веде назад у селище (рішення 21.10.2026) */}
      <Link
        to={onMap ? "/" : "/map"}
        data-tutorial={onMap ? undefined : "nav:/map"}
        aria-label={onMap ? t("common:nav.village") : t("common:nav.worldMap")}
        className="absolute bottom-1.5 right-3 z-10 flex h-16 w-16 shrink-0 flex-col items-center justify-center rounded-full bg-gradient-to-b from-[#5b85c4] to-[#24467f] text-[10px] font-bold text-white shadow-lg ring-4 ring-[#5b85c4] hover:brightness-110"
      >
        <NavIcon name={onMap ? "village" : "map"} size={30} />
        {onMap ? t("common:nav.village") : t("common:nav.map")}
      </Link>
    </nav>
  );
}
