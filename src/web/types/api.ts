export type Household = { id: string; name: string; role: string };

export type PantryItem = {
  id: string;
  name: string;
  quantity: number;
  unit: string;
  location?: string;
  expiryDate?: string;
  lowStockThreshold?: number;
};

export type Ingredient = {
  name: string;
  quantity: number;
  unit: string;
  pantryItemId?: string | null;
  nutritionProductId?: string | null;
  unresolved?: boolean;
};

export type RecipeStep = { order: number; instruction: string };

export type Recipe = {
  id: string;
  title: string;
  markdown: string;
  tags: string[];
  cuisine?: string | null;
  dietaryRestrictions: string[];
  instructions: RecipeStep[];
  ingredients: Ingredient[];
  revision: number;
};

export type AgentRun = {
  id: string;
  status: string;
  events: string[];
  threadId?: string;
  createdAt: string;
  hasPendingQueuedMessage?: boolean;
};
