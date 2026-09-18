/** Відповідь про помилку з беку: ProblemDetails плюс наші розширення. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  traceId?: string;
  /** Стабільний код для розгалуження в UI. Текст Detail для цього не годиться. */
  errorCode?: string;
  /** Лише для errorCode === "NotEnoughResources". */
  resource?: string;
  need?: number;
  have?: number;
  /** ValidationProblemDetails: поле -> перелік повідомлень. */
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Запит провалився (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get errorCode(): string | undefined {
    return this.problem.errorCode;
  }

  is(code: string): boolean {
    return this.problem.errorCode === code;
  }

  /** Цифри нестачі ресурсу або null, якщо відмова інша. */
  get shortfall(): { resource: string; need: number; have: number } | null {
    const { errorCode, resource, need, have } = this.problem;

    if (errorCode !== "NotEnoughResources" || resource === undefined || need === undefined || have === undefined) {
      return null;
    }

    return { resource, need, have };
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}
