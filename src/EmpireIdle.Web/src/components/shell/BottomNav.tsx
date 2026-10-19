import { useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { NavIcon } from "./icons";

/** Головні розділи — завжди на панелі; на них же вказує навчання (data-tutorial="nav:…"). */
const MAIN = [
  { to: "/", label: "Село", icon: "village" },
  { to: "/army", label: "Військо", icon: "army" },
  { to: "/heroes", label: "Герої", icon: "heroes" },
  { to: "/map", label: "Мапа", icon: "map" },
  { to: "/tower", label: "Вежа", icon: "tower" },
  { to: "/quests", label: "Квести", icon: "quests" },
  { to: "/inventory", label: "Рюкзак", icon: "inventory" },
] as const;

/** Решта — у меню «Ще», щоб панель не розповзалась на телефоні. */
const MORE = [
  { to: "/beasts", label: "Звіринець", emoji: "🐺" },
  { to: "/dungeons", label: "Данжі", emoji: "🕳️" },
  { to: "/banners", label: "Банери", emoji: "🚩" },
  { to: "/shop", label: "Крамниця", emoji: "🛒" },
  { to: "/market", label: "Ринок", emoji: "⚖️" },
  { to: "/clan", label: "Клан", emoji: "🛡️" },
  { to: "/rating", label: "Рейтинг", emoji: "🏆" },
  { to: "/chat", label: "Чат", emoji: "💬" },
  { to: "/mail", label: "Скринька", emoji: "✉️" },
] as const;

interface Props {
  /** Скільки чекає в кожному розділі (шлях → кількість), з useAttention. */
  attention: Record<string, number>;
}

/**
 * Нижня панель розділів, як у мобільних стратегіях (рішення 19.10.2026): значки з підписом,
 * активний розділ піднятий і світліший. «Ще» відкриває сітку решти розділів над панеллю.
 */
export default function BottomNav({ attention }: Props) {
  const location = useLocation();
  const [more, setMore] = useState(false);

  const inMore = MORE.some((item) => location.pathname.startsWith(item.to));
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
  const waitingInMore = MORE.reduce((sum, item) => sum + (attention[item.to] ?? 0), 0);

  const tab = (active: boolean) =>
    `relative flex min-w-0 flex-1 flex-col items-center gap-0.5 rounded-t-xl px-1 pb-1.5 pt-1 text-[11px] font-semibold transition ${
      active ? "-mt-2 bg-gradient-to-b from-[#5b85c4] to-[#24467f] pt-2 text-white" : "text-sky-100/80 hover:text-white"
    }`;

  return (
    <nav className="shrink-0 border-t-2 border-[#5b85c4] bg-gradient-to-b from-[#1d3b6f] to-[#0f2147] [text-shadow:0_1px_1px_#0008]">
      <div className="relative mx-auto flex max-w-3xl items-end gap-1 px-1">
        {more && (
          <div className="absolute bottom-full right-1 z-50 mb-3 grid w-[min(22rem,calc(100vw-1rem))] grid-cols-3 gap-2 rounded-2xl bg-[#1d3b6f] p-3 shadow-2xl ring-2 ring-[#5b85c4]">
            {MORE.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                data-tutorial={`nav:${item.to}`}
                onClick={() => setMore(false)}
                className={({ isActive }) =>
                  `relative flex flex-col items-center gap-1 rounded-xl px-2 py-2 text-xs font-semibold text-white ${
                    isActive ? "bg-[#5b85c4]" : "bg-[#24467f] hover:bg-[#335c9a]"
                  }`
                }
              >
                <span className="text-2xl">{item.emoji}</span>
                {item.label}
                {badge(attention[item.to] ?? 0)}
              </NavLink>
            ))}
          </div>
        )}

        {MAIN.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            data-tutorial={`nav:${item.to}`}
            onClick={() => setMore(false)}
            className={({ isActive }) => tab(isActive)}
          >
            <span className="relative">
              <NavIcon name={item.icon} />
              {badge(attention[item.to] ?? 0)}
            </span>
            <span className="w-full truncate text-center">{item.label}</span>
          </NavLink>
        ))}
        <button
          type="button"
          onClick={() => setMore((open) => !open)}
          aria-expanded={more}
          className={tab(more || inMore)}
        >
          <span className="relative">
            <NavIcon name="more" />
            {badge(waitingInMore)}
          </span>
          <span className="w-full truncate text-center">Ще</span>
        </button>
      </div>
    </nav>
  );
}
