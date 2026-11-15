import { useCallback, useEffect, useState } from "react";
import { recipeApi } from "@/lib/api";
import type { Recipe } from "@/types/api";

export type RecipeDraft = Omit<Recipe, "id" | "revision"> & { householdId: string };

export function useRecipes(householdId?: string) {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [status, setStatus] = useState("");
  const load = useCallback(async () => {
    if (!householdId) return;
    const response = await fetch(`${recipeApi}/api/v1/recipes?householdId=${householdId}`);
    if (!response.ok) throw new Error("Recipe service is unavailable.");
    setRecipes(await response.json());
  }, [householdId]);
  useEffect(() => { void load().catch(() => setStatus("Recipe service is unavailable.")); }, [load]);

  const save = async (recipe: RecipeDraft, editing?: Recipe | null) => {
    const endpoint = editing ? `${recipeApi}/api/v1/recipes/${editing.id}?expectedRevision=${editing.revision}` : `${recipeApi}/api/v1/recipes`;
    const response = await fetch(endpoint, { method: editing ? "PATCH" : "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(recipe) });
    if (!response.ok) { setStatus("The recipe needs a title, Markdown, steps, and valid ingredients."); return false; }
    await load(); setStatus("Recipe saved."); return true;
  };
  const remove = async (recipe: Recipe) => {
    if (!householdId) return;
    const response = await fetch(`${recipeApi}/api/v1/recipes/${recipe.id}?householdId=${householdId}&expectedRevision=${recipe.revision}`, { method: "DELETE" });
    if (!response.ok) { setStatus("Could not delete this recipe because it changed."); return; }
    await load(); setStatus("Recipe deleted.");
  };
  return { recipes, status, load, save, remove };
}
