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
  nutritionProductId?: string | null;
  unresolved?: boolean;
};

export type Recipe = {
  id: string;
  title: string;
  description?: string;
  instructions: string;
  totalMinutes: number;
  servings: number;
  tags: string[];
  ingredients: Ingredient[];
  revision: number;
};

export type Proposal = { id: string; recipe?: { title: string }; kind: string };

export type AgentRun = {
  id: string;
  status: string;
  events: string[];
  threadId?: string;
  createdAt: string;
  hasPendingQueuedMessage?: boolean;
};
