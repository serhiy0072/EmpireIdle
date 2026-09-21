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
