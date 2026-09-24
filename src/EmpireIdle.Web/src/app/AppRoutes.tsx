import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AppLayout from "../components/AppLayout";
import { useSession } from "../hooks/useSession";
import ArmyPage from "../pages/ArmyPage";
import ArtifactSetsPage from "../pages/ArtifactSetsPage";
import BannersPage from "../pages/BannersPage";
import ChatPage from "../pages/ChatPage";
import ClanPage from "../pages/ClanPage";
import DungeonsPage from "../pages/DungeonsPage";
import ForgePage from "../pages/ForgePage";
import HeroCodexPage from "../pages/HeroCodexPage";
import HeroesPage from "../pages/HeroesPage";
import InventoryPage from "../pages/InventoryPage";
import LoginPage from "../pages/LoginPage";
import MapPage from "../pages/MapPage";
import MarketPage from "../pages/MarketPage";
import QuestsPage from "../pages/QuestsPage";
import RatingPage from "../pages/RatingPage";
import RegisterPage from "../pages/RegisterPage";
import ShopPage from "../pages/ShopPage";
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
            <Route path="/heroes/codex" element={<HeroCodexPage />} />
            <Route path="/dungeons" element={<DungeonsPage />} />
            <Route path="/inventory" element={<InventoryPage />} />
            <Route path="/inventory/sets" element={<ArtifactSetsPage />} />
            <Route path="/forge" element={<ForgePage />} />
            <Route path="/banners" element={<BannersPage />} />
            <Route path="/clan" element={<ClanPage />} />
            <Route path="/shop" element={<ShopPage />} />
            <Route path="/market" element={<MarketPage />} />
            <Route path="/chat" element={<ChatPage />} />
            <Route path="/rating" element={<RatingPage />} />
            <Route path="/quests" element={<QuestsPage />} />
            <Route path="/map" element={<MapPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        )}
      </Routes>
    </BrowserRouter>
  );
}
