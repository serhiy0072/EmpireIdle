import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AppLayout from "../components/AppLayout";
import { useSession } from "../hooks/useSession";
import LoginPage from "../pages/LoginPage";
import VillagePage from "../pages/VillagePage";

/**
 * Без сесії доступний лише логін. Перевірка тут, а не в кожній сторінці:
 * новий маршрут усередині лейауту захищений автоматично.
 */
export default function AppRoutes() {
  const session = useSession();

  return (
    <BrowserRouter>
      <Routes>
        {session === null ? (
          <>
            <Route path="/login" element={<LoginPage />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </>
        ) : (
          <Route element={<AppLayout />}>
            <Route path="/" element={<VillagePage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        )}
      </Routes>
    </BrowserRouter>
  );
}
