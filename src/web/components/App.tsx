import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "@/components/layout/AppShell";
import { HouseholdProvider } from "@/components/providers/HouseholdProvider";
import { PantryPage } from "@/features/pantry/PantryPage";
import { RecipeChatPage } from "@/features/recipe-chat/RecipeChatPage";
import { RecipesPage } from "@/features/recipes/RecipesPage";

export function App() {
  return (
    <HouseholdProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/pantry" replace />} />
            <Route path="pantry" element={<PantryPage />} />
            <Route path="assistant" element={<RecipeChatPage />} />
            <Route path="recipes" element={<RecipesPage />} />
            <Route path="*" element={<Navigate to="/pantry" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </HouseholdProvider>
  );
}
