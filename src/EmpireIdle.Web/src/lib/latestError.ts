/** Мінімум, який потрібен від мутації React Query, щоб знайти останню. */
interface Submitted {
  submittedAt: number;
  error: unknown;
}

/**
 * Помилка останньої з дій на екрані. Перша-ліпша помилка зі списку мутацій бреше: стара відмова однієї кнопки
 * перекривала б свіжий успіх чи свіжу відмову іншої.
 */
export function latestError(mutations: readonly Submitted[]): unknown {
  if (mutations.length === 0) return null;

  return mutations.reduce((last, mutation) => (mutation.submittedAt > last.submittedAt ? mutation : last)).error;
}
