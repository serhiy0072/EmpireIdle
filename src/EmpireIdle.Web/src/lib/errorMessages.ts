import { isApiError } from "./problem";
import { refusalText } from "./refusals";
import { resourceGenitive } from "./resourceNames";

export interface ErrorMessage {
  text: string;
  /** Відмова гравцю зрозуміла й виправна. Технічні збої показуються інакше. */
  actionable: boolean;
  traceId?: string;
}

/**
 * Відмова в текст для гравця.
 *
 * Розгалуження за errorCode, а не за Detail: текст беку англійський.
 * Невідомий код і помилки валідації запиту — це дефект клієнта, а не дія
 * гравця, тож назовні йде нейтральне повідомлення, а деталі в консоль.
 */
export function explainError(error: unknown): ErrorMessage {
  if (!isApiError(error)) {
    console.error("Мережева помилка", error);
    return { text: "Не вдалося з'єднатися з сервером", actionable: true };
  }

  const traceId = error.problem.traceId;
  const shortfall = error.shortfall;

  if (shortfall !== null) {
    return {
      text: `Не вистачає ${resourceGenitive(shortfall.resource)}: потрібно ${shortfall.need.toLocaleString("uk-UA")}, є ${shortfall.have.toLocaleString("uk-UA")}`,
      actionable: true,
    };
  }

  // Причина з сервера — власний текст для гравця з підставленими параметрами
  const refusal = refusalText(error.problem.reason, error.problem.args);

  if (refusal !== null) {
    return { text: refusal, actionable: true };
  }

  // Відмова без відомої причини: англійський Detail гравцю не показуємо — лише в консоль
  if (error.is("RequirementNotMet")) {
    console.warn("Відмова без тексту для гравця", error.problem);
    return { text: "Зараз це недоступно", actionable: true };
  }

  if (error.is("OperationInProgress")) {
    return { text: "Дія вже виконується", actionable: true };
  }

  if (error.is("StaleTurn")) {
    return { text: "Бій уже пішов далі — оновіть сторінку", actionable: true };
  }

  if (error.is("ConcurrencyConflict")) {
    return { text: "Дані щойно змінились, спробуйте ще раз", actionable: true };
  }

  if (error.is("AlreadyExists")) {
    return { text: "Це вже існує", actionable: true };
  }

  if (error.is("NotFound")) {
    return { text: "Об'єкт не знайдено", actionable: true };
  }

  if (error.is("AuthenticationFailed")) {
    return { text: "Невірна пошта або пароль", actionable: true };
  }

  // Валідація запиту — помилка в клієнті: гравець її виправити не може
  console.error("Запит відхилено сервером", error.problem);

  return { text: "Щось пішло не так. Спробуйте ще раз", actionable: false, traceId };
}

/** Короткий варіант для місць, де потрібен лише рядок. */
export function describeError(error: unknown): string {
  return explainError(error).text;
}
