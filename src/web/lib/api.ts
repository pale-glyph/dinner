import type { AgentRun, PantryItem, Recipe } from "@/types/api";

export const pantryApi =
  process.env.NEXT_PUBLIC_PANTRY_API_URL ?? "http://localhost:18083";
export const recipeApi =
  process.env.NEXT_PUBLIC_RECIPE_API_URL ?? "http://localhost:18084";
export const agentApi =
  process.env.NEXT_PUBLIC_AGENT_GATEWAY_API_URL ?? "http://localhost:18081";

export type { AgentRun, PantryItem, Recipe };
