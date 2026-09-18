import { isApiError } from "./problem";

const RESOURCE_NAMES: Record<string, string> = {
  gold: "золота",
  wood: "деревини",
  stone: "каменю",
  iron: "заліза",
  food: "їжі",
  gems: "самоцвітів",
};

/**
 * Текст відмови для гравця. Розгалуження за errorCode, а не за Detail:
 * текст беку англійський і може змінитись будь-коли.
 */
export function describeError(error: unknown): string {
  if (!isApiError(error)) return "Не вдалося з'єднатися з сервером";

  const shortfall = error.shortfall;

  if (shortfall !== null) {
    const name = RESOURCE_NAMES[shortfall.resource] ?? shortfall.resource;
    return `Не вистачає ${name}: потрібно ${shortfall.need}, є ${shortfall.have}`;
  }

  if (error.is("RequirementNotMet")) return error.problem.detail ?? "Умову не виконано";
  if (error.is("OperationInProgress")) return "Дія вже виконується";
  if (error.is("ConcurrencyConflict")) return "Дані щойно змінились, спробуй ще раз";
  if (error.is("NotFound")) return "Об'єкт не знайдено";

  const fields = Object.values(error.fieldErrors).flat();

  return fields.length > 0 ? fields.join(" ") : error.message;
}
