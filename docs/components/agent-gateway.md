# Agent Gateway Specification

**Status:** Draft  
**Component type:** C# / ASP.NET Core integration service

## Purpose

Provide the Dinner-facing HTTP and webhook boundary for conversations and agent runs on the in-house agent platform.

## Responsibilities

- Start agent conversations and runs.
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
POST /api/v1/agent/runs/{runId}/approve
POST /api/v1/agent/runs/{runId}/reject
GET  /api/v1/agent/runs/{runId}
POST /api/v1/agent/webhooks/events
```

The webhook endpoint must validate the platform’s signature or shared-secret mechanism once that contract is available, reject replays, and acknowledge quickly before processing long-running events asynchronously.

## Platform contract

The gateway must support:

- Run creation with conversation ID, household context, user request, and available MCP servers.
- Progress, clarification, proposal, completion, and failure events.
- Approval or rejection callbacks.
- Idempotent event handling.
- Timeout, retry, and dead-letter handling.

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
