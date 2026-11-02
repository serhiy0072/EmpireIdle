import { explainError } from "../lib/errorMessages";
import { useCatalog } from "../lib/queries/catalog";

interface Props {
  error: unknown;
}

/** Єдиний вигляд відмов на всіх екранах. null і undefined нічого не малюють. */
export default function ErrorBanner({ error }: Props) {
  const catalog = useCatalog();

  if (error === null || error === undefined) return null;

  const message = explainError(error, catalog);

  return (
    <div
      role="alert"
      className={`rounded-lg px-3 py-2 text-sm ${
        message.actionable ? "bg-amber-50 text-amber-900" : "bg-red-50 text-red-700"
      }`}
    >
      {message.text}
      {/* traceId лише для технічних збоїв: із ним збій знаходиться в логах за секунди */}
      {message.traceId !== undefined && (
        <span className="ml-2 font-mono text-xs opacity-60">{message.traceId}</span>
      )}
    </div>
  );
}
