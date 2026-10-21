import { NavLink } from "react-router-dom";

/** Вкладки будівлі «Ринок» (рішення 19.10.2026): крамниця, банери й ринок гравців — одна будівля. */
const TABS = [
  { to: "/shop", label: "Крамниця" },
  { to: "/banners", label: "Банери" },
  { to: "/market", label: "Ринок гравців" },
] as const;

export default function MarketTabs() {
  return (
    <nav className="flex gap-1 rounded-xl bg-[#1d3b6f] p-1 shadow ring-1 ring-[#5b85c4]">
      {TABS.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to}
          className={({ isActive }) =>
            `flex-1 rounded-lg px-3 py-1.5 text-center text-sm font-semibold ${
              isActive ? "bg-gradient-to-b from-[#5b85c4] to-[#24467f] text-white shadow" : "text-sky-100/80 hover:text-white"
            }`
          }
        >
          {tab.label}
        </NavLink>
      ))}
    </nav>
  );
}
