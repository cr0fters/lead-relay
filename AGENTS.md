# AGENTS.md

## Purpose
This file defines the default engineering standards for all contributors (human and AI agents) in this repository.

## Scope
- Applies to all new code.
- Applies to modifications of existing code where practical.

## Core Standards
- Write tests for all new behavior.
- Keep code aligned with SOLID principles.
- Prefer simple, maintainable solutions (KISS, YAGNI).
- Avoid duplication (DRY).
- Keep boundaries clear between Domain, Application, Infrastructure, and Web layers.
- Keep the data model lean: prefer one canonical storage location per business fact.

## Data Modeling (Lean by Default)
- Do not duplicate the same business field across multiple tables unless there is a clear, documented reason.
- If duplication is intentionally introduced (for caching/read-model performance), document:
  - source of truth
  - sync/update strategy
  - acceptable staleness window
- For relationship modeling, prefer references (foreign keys) over copying profile/contact fields.
- When refactoring duplicated schema, include a safe data backfill/migration plan and regression tests.
- Treat schema simplification as a product requirement, not optional cleanup.

## Testing Requirements
- Every new feature or behavior change must include automated tests.
- Bug fixes must include a regression test.
- Tests should validate observable behavior, not private implementation details.
- Maintain or improve coverage in changed areas.
- Use:
  - `tests/LeadRelay.UnitTests` for unit-level logic.
  - `tests/LeadRelay.IntegrationTests` for cross-layer/data-access behavior.

## SOLID Guidance (Practical)
- `S`: One reason to change per class/module.
- `O`: Extend with new implementations before modifying stable logic.
- `L`: Preserve behavioral contracts when substituting implementations.
- `I`: Keep interfaces focused and minimal.
- `D`: Depend on abstractions in Application/Domain-facing logic.

## Code Quality Rules
- Favor small, composable methods and classes.
- Name things by domain intent, not technical shortcuts.
- Use explicit error handling and meaningful logging at boundaries.
- Do not introduce dead code, commented-out blocks, or speculative abstractions.
- Preserve backward compatibility for public endpoints/contracts unless explicitly changing them.

## Security and Configuration
- Never hardcode secrets or tokens.
- Use environment/configuration for sensitive values.
- Validate and sanitize external inputs at system boundaries.

## Real-World Product Usage
- Design and implement features with a complete real-user journey, not just technical endpoint coverage.
- New user-facing capabilities must be discoverable from an expected entry point in the UI (for example: navigation, CTA, or contextual link), not only via hidden routes.
- Prefer conventional UX patterns over custom/implicit behavior unless there is a strong product reason.
- Before closing a feature, validate: how a first-time user finds it, starts it, completes it, and recovers from failure.

## Authentication and Access UX
- Authentication flows must follow standard, expected patterns for the target user type.
- Avoid requiring users to manually know deep links and separately source opaque tokens unless explicitly intended for internal/admin-only workflows.
- For end-user auth flows, include core lifecycle paths where applicable:
  - sign in
  - sign out
  - credential/token recovery or reset
  - clear error states and retry paths
- If a shortcut auth mechanism is introduced for MVP speed, document it as temporary and create follow-up tasks for a production-grade flow.

## Pull Request / Change Checklist
- [ ] New/changed behavior has tests.
- [ ] Existing tests pass.
- [ ] Code follows SOLID and keeps layer boundaries clean.
- [ ] No duplicated logic introduced without clear reason.
- [ ] Documentation updated when behavior/config changes.
- [ ] User journey is realistic and discoverable from the UI.
- [ ] Auth flows follow standard expectations for the intended users.

## Working Agreement for Agents
- If a requested change cannot reasonably include tests, explain why and propose the smallest acceptable follow-up.
- Prefer incremental, reviewable changes over broad rewrites.
