# Shopping List Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Maintain actionable shopping lists generated manually, from pantry rules, or from meal-plan requirements.

## Responsibilities

- Create and edit shopping lists.
- Add, merge, group, defer, and complete list items.
- Normalise ingredient names and compatible units.
- Reconcile required meal-plan ingredients with pantry stock.
- Preserve item origin and explanation metadata.

## Core entities

- `ShoppingList`: household, name, status, and target date.
- `ShoppingItem`: ingredient, quantity, unit, category, status, and origin.
- `ItemSource`: manual, pantry, meal plan, or agent proposal.
- `ListRevision`: changes and actor/run metadata.

## API

```text
GET    /api/v1/shopping-lists
POST   /api/v1/shopping-lists
GET    /api/v1/shopping-lists/{listId}
PATCH  /api/v1/shopping-lists/{listId}
POST   /api/v1/shopping-lists/{listId}/items
PATCH  /api/v1/shopping-lists/{listId}/items/{itemId}
POST   /api/v1/shopping-lists/{listId}/items/{itemId}/complete
POST   /api/v1/shopping-lists/from-meal-plan
```

## MCP capabilities

Resources:

- `shopping://lists`
- `shopping://list/{listId}`

Tools:

- `create_shopping_list_draft`
- `merge_shopping_list_items`
- `add_shopping_items`
- `complete_shopping_item`
- `commit_shopping_list_changes`

Agent-created lists and bulk changes should be proposed before commit.

## Business rules

- Duplicate items may be merged only when ingredient identity and units are compatible.
- Completed items remain in history and can be restored.
- Meal-plan-derived items retain links to the source plan and assignment where possible.
- Pantry stock is a recommendation input; the user can override the calculated quantity.

## Acceptance criteria

- A committed meal plan can produce a shopping-list draft.
- Duplicate compatible ingredients are merged with a visible explanation.
- Users can edit or reject any AI-generated list item before commit.
