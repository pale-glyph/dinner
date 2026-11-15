import { useEffect, useState, type FormEvent } from "react";
import type { Ingredient, Recipe, RecipeStep } from "@/types/api";

type RecipeDraft = Omit<Recipe, "id" | "revision">;

export function RecipeDraftForm({ editing, onSubmit }: { editing: Recipe | null; onSubmit: (recipe: RecipeDraft) => Promise<boolean> }) {
  const [title, setTitle] = useState(editing?.title ?? ""); const [markdown, setMarkdown] = useState(editing?.markdown ?? ""); const [cuisine, setCuisine] = useState(editing?.cuisine ?? "");
  const [tags, setTags] = useState(editing?.tags.join(", ") ?? "dinner"); const [dietaryRestrictions, setDietaryRestrictions] = useState(editing?.dietaryRestrictions.join(", ") ?? "");
  const [instructions, setInstructions] = useState(formatSteps(editing?.instructions)); const [ingredients, setIngredients] = useState(formatIngredients(editing?.ingredients));
  useEffect(() => { setTitle(editing?.title ?? ""); setMarkdown(editing?.markdown ?? ""); setCuisine(editing?.cuisine ?? ""); setTags(editing?.tags.join(", ") ?? "dinner"); setDietaryRestrictions(editing?.dietaryRestrictions.join(", ") ?? ""); setInstructions(formatSteps(editing?.instructions)); setIngredients(formatIngredients(editing?.ingredients)); }, [editing]);
  const submit = async (event: FormEvent) => { event.preventDefault(); await onSubmit({ title, markdown, cuisine: cuisine.trim() || null, tags: splitValues(tags), dietaryRestrictions: splitValues(dietaryRestrictions), instructions: parseSteps(instructions), ingredients: parseIngredients(ingredients) }); };
  return <div><h2>{editing ? "Edit recipe" : "New recipe"}</h2><form onSubmit={submit}>
    <label>Title<input required value={title} onChange={(event) => setTitle(event.target.value)} /></label><label>Recipe Markdown<textarea required value={markdown} onChange={(event) => setMarkdown(event.target.value)} /></label><label>Cuisine<input value={cuisine} onChange={(event) => setCuisine(event.target.value)} /></label><label>Tags <span>comma separated</span><input value={tags} onChange={(event) => setTags(event.target.value)} /></label><label>Dietary restrictions <span>comma separated</span><input value={dietaryRestrictions} onChange={(event) => setDietaryRestrictions(event.target.value)} /></label><label>Steps <span>one per line</span><textarea required value={instructions} onChange={(event) => setInstructions(event.target.value)} /></label><label>Ingredients <span>one per line: name|quantity|unit</span><textarea required value={ingredients} onChange={(event) => setIngredients(event.target.value)} /></label><button>{editing ? "Save changes" : "Save recipe"}</button>
  </form></div>;
}
function splitValues(value: string) { return value.split(",").map((item) => item.trim()).filter(Boolean); }
function formatSteps(steps?: RecipeStep[]) { return steps?.map((step) => step.instruction).join("\n") ?? ""; }
function formatIngredients(ingredients?: Ingredient[]) { return ingredients?.map((item) => `${item.name}|${item.quantity}|${item.unit}`).join("\n") ?? "Tinned tomatoes|2|item\nChickpeas|1|item"; }
function parseSteps(value: string): RecipeStep[] { return value.split("\n").map((instruction, index) => ({ order: index + 1, instruction: instruction.trim() })).filter((step) => step.instruction); }
function parseIngredients(value: string): Ingredient[] { return value.split("\n").filter(Boolean).map((line) => { const [name, quantity, unit] = line.split("|"); return { name: name?.trim() ?? "", quantity: Number(quantity), unit: unit?.trim() ?? "" }; }); }
