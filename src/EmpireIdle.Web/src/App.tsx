import AppRoutes from "./app/AppRoutes";
import { useRealtime } from "./hooks/useRealtime";

export default function App() {
  // Хук викликається завжди, а підключення піднімається лише за наявної сесії
  useRealtime();

  return <AppRoutes />;
}