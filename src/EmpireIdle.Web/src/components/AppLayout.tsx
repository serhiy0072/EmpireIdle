import { useMutation } from "@tanstack/react-query";
import { useQueryClient } from "@tanstack/react-query";
import { Outlet } from "react-router-dom";
import { useSession } from "../hooks/useSession";
import { logout } from "../lib/auth";
import { seedAccount } from "../lib/dev";
import { describeError } from "../lib/errorMessages";
import { useVillage } from "../lib/queries/village";
import ResourceBar from "./ResourceBar";

export default function AppLayout() {
  const session = useSession();
  const queryClient = useQueryClient();
  const playerId = session?.playerId ?? "";
  const village = useVillage(playerId);

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

        {seed.isError && (
          <p className="bg-red-50 px-4 py-2 text-sm text-red-700">{describeError(seed.error)}</p>
        )}
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
