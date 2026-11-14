# Integration conventions

## HTTP APIs

- Use HTTPS and JSON.
- Version public routes under `/api/v1`.
- Use plural, kebab-case resource segments, for example `/api/v1/pantry/items`.
- Return RFC 7807 problem details for errors.
- Accept and return `X-Correlation-ID`; create one when it is absent.
- Mutation endpoints that can be retried accept `Idempotency-Key`.

## Events

Publish versioned PascalCase event types: `<Aggregate><PastTense>.v1`, for example `PantryItemUpdated.v1`.

All events use this envelope:

```json
{
  "eventType": "PantryItemUpdated.v1",
  "eventId": "uuid",
  "occurredAt": "2026-11-14T00:00:00Z",
  "householdId": "uuid",
  "correlationId": "uuid",
  "data": {}
}
```

## MCP

- Resource URIs use a lower-case service scheme, for example `pantry://items`.
- Tool names use lower snake case, for example `search_pantry`.
- Write tools expose a proposal operation separately from the approved commit operation.
- Tool schemas include a household identifier and correlation identifier where applicable.
