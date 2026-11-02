import { formatMoment } from "./format";
/** Кінець щита після падіння міста — так, як його бачить гравець: день і година. */
export function shieldUntilLabel(iso: string): string {
  return formatMoment(iso);
}

/** Чи діє щит зараз. Сервер віддає лише чинний, але сторінка могла простояти відкритою. */
export function isShieldActive(iso: string | null | undefined): iso is string {
  return iso != null && new Date(iso).getTime() > Date.now();
}
