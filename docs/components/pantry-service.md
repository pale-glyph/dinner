# Pantry Service Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core domain service

## Purpose

Maintain the household’s current food inventory and provide reliable stock, quantity, freshness, and expiry information to users and agents.

## Responsibilities

- Create, update, consume, remove, and restore pantry items.
- Track quantity, unit, ingredient identity, location, purchase date, and expiry date.
- Normalise common ingredient names and units.
- Optionally associate pantry items with a barcode or Nutrition Data Service product reference.
- Report low-stock, missing, and expiring items.
- Provide transaction history for inventory changes.

## Core entities

- `PantryItem`: ingredient, quantity, unit, location, status, and dates.
- `IngredientReference`: canonical ingredient and aliases.
- `PantryAdjustment`: signed quantity change and reason.
- `StockRule`: optional household threshold and replenishment preference.
- `PantryProductLink`: pantry item, product identifier, match method, and confidence.

## API

```text
GET    /api/v1/pantry/items
POST   /api/v1/pantry/items
GET    /api/v1/pantry/items/{itemId}
PATCH  /api/v1/pantry/items/{itemId}
POST   /api/v1/pantry/items/{itemId}/consume
POST   /api/v1/pantry/items/{itemId}/restore
GET    /api/v1/pantry/alerts
GET    /api/v1/pantry/history
```

## MCP capabilities

Resources:

- `pantry://items`
- `pantry://expiring`
- `pantry://low-stock`

Tools:

- `search_pantry`
- `get_pantry_item`
- `propose_pantry_adjustments`
- `apply_pantry_adjustments`

Nutrition lookups are delegated to the Nutrition Data Service; Pantry stores only the selected product reference and match metadata.

`apply_pantry_adjustments` must be idempotent and require an approval reference when invoked by an agent.

## Events

- `PantryItemAdded.v1`
- `PantryItemUpdated.v1`
- `PantryItemConsumed.v1`
- `PantryItemRemoved.v1`
- `PantryItemExpiring.v1`

## Business rules

- Quantities cannot become negative unless an explicit correction workflow is used.
- Units must be compatible before quantities are combined.
- Expired items remain visible in history but are excluded from available-stock recommendations by default.
- Consumption must record the source, such as manual action, meal plan, or agent proposal.

## Acceptance criteria

- A user can manage pantry items without losing adjustment history.
- An agent can answer what is available, expiring, or low in stock.
- Pantry data can be used to calculate meal-plan and shopping-list requirements.
