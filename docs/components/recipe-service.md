# Recipe Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Provide household-scoped recipe CRUD, generated-recipe draft validation, and structured ingredient data. The service is the authority that turns an approved agent proposal into a saved recipe.

## Responsibilities

- Store, retrieve, update, and delete household recipes.
- Store structured ingredients, quantities, units, substitutions, and dietary tags.
- Support search by ingredient, time, difficulty, cuisine, and constraints.
- Link recipes and ingredients to nutrition products or canonical nutrition references where available.
- Track immutable recipe revisions, source metadata, and originating agent-run/proposal IDs.
- Provide ingredient requirements for a serving count.

## Core entities

- `Recipe`: household ID, title, description, instructions, times, servings, source, and current revision.
- `RecipeIngredient`: canonical ingredient, quantity, unit, optionality, and preparation note.
- `RecipeTag`: cuisine, dietary tag, allergen, equipment, and meal type.
- `RecipeVersion`: immutable revision and publication status.
- `RecipeNutritionProjection`: calculated nutrients per serving, source version, and calculation status.

## API

```text
GET    /api/v1/recipes
POST   /api/v1/recipes
GET    /api/v1/recipes/{recipeId}
PATCH  /api/v1/recipes/{recipeId}
DELETE /api/v1/recipes/{recipeId}
POST   /api/v1/recipes/{recipeId}/scale
POST   /api/v1/recipes/search
```

Create, update, and delete requests include an actor/source (`user` or `agent`) and, for an agent mutation, an approval reference and expected revision. A delete is a soft delete in the MVP so it remains auditable and can be restored during the retention period.

## MCP capabilities

Resources:

- `recipes://recipe/{recipeId}`
- `recipes://catalog`

Tools:

- `find_recipes`
- `get_recipe`
- `scale_recipe`
- `get_recipe_ingredients`
- `get_recipe_nutrition`
- `propose_recipe_create`
- `propose_recipe_update`
- `propose_recipe_delete`
- `apply_approved_recipe_proposal`

Proposal tools do not mutate recipes. `apply_approved_recipe_proposal` requires a Dinner approval reference, an idempotency key, and an expected revision for updates/deletes. Recipe search results should include structured match reasons so the agent can explain recommendations.

## Events

- `RecipeCreated.v1`
- `RecipeUpdated.v1`
- `RecipePublished.v1`
- `RecipeDeleted.v1`

## Business rules

- Every ingredient must have a canonical identity or an explicit unresolved state.
- Recipe scaling must preserve unit compatibility and mark approximate conversions.
- Allergy tags are safety metadata and must not be inferred solely from an LLM response.
- Nutrition totals must identify whether they are calculated from structured recipe ingredients, sourced from a product, or unavailable.
- Imported or user-provided recipe content must retain source attribution.
- Generated draft content is untrusted until it passes domain validation and the user approves it.
- Updating or deleting a recipe requires its expected current revision; a stale request returns a conflict rather than overwriting another edit.

## Acceptance criteria

- A user can manually create, update, search, and delete only their household recipes.
- An agent can generate a structured recipe draft and propose a create, update, or delete without mutating saved data.
- An approved agent proposal produces one auditable, idempotent recipe command.
