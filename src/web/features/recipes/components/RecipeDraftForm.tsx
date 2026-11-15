import { useState, type FormEvent } from "react";
import type { Ingredient, Recipe } from "@/types/api";

type RecipeDraft = Omit<Recipe, "id" | "revision">;

export function RecipeDraftForm({
  editing,
  onSubmit,
}: {
  editing: Recipe | null;
  onSubmit: (recipe: RecipeDraft) => Promise<boolean>;
}) {
  const [title, setTitle] = useState(editing?.title ?? "");
  const [instructions, setInstructions] = useState(editing?.instructions ?? "");
  const [ingredients, setIngredients] = useState(
    editing
      ? editing.ingredients
          .map((item) => `${item.name}|${item.quantity}|${item.unit}`)
          .join("\n")
      : "Tinned tomatoes|2|item\nChickpeas|1|item",
  );
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await onSubmit({
      title,
      instructions,
      totalMinutes: 30,
      servings: 2,
      tags: ["dinner"],
      ingredients: parseIngredients(ingredients),
    });
  };
  return (
    <div>
      <h2>{editing ? "Edit recipe" : "Recipe draft"}</h2>
      <form onSubmit={submit}>
        <label>
          Title
          <input
            required
            value={title}
            onChange={(event) => setTitle(event.target.value)}
          />
        </label>
        <label>
          Instructions
          <textarea
            required
            value={instructions}
            onChange={(event) => setInstructions(event.target.value)}
          />
        </label>
        <label>
          Ingredients <span>one per line: name|quantity|unit</span>
          <textarea
            required
            value={ingredients}
            onChange={(event) => setIngredients(event.target.value)}
          />
        </label>
        <button>{editing ? "Propose update" : "Propose recipe"}</button>
      </form>
    </div>
  );
}

function parseIngredients(value: string): Ingredient[] {
  return value
    .split("\n")
    .filter(Boolean)
    .map((line) => {
      const [name, quantity, unit] = line.split("|");
      return {
        name: name?.trim() ?? "",
        quantity: Number(quantity),
        unit: unit?.trim() ?? "",
      };
    });
}
