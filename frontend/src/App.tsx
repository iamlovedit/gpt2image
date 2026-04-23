import { Navigate, Route, Routes } from "react-router-dom";
import AdminLayout from "./layouts/AdminLayout";
import LoginPage from "./pages/LoginPage";
import DashboardPage from "./pages/DashboardPage";
import AccountsPage from "./pages/AccountsPage";
import KeysPage from "./pages/KeysPage";
import LogsPage from "./pages/LogsPage";
import ModelMappingPage from "./pages/ModelMappingPage";
import SettingsPage from "./pages/SettingsPage";
import ProfilePage from "./pages/ProfilePage";
import DocsPage from "./pages/DocsPage";
import { useAuthStore } from "./stores/authStore";

function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = useAuthStore((s) => s.token);
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function RequireGuest({ children }: { children: React.ReactNode }) {
  const token = useAuthStore((s) => s.token);
  if (token) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

function RouteFallback() {
  const token = useAuthStore((s) => s.token);
  return <Navigate to={token ? "/dashboard" : "/"} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<DocsPage />} />
      <Route
        path="/login"
        element={
          <RequireGuest>
            <LoginPage />
          </RequireGuest>
        }
      />
      <Route
        element={
          <RequireAuth>
            <AdminLayout />
          </RequireAuth>
        }
      >
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="accounts" element={<AccountsPage />} />
        <Route path="keys" element={<KeysPage />} />
        <Route path="logs" element={<LogsPage />} />
        <Route path="models" element={<ModelMappingPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="profile" element={<ProfilePage />} />
      </Route>
      <Route path="*" element={<RouteFallback />} />
    </Routes>
  );
}
