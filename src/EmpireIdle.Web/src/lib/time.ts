/**
 * Залишок до серверного моменту у вигляді "г:хх:сс" або "хх:сс".
 * Рахуємо від completesAt, а не від годинника клієнта: розбіжність
 * годинників лише зсуває відлік, але не міняє момент завершення.
 */
export function formatRemaining(completesAt: string, now: number): string {
  const seconds = Math.max(0, Math.round((Date.parse(completesAt) - now) / 1_000));

  const hours = Math.floor(seconds / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const rest = seconds % 60;

  const pad = (value: number) => value.toString().padStart(2, "0");

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

/**
 * TimeSpan з .NET приходить рядком "[d.]hh:mm:ss[.fffffff]". Повертає мілісекунди;
 * NaN — рядок не схожий на TimeSpan (тоді краще показати "?", ніж 0:00).
 */
export function parseTimeSpan(value: string): number {
  const match = /^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(value);

  if (match === null) return Number.NaN;

  const [, days, hours, minutes, seconds] = match;

  return (
    (Number(days ?? 0) * 86_400 + Number(hours) * 3_600 + Number(minutes) * 60 + Number(seconds)) * 1_000
  );
}

/** Тривалість у вигляді "г:хх:сс" або "хх:сс" — той самий формат, що й у зворотних відліків. */
export function formatDuration(ms: number): string {
  if (Number.isNaN(ms)) return "?";

  return formatRemaining(new Date(ms).toISOString(), 0);
}
