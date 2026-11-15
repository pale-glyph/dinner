import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "@/components/layout/AppShell";
import { HouseholdProvider, useHousehold } from "@/components/providers/HouseholdProvider";
import { PantryPage } from "@/features/pantry/PantryPage";
import { RecipeChatPage } from "@/features/recipe-chat/RecipeChatPage";
import { RecipesPage } from "@/features/recipes/RecipesPage";
import { HouseholdOnboarding } from "./HouseholdOnboarding";
import { HouseholdsPage } from "./HouseholdsPage";
import { JoinHouseholdPage } from "./JoinHouseholdPage";

function ApplicationRoutes() {
  const { loading, households } = useHousehold();

  if (loading) return <div className="app-loading">Loading your households…</div>;

  return (
    <Routes>
      <Route path="join/:token" element={<JoinHouseholdPage />} />
      {households.length === 0 ? (
        <Route path="*" element={<HouseholdOnboarding />} />
      ) : (
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/pantry" replace />} />
          <Route path="pantry" element={<PantryPage />} />
          <Route path="assistant" element={<RecipeChatPage />} />
          <Route path="recipes" element={<RecipesPage />} />
          <Route path="households" element={<HouseholdsPage />} />
          <Route path="*" element={<Navigate to="/pantry" replace />} />
        </Route>
      )}
    </Routes>
  );
}

export function App() {
  return (
    <HouseholdProvider>
      <BrowserRouter>
        <ApplicationRoutes />
      </BrowserRouter>
    </HouseholdProvider>
  );
}
