import { ApiError, type ProblemDetails } from "./problem";
import { fromAuthResponse, getSession, setSession, syncSessionFromStorage, type AuthResponse } from "./session";

export { ApiError, isApiError } from "./problem";

export const API_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? "";

export interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  /**
   * Команди з IIdempotentRequest: один ключ на виклик, і повтор після обриву
   * мережі йде з ним же — сервер віддає той самий результат, а не виконує вдруге.
   */
  idempotent?: boolean;
  signal?: AbortSignal;
}

/** Одна ротація на всіх: шість паралельних 401 не мають зробити шість рефрешів. */
let refreshing: Promise<boolean> | null = null;

/** Запас до закінчення: токен, що доживає останні секунди, оновлюємо заздалегідь. */
const EXPIRY_MARGIN_MS = 30_000;

/** Термін дії з claim exp. Підпис не перевіряємо — це робить сервер, нам потрібен лише час. */
function expiresAt(token: string): number | null {
  const payload = token.split(".")[1];

  if (payload === undefined) return null;

  try {
    const claims = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/"))) as { exp?: unknown };
    return typeof claims.exp === "number" ? claims.exp * 1_000 : null;
  } catch {
    return null;
  }
}

/**
 * Access-токен, що проживе ще щонайменше пів хвилини. Протухлий ротується
 * до запиту: і HTTP, і хаб отримують робочий токен з першої спроби.
 * null — сесії немає або рефреш не вдався.
 */
export async function freshAccessToken(): Promise<string | null> {
  const session = getSession();

  if (session === null) return null;

  const expiry = expiresAt(session.accessToken);

  if (expiry !== null && expiry - Date.now() > EXPIRY_MARGIN_MS) {
    return session.accessToken;
  }

  return (await refreshOnce()) ? (getSession()?.accessToken ?? null) : null;
}

/**
 * Паузи перед повторами команди з ключем ідемпотентності. Дві спроби понад
 * першу: довше гравець чекав би помилку, яку однаково побачить.
 */
const RETRY_DELAYS_MS = [500, 1_500];

/** Проксі не дочекався сервера — команда могла виконатись, а відповідь загубитись. */
const RETRYABLE_STATUSES = new Set([502, 503, 504]);

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  // Ключ на дію гравця, а не на HTTP-запит: повтор після обриву має прийти
  // з тим самим ключем, щоб сервер віддав збережений результат, а не списав
  // вдруге. Новий ключ на кожен запит робив би ідемпотентність декорацією
  const idempotencyKey = options.idempotent === true ? crypto.randomUUID() : null;

  const response = await sendWithRetry(path, options, idempotencyKey);

  // Сам рефреш не рефрешимо: інакше протухла пара зациклиться
  if (response.status === 401 && !path.startsWith("/api/auth/")) {
    if (await refreshOnce()) {
      return unwrap<T>(await sendWithRetry(path, options, idempotencyKey));
    }

    setSession(null);
  }

  return unwrap<T>(response);
}

export function apiGet<T>(path: string): Promise<T> {
  return api<T>(path);
}

export function apiPost<T>(path: string, body: unknown, options: RequestOptions = {}): Promise<T> {
  return api<T>(path, { ...options, method: "POST", body });
}

/**
 * Надсилає запит, а команду з ключем повторює, поки сервер міг її виконати,
 * але відповідь не дійшла. Без ключа не повторюємо нічого: повтор означав би
 * друге виконання.
 */
async function sendWithRetry(path: string, options: RequestOptions, idempotencyKey: string | null): Promise<Response> {
  for (let attempt = 0; ; attempt++) {
    const delay = idempotencyKey === null ? undefined : RETRY_DELAYS_MS[attempt];

    try {
      const response = await send(path, options, idempotencyKey);

      if (delay === undefined || !(await isRetryable(response))) return response;
    } catch (error: unknown) {
      // Скасований запит не повторюємо: його відкликав сам клієнт
      if (delay === undefined || options.signal?.aborted === true) throw error;
    }

    await new Promise((resolve) => window.setTimeout(resolve, delay));
  }
}

/**
 * 500 не повторюємо: сервер уже зняв резерв ключа, а збій здебільшого
 * детермінований. 409 OperationInProgress — той самий ключ ще виконується,
 * тож чекаємо на його результат.
 */
async function isRetryable(response: Response): Promise<boolean> {
  if (RETRYABLE_STATUSES.has(response.status)) return true;

  if (response.status !== 409) return false;

  const problem = parseJson(await response.clone().text()) as ProblemDetails | null;

  return problem?.errorCode === "OperationInProgress";
}

async function send(path: string, options: RequestOptions, idempotencyKey: string | null): Promise<Response> {
  const token = await freshAccessToken();
  const headers = new Headers({ Accept: "application/json" });

  if (token !== null) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (idempotencyKey !== null) {
    headers.set("Idempotency-Key", idempotencyKey);
  }

  return fetch(`${API_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  });
}

function refreshOnce(): Promise<boolean> {
  refreshing ??= rotate().finally(() => {
    refreshing = null;
  });

  return refreshing;
}

/**
 * Вкладки одного браузера ділять refresh-токен, а refreshOnce рятує лише в
 * межах однієї вкладки. Замок робить ротацію послідовною для всіх вкладок:
 * дві вкладки з тим самим токеном — це «повторне використання», і сервер
 * відкликав би всі сесії гравця. Без Web Locks (старий браузер) — як раніше.
 */
function rotate(): Promise<boolean> {
  // Токен запам'ятовуємо до черги за замком: поки чекаємо, подія storage уже
  // підмінить сесію на свіжу, і порівняння всередині замка нічого б не побачило
  const stale = getSession()?.refreshToken ?? null;

  return typeof navigator.locks === "undefined"
    ? rotateUnlocked(stale)
    : navigator.locks.request("empireidle.refresh", () => rotateUnlocked(stale));
}

async function rotateUnlocked(stale: string | null): Promise<boolean> {
  // Сусідня вкладка могла вже ротувати: беремо її пару замість того,
  // щоб пред'явити вже використаний токен
  const session = syncSessionFromStorage();

  if (session === null || stale === null) return false;

  if (session.refreshToken !== stale) return true;

  const response = await fetch(`${API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ refreshToken: session.refreshToken }),
  });

  if (!response.ok) return false;

  setSession(fromAuthResponse((await response.json()) as AuthResponse));
  return true;
}

async function unwrap<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload = parseJson(text);

  if (!response.ok) {
    // Бек завжди віддає ProblemDetails, але падати на відповіді проксі не варто
    throw new ApiError(response.status, (payload as ProblemDetails | null) ?? { status: response.status });
  }

  return payload as T;
}

/** HTML-сторінка від проксі чи порожнє тіло — це не JSON: віддаємо null, а не SyntaxError. */
function parseJson(text: string): unknown {
  if (text === "") return null;

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}
