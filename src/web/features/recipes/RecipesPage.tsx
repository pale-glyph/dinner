import { useState } from "react";
import { useHousehold } from "@/components/providers/HouseholdProvider";
import { RecipeDraftForm } from "@/features/recipes/components/RecipeDraftForm";
import { RecipeList } from "@/features/recipes/components/RecipeList";
import { useRecipes } from "@/features/recipes/hooks/useRecipes";
import type { Recipe } from "@/types/api";

export function RecipesPage() {
  const { household, loading } = useHousehold();
  const [editing, setEditing] = useState<Recipe | null>(null);
  const recipes = useRecipes(household?.id);
  if (loading) return <p className="status">Loading your household…</p>;
  if (!household)
    return <p className="status">Your household could not be loaded.</p>;
  const submit = async (recipe: Omit<Recipe, "id" | "revision">) => {
    const saved = await recipes.save({ ...recipe, householdId: household.id }, editing);
    if (saved) setEditing(null);
    return saved;
  };
  return (
    <>
      <section className="pantry-grid">
        <RecipeDraftForm editing={editing} onSubmit={submit} />
        <div>{recipes.status && <p className="status">{recipes.status}</p>}</div>
      </section>
      <RecipeList
        recipes={recipes.recipes}
        onEdit={setEditing}
        onRemove={(recipe) => {
          void recipes.remove(recipe);
        }}
        onRefresh={recipes.load}
      />
    </>
  );
}
