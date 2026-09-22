import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AppLayout from "../components/AppLayout";
import { useSession } from "../hooks/useSession";
import ArmyPage from "../pages/ArmyPage";
import HeroesPage from "../pages/HeroesPage";
import InventoryPage from "../pages/InventoryPage";
import LoginPage from "../pages/LoginPage";
import MapPage from "../pages/MapPage";
import QuestsPage from "../pages/QuestsPage";
import RegisterPage from "../pages/RegisterPage";
import VillagePage from "../pages/VillagePage";

/**
 * Без сесії доступні лише логін і реєстрація. Перевірка тут, а не в кожній
 * сторінці: новий маршрут усередині лейауту захищений автоматично.
 */
export default function AppRoutes() {
  const session = useSession();

  return (
    <BrowserRouter>
      <Routes>
        {session === null ? (
          <>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </>
        ) : (
          <Route element={<AppLayout />}>
            <Route path="/" element={<VillagePage />} />
            <Route path="/army" element={<ArmyPage />} />
            <Route path="/heroes" element={<HeroesPage />} />
            <Route path="/inventory" element={<InventoryPage />} />
            <Route path="/quests" element={<QuestsPage />} />
            <Route path="/map" element={<MapPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        )}
      </Routes>
    </BrowserRouter>
  );
}
