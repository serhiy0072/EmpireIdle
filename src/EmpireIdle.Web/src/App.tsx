import { useSession } from "./hooks/useSession";
import { logout } from "./lib/auth";
import LoginPage from "./pages/LoginPage";

export default function App() {
  const session = useSession();

  if (session === null) {
    return <LoginPage />;
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-4 bg-slate-50">
      <p className="text-lg text-slate-700">
        Увійшли. playerId: <span className="font-mono">{session.playerId}</span>
      </p>
      <button
        type="button"
        onClick={logout}
        className="rounded-lg border border-slate-300 px-4 py-2 text-slate-700 hover:bg-slate-100"
      >
        Вийти
      </button>
    </div>
  );
}
