import { useState } from "react";
import { useHousehold } from "@/components/providers/HouseholdProvider";
import { ProposalReview } from "@/features/recipes/components/ProposalReview";
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
  const submit = async (recipe: Omit<Recipe, "id" | "revision">) =>
    recipes.propose({ ...recipe, householdId: household.id }, editing);
  const approve = async () => {
    if (await recipes.approve()) setEditing(null);
  };
  return (
    <>
      <section className="pantry-grid">
        <RecipeDraftForm editing={editing} onSubmit={submit} />
        <ProposalReview
          proposal={recipes.proposal}
          pending={recipes.pending}
          status={recipes.status}
          onSelect={recipes.setProposal}
          onApprove={() => {
            void approve();
          }}
        />
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
