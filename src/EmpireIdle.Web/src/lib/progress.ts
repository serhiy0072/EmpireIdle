/** Частка 0…1 без NaN: порожня ціль (max ≤ 0) — повна смуга, а не порожнеча. */
export function progressShare(value: number, max: number): number {
  if (!(max > 0)) return 1;
  const share = value / max;
  return Number.isFinite(share) ? Math.min(1, Math.max(0, share)) : 0;
}
