import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./AppShell";
import { PantryPage } from "./PantryPage";
import { RecipeChatPage } from "./RecipeChatPage";
import { RecipesPage } from "./RecipesPage";
import { HouseholdProvider } from "./HouseholdProvider";

export function App() {
  return <HouseholdProvider><BrowserRouter><Routes><Route element={<AppShell />}><Route index element={<Navigate to="/pantry" replace />} /><Route path="pantry" element={<PantryPage />} /><Route path="assistant" element={<RecipeChatPage />} /><Route path="recipes" element={<RecipesPage />} /><Route path="*" element={<Navigate to="/pantry" replace />} /></Route></Routes></BrowserRouter></HouseholdProvider>;
}
