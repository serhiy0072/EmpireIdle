import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { register } from "../lib/auth";
import { describeError } from "../lib/errorMessages";
import { passwordIsValid, passwordRules } from "../lib/passwordRules";
import { userNameIsValid, userNameRules } from "../lib/userNameRules";

export default function RegisterPage() {
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [touched, setTouched] = useState(false);

  const mutation = useMutation({
    mutationFn: () => register(userName, email, password),
  });

  const mismatch = confirm.length > 0 && confirm !== password;
  const ready = userNameIsValid(userName) && email.length > 0 && passwordIsValid(password) && !mismatch;

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setTouched(true);

    if (!ready) return;

    mutation.mutate();
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-sm space-y-5 rounded-xl border border-slate-200 bg-white p-8 shadow-sm"
      >
        <h1 className="text-xl font-medium text-slate-800">Реєстрація в EmpireIdle</h1>

        <div>
          <label htmlFor="userName" className="mb-1 block text-sm text-slate-600">
            Ім'я
          </label>
          <input
            id="userName"
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
            required
            maxLength={50}
            placeholder="Як вас називати"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />

          {/* Правило видно одразу, як і для пароля: інакше гравець дізнався б про нього з відмови */}
          <ul className="mt-2 space-y-1">
            {userNameRules.map((rule) => {
              const passed = rule.passed(userName);

              return (
                <li
                  key={rule.label}
                  className={`flex items-center gap-2 text-xs ${passed ? "text-emerald-700" : "text-slate-500"}`}
                >
                  <span aria-hidden>{passed ? "✓" : "•"}</span>
                  {rule.label}
                </li>
              );
            })}
          </ul>
        </div>

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
            className="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />

          {/* Правила видно одразу: інакше гравець дізнається про них лише з відмови */}
          <ul className="mt-2 space-y-1">
            {passwordRules.map((rule) => {
              const passed = rule.passed(password);

              return (
                <li
                  key={rule.label}
                  className={`flex items-center gap-2 text-xs ${passed ? "text-emerald-700" : "text-slate-500"}`}
                >
                  <span aria-hidden>{passed ? "✓" : "•"}</span>
                  {rule.label}
                </li>
              );
            })}
          </ul>
        </div>

        <div>
          <label htmlFor="confirm" className="mb-1 block text-sm text-slate-600">
            Повторіть пароль
          </label>
          <input
            id="confirm"
            type="password"
            value={confirm}
            onChange={(event) => setConfirm(event.target.value)}
            required
            className={`w-full rounded-lg border px-3 py-2 focus:outline-none focus:ring-2 ${
              mismatch ? "border-red-400 focus:ring-red-400" : "border-slate-300 focus:ring-emerald-500"
            }`}
          />
          {mismatch && <p className="mt-1 text-xs text-red-600">Паролі не збігаються</p>}
        </div>

        {touched && !ready && !mismatch && (
          <p className="text-sm text-slate-500">Заповніть усі поля й виконайте вимоги до імені та пароля.</p>
        )}

        {mutation.isError && <p className="text-sm text-red-600">{describeError(mutation.error)}</p>}

        <button
          type="submit"
          disabled={mutation.isPending}
          className="w-full rounded-lg bg-emerald-600 py-2 font-medium text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {mutation.isPending ? "Створюємо…" : "Створити акаунт"}
        </button>

        <p className="text-center text-sm text-slate-500">
          Уже маєте акаунт?{" "}
          <Link to="/login" className="font-medium text-emerald-700 hover:underline">
            Увійти
          </Link>
        </p>
      </form>
    </div>
  );
}
