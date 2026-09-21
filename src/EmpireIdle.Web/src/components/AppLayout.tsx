import { useMutation } from "@tanstack/react-query";
import { useQueryClient } from "@tanstack/react-query";
import { Outlet } from "react-router-dom";
import { useSession } from "../hooks/useSession";
import { logout } from "../lib/auth";
import { seedAccount } from "../lib/dev";
import { useVillage } from "../lib/queries/village";
import { useWallet } from "../lib/queries/wallet";
import { NavLink } from "react-router-dom";
import ResourceBar from "./ResourceBar";
import ErrorBanner from "./ErrorBanner";

export default function AppLayout() {
  const session = useSession();
  const queryClient = useQueryClient();
  const playerId = session?.playerId ?? "";
  const village = useVillage(playerId);
  const wallet = useWallet(playerId);

  const seed = useMutation({
    mutationFn: () => seedAccount(playerId),
    // Сід наливає все одразу: точково інвалідувати нема сенсу
    onSuccess: () => void queryClient.invalidateQueries(),
  });

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <ResourceBar village={village.data} />

          <div className="flex items-center gap-2">
            <span className="rounded-full bg-violet-100 px-3 py-1 text-sm text-violet-800">
              💎 <span className="font-medium">{(wallet.data?.gemBalance ?? 0).toLocaleString("uk-UA")}</span>
            </span>

            {import.meta.env.DEV && (
              <button
                type="button"
                onClick={() => seed.mutate()}
                disabled={seed.isPending}
                className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-1 text-sm text-amber-800 hover:bg-amber-100 disabled:opacity-50"
              >
                {seed.isPending ? "Наливаємо…" : "Сід"}
              </button>
            )}

            <button
              type="button"
              onClick={logout}
              className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-100"
            >
              Вийти
            </button>
          </div>
        </div>

                  <nav className="flex gap-1">
            {[
              { to: "/", label: "Село" },
              { to: "/heroes", label: "Герої" },
            ].map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end
                className={({ isActive }) =>
                  `rounded-lg px-3 py-1 text-sm ${isActive ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

        <ErrorBanner error={seed.error} />
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
