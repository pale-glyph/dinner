# Dinner integration contracts

This directory is the source of truth for the initial versioned integration surface. Domain behaviour remains in each service; these files establish stable names before implementation begins.

- `conventions.md` defines HTTP, event, error, correlation, and MCP naming.
- `mcp-capabilities.json` records the initial MCP resource and tool names by service.
- `agent-platform-webhook.fixture.json` is a safe example payload for Agent Gateway development.

OpenAPI documents will be added alongside a service once its endpoints move beyond the M0 skeleton.
