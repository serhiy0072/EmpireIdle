import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { login } from "../lib/auth";
import { describeError } from "../lib/errorMessages";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);

  // Сесію виставляє login(): маршрути перемкнуться самі, редирект тут не потрібен
  const mutation = useMutation({
    mutationFn: () => login(email, password, rememberMe),
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    mutation.mutate();
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm space-y-5 rounded-xl border border-slate-200 bg-white p-8 shadow-sm"
      >
        <h1 className="text-xl font-medium text-slate-800">Вхід в EmpireIdle</h1>

        <div>
          <label htmlFor="email" className="mb-1 block text-sm text-slate-600">
            Email
          </label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoComplete="email"
            placeholder="Введіть ваш email"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>

        <div>
          <label htmlFor="password" className="mb-1 block text-sm text-slate-600">
            Пароль
          </label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            autoComplete="current-password"
            placeholder="Введіть ваш пароль"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>

        <label className="flex select-none items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={rememberMe}
            onChange={(event) => setRememberMe(event.target.checked)}
            className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500"
          />
          Запам'ятати мене
        </label>

        {mutation.isError && <p className="text-sm text-red-600">{describeError(mutation.error)}</p>}

        <button
          type="submit"
          disabled={mutation.isPending}
          className="w-full rounded-lg bg-emerald-600 py-2 font-medium text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {mutation.isPending ? "Входимо…" : "Вхід"}
        </button>

        <p className="text-center text-sm text-slate-500">
          Немає акаунта?{" "}
          <Link to="/register" className="font-medium text-emerald-700 hover:underline">
            Зареєструватись
          </Link>
        </p>
      </form>
    </div>
  );
}
