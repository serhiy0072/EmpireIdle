import { NavLink } from "react-router-dom";
import { NavIcon } from "./icons";

/**
 * Розділи панелі — усі на виду, без меню «Ще»; на них же вказує навчання (data-tutorial="nav:…").
 * Військо, звіринець, данжі й ринок відкриваються з їхніх будівель у селі (рішення 19.10.2026),
 * чат — смужкою над панеллю (ChatTicker).
 */
const SECTIONS = [
  { to: "/", label: "Село", icon: "village" },
  { to: "/heroes", label: "Герої", icon: "heroes" },
  { to: "/tower", label: "Вежа", icon: "tower" },
  { to: "/quests", label: "Квести", icon: "quests" },
  { to: "/inventory", label: "Рюкзак", icon: "inventory" },
  { to: "/clan", label: "Клан", icon: "clan" },
  { to: "/mail", label: "Скринька", icon: "mail" },
  { to: "/rating", label: "Рейтинг", icon: "rating" },
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
            end={item.to === "/"}
            data-tutorial={`nav:${item.to}`}
            className={({ isActive }) => tab(isActive)}
          >
            <span className="relative">
              <NavIcon name={item.icon} />
              {badge(attention[item.to] ?? 0)}
            </span>
            <span className="w-full truncate text-center">{item.label}</span>
          </NavLink>
        ))}
      </div>
      {/* Мапа світу — кругла кнопка в правому куті екрана */}
      <NavLink
        to="/map"
        data-tutorial="nav:/map"
        aria-label="Мапа світу"
        className={({ isActive }) =>
          `absolute bottom-1.5 right-3 z-10 flex h-16 w-16 shrink-0 flex-col items-center justify-center rounded-full bg-gradient-to-b text-[10px] font-bold text-white shadow-lg ring-4 ${
            isActive ? "from-amber-300 to-amber-600 ring-amber-200" : "from-[#5b85c4] to-[#24467f] ring-[#5b85c4] hover:brightness-110"
          }`
        }
      >
        <NavIcon name="map" size={30} />
        Мапа
      </NavLink>
    </nav>
  );
}
