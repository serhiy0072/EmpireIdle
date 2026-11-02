import AppRoutes from "./app/AppRoutes";
import { useRealtime } from "./hooks/useRealtime";
import { useLanguage } from "./lib/i18n";

export default function App() {
  // Хук викликається завжди, а підключення піднімається лише за наявної сесії
  useRealtime();
  const language = useLanguage();

  // Зміна мови перемонтовує екрани: числа й дати форматуються поза хуками й інакше лишилися б старою мовою.
  // Realtime живе вище і не перепідключається
  return <AppRoutes key={language} />;
}
