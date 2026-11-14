# Recipe Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Provide searchable recipes and structured ingredient data for meal planning, pantry matching, and shopping-list generation.

## Responsibilities

- Store and retrieve recipes.
- Store structured ingredients, quantities, units, substitutions, and dietary tags.
- Support search by ingredient, time, difficulty, cuisine, and constraints.
- Link recipes and ingredients to nutrition products or canonical nutrition references where available.
- Track recipe versions and source metadata.
- Provide ingredient requirements for a serving count.

## Core entities

- `Recipe`: title, description, instructions, times, servings, and source.
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
POST   /api/v1/recipes/{recipeId}/scale
POST   /api/v1/recipes/search
```

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

Recipe search results should include structured match reasons so the agent can explain recommendations.

## Events

- `RecipeCreated.v1`
- `RecipeUpdated.v1`
- `RecipePublished.v1`

## Business rules

- Every ingredient must have a canonical identity or an explicit unresolved state.
- Recipe scaling must preserve unit compatibility and mark approximate conversions.
- Allergy tags are safety metadata and must not be inferred solely from an LLM response.
- Nutrition totals must identify whether they are calculated from structured recipe ingredients, sourced from a product, or unavailable.
- Imported or user-provided recipe content must retain source attribution.

## Acceptance criteria

- Meal Planning can retrieve recipes matching household constraints.
- Shopping List can obtain complete ingredient requirements for a serving count.
- Agents receive enough structured metadata to explain why a recipe was selected.
