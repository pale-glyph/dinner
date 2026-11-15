export const pantryApi = process.env.NEXT_PUBLIC_PANTRY_API_URL ?? "http://localhost:18083";
export const recipeApi = process.env.NEXT_PUBLIC_RECIPE_API_URL ?? "http://localhost:18084";
export const agentApi = process.env.NEXT_PUBLIC_AGENT_GATEWAY_API_URL ?? "http://localhost:18081";
export const householdId = "11111111-1111-1111-1111-111111111111";

export type PantryItem = { id: string; name: string; quantity: number; unit: string; location?: string; expiryDate?: string; lowStockThreshold?: number };
export type Ingredient = { name: string; quantity: number; unit: string; nutritionProductId?: string | null; unresolved?: boolean };
export type Recipe = { id: string; title: string; description?: string; instructions: string; totalMinutes: number; servings: number; tags: string[]; ingredients: Ingredient[]; revision: number };
export type Proposal = { id: string; recipe?: { title: string }; kind: string };
export type AgentRun = { id: string; status: string; events: string[]; threadId?: string; createdAt: string; hasPendingQueuedMessage?: boolean };
