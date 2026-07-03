import { useState, type FormEvent } from "react";
import { login } from "../lib/auth";
import { ApiError } from "../lib/api";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [playerId, setPlayerId] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const auth = await login(email, password, rememberMe);
      setPlayerId(auth.playerId);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Не вдалося з'єднатися з сервером");
    } finally {
      setLoading(false);
    }
  }

  if (playerId) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <p className="text-lg text-slate-700">
          Увійшли. playerId: <span className="font-mono">{playerId}</span>
        </p>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <form onSubmit={handleSubmit} className="w-full max-w-sm bg-white rounded-xl shadow-sm border border-slate-200 p-8 space-y-5">
        <h1 className="text-xl font-medium text-slate-800">Вхід в EmpireIdle</h1>

        <div>
          <label htmlFor="email" className="block text-sm text-slate-600 mb-1">Email</label>
          <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required
            placeholder="Введіть ваш email"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500" />
        </div>

        <div>
          <label htmlFor="password" className="block text-sm text-slate-600 mb-1">Пароль</label>
          <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required
            placeholder="Введіть ваш пароль"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500" />
        </div>

        <div className="flex items-center justify-between">
          <label className="flex items-center gap-2 text-sm text-slate-600 select-none">
            <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)}
              className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
            Запам'ятати мене
          </label>
          <button type="button" className="text-sm text-emerald-700 hover:underline">
            Забули пароль?
          </button>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}

        <button type="submit" disabled={loading}
          className="w-full rounded-lg bg-emerald-600 text-white py-2 font-medium hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed">
          {loading ? "Входимо…" : "Вхід"}
        </button>
      </form>
    </div>
  );
}