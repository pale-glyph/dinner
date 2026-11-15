# Recipe Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Provide household-scoped recipe CRUD with Markdown as the canonical recipe content and structured data for search, planning, and future pantry matching. Agents can save and refine recipes directly.

## Responsibilities

- Store, retrieve, update, and delete household recipes.
- Store structured ingredients, quantities, units, substitutions, and dietary tags.
- Support search by ingredient, time, difficulty, cuisine, and constraints.
- Link recipes and ingredients to nutrition products or canonical nutrition references where available.
- Track immutable recipe revisions, source metadata, and originating agent-run/proposal IDs.
- Provide ingredient requirements for a serving count.

## Core entities

- `Recipe`: household ID, title, canonical Markdown, tags, cuisine, dietary restrictions, ordered steps, source, and current revision.
- `RecipeIngredient`: name, quantity, unit, optional linked pantry-item ID, nutrition-product ID, and unresolved state.
- `RecipeStep`: ordered structured instruction alongside the Markdown recipe.
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

Create, update, and delete requests are direct. Updates and deletes include the expected revision, including for agent mutations. A delete is a soft delete in the MVP so it remains auditable and can be restored during the retention period.

## MCP capabilities

Resources:

- `recipes://recipe/{recipeId}`
- `recipes://catalog`

Tools:

- `find_recipes`
- `get_recipe`
- `create_recipe`
- `update_recipe`
- `delete_recipe`

Recipe tools mutate recipes directly. Recipe search can filter by ingredient, cuisine, dietary restriction, and tag.

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
- Generated content must pass the same domain validation as a user-authored recipe; no approval workflow is required.
- Updating or deleting a recipe requires its expected current revision; a stale request returns a conflict rather than overwriting another edit.

## Acceptance criteria

- A user can manually create, update, search, and delete only their household recipes.
- An agent can create, update, and delete a structured recipe directly.
