import { useCallback, useEffect, useState } from "react";
import { recipeApi } from "@/lib/api";
import type { Proposal, Recipe } from "@/types/api";

export type RecipeDraft = Omit<Recipe, "id" | "revision"> & {
  householdId: string;
};

export function useRecipes(householdId?: string) {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [pending, setPending] = useState<Proposal[]>([]);
  const [status, setStatus] = useState("");

  const load = useCallback(async () => {
    if (!householdId) return;
    const [recipeResponse, proposalResponse] = await Promise.all([
      fetch(`${recipeApi}/api/v1/recipes?householdId=${householdId}`),
      fetch(`${recipeApi}/api/v1/recipes/proposals?householdId=${householdId}`),
    ]);
    if (recipeResponse.ok) setRecipes(await recipeResponse.json());
    if (proposalResponse.ok) setPending(await proposalResponse.json());
  }, [householdId]);

  useEffect(() => {
    void load().catch(() => setStatus("Recipe service is unavailable."));
  }, [load]);

  const propose = async (recipe: RecipeDraft, editing?: Recipe | null) => {
    const body = editing
      ? { recipeId: editing.id, expectedRevision: editing.revision, recipe }
      : recipe;
    const endpoint = editing
      ? "propose_recipe_update"
      : "propose_recipe_create";
    const response = await fetch(`${recipeApi}/mcp/tools/${endpoint}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      setStatus(
        "The recipe draft needs a title, instructions, servings, and valid ingredients.",
      );
      return false;
    }
    const result = await response.json();
    setProposal(result.structuredContent);
    setStatus("Recipe proposal ready for your approval.");
    return true;
  };

  const approve = async () => {
    if (!proposal) return false;
    const response = await fetch(
      `${recipeApi}/mcp/tools/apply_approved_recipe_proposal`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          proposalId: proposal.id,
          approvalReference: `ui:${proposal.id}`,
          idempotencyKey: `ui:${proposal.id}`,
        }),
      },
    );
    if (!response.ok) {
      setStatus("Could not apply that proposal.");
      return false;
    }
    setProposal(null);
    await load();
    setStatus("Recipe saved.");
    return true;
  };

  const remove = async (recipe: Recipe) => {
    if (
      !householdId ||
      !window.confirm(
        `Delete ${recipe.title}? This requires explicit confirmation.`,
      )
    )
      return;
    const response = await fetch(
      `${recipeApi}/api/v1/recipes/${recipe.id}?householdId=${householdId}&expectedRevision=${recipe.revision}`,
      { method: "DELETE" },
    );
    if (!response.ok) {
      setStatus("Could not delete this recipe because it changed.");
      return;
    }
    await load();
  };

  return {
    recipes,
    proposal,
    pending,
    status,
    setProposal,
    load,
    propose,
    approve,
    remove,
  };
}
