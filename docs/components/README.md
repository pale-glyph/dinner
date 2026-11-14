# Dinner component specifications

This directory contains the detailed specification for each Dinner component. The system-wide product and architecture context is in [`../dinner-spec.md`](../dinner-spec.md).

| Component | Specification |
| --- | --- |
| Next.js Web App | [`nextjs-web-app.md`](nextjs-web-app.md) |
| Household Service | [`household-service.md`](household-service.md) |
| Pantry Service | [`pantry-service.md`](pantry-service.md) |
| Recipe Service | [`recipe-service.md`](recipe-service.md) |
| Nutrition Data Service | [`nutrition-data-service.md`](nutrition-data-service.md) |
| Meal Planning Service | [`meal-planning-service.md`](meal-planning-service.md) |
| Shopping List Service | [`shopping-list-service.md`](shopping-list-service.md) |
| Agent Gateway | [`agent-gateway.md`](agent-gateway.md) |
| Notification Service | [`notification-service.md`](notification-service.md) |

All backend services are C# / ASP.NET Core services. Domain services expose normal application APIs to the frontend and MCP capabilities to the in-house agent platform. Services own their data and do not access another service’s database directly.
