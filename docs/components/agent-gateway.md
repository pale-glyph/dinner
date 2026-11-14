# Agent Gateway Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core integration service

## Purpose

Provide the Dinner-facing HTTP and webhook boundary for conversations and agent runs on the in-house agent platform.

## Responsibilities

- Start and continue platform chat threads for agent conversations.
- Send scoped household/profile context to the platform.
- Receive and validate platform webhooks.
- Persist run status, proposals, tool actions, approvals, and errors.
- Expose frontend-friendly status and approval endpoints.
- Correlate frontend requests, platform requests, MCP calls, and domain events.

## External interface

```text
POST /api/v1/agent/conversations
POST /api/v1/agent/conversations/{conversationId}/messages
GET  /api/v1/agent/conversations/{conversationId}
POST /api/v1/agent/proposals/{proposalId}/approve
POST /api/v1/agent/proposals/{proposalId}/reject
GET  /api/v1/agent/runs/{runId}
POST /api/v1/agent/webhooks/events
```

The gateway starts a platform thread at `POST /api/projects/{projectId}/agents/{agentId}/threads`, keeps the returned thread ID, and polls its timeline from the last processed sequence. The webhook endpoint validates the HMAC-SHA-256 signature over the raw request body, deduplicates the event ID, durably records the event, and only then returns `2xx`.

## Platform contract

The gateway must support:

- Thread creation, timeline polling, and at most one pending queued message.
- `agent.message` and `agent.tool_call` webhook handling with the platform's delivery semantics.
- Proposal extraction from agent messages/tool results; Dinner-owned approval or rejection of the associated recipe command.
- Event-ID deduplication, sequence ordering, reconciliation, timeout, retry, and dead-letter handling.

## MCP registration

The gateway stores or resolves the MCP server registry used by each environment. The agent platform receives the appropriate MCP endpoint set for the active household context.

## Security and safety

- Do not expose platform credentials to the browser.
- Validate household/profile context before creating a run.
- Keep write proposals separate from committed domain changes.
- Require explicit approval references for destructive or bulk mutations.
- Redact prompts and tool payloads according to configured data-sensitivity rules.

## Events

- `AgentRunStarted.v1`
- `AgentRunProgressed.v1`
- `AgentProposalCreated.v1`
- `AgentRunCompleted.v1`
- `AgentRunFailed.v1`

## Acceptance criteria

- The frontend can start a run and receive progress through polling or server-sent updates.
- Platform webhooks are authenticated, idempotent, and traceable.
- A proposal can be approved or rejected exactly once.
- The gateway exposes an auditable run history without exposing secrets.
