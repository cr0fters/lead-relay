# AGENTS.md

## Purpose
This file defines the default engineering standards for all contributors (human and AI agents) in this repository.

## Scope
- Applies to all new code.
- Applies to modifications of existing code where practical.

## Before Making Changes
- Read this file, `TASKS.md`, and any documentation directly related to the requested area before editing.
- Inspect `git status` first. Existing changes belong to the user and must be preserved unless the user explicitly asks to replace or revert them.
- Trace the complete affected journey and its boundaries before choosing an implementation. For example, a billing change may affect signup, webhooks, access gating, email, account closure, and support—not only the billing page.
- Prefer the smallest coherent change that completes the requested outcome. Do not mix unrelated cleanup into a feature or fix.
- Confirm claims against the code. Do not present planned, partial, admin-only, or unverified behavior as a completed customer feature.

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
- Include negative, authorization, tenant-isolation, failure, retry/idempotency, and cancellation cases where relevant—not only the happy path.
- Treat provider webhooks and migrations as integration boundaries that require dedicated regression coverage.

## Local Verification Constraint
- Agents must not run `dotnet build` or `dotnet test` in this workspace because those commands hang in the local agent environment.
- Rely on GitHub Actions for the authoritative .NET build, test, and migration-validation run.
- Agents should still run safe non-.NET checks that are relevant to the change, such as `git diff --check`, JavaScript syntax checks, formatting checks that do not invoke a build, and focused read-only inspections.
- Never claim that the solution builds or tests pass unless a completed CI run or other trustworthy evidence proves it. State exactly what was and was not run.

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
- Use sentence case for user-facing UI labels and headings (for example: `Project summary`, not `Project Summary`), except for proper nouns, brand names, and acronyms.

## Security and Configuration
- Never hardcode secrets or tokens.
- Use environment/configuration for sensitive values.
- Validate and sanitize external inputs at system boundaries.
- Never print, log, commit, or paste plaintext production credentials, access tokens, webhook secrets, reset tokens, or payment data.
- Keep browser-supplied product IDs, price IDs, site IDs, redirect URLs, and authorization claims untrusted until verified server-side.
- Public webhook endpoints must verify signatures before processing, be idempotent, tolerate duplicate/out-of-order delivery, and avoid leaking provider payloads in errors or logs.
- Payment-card data must remain with Stripe-hosted surfaces; LeadRelay must not collect or store card details.

## Multi-Tenancy and Authorization
- Treat `SiteId` as a tenant boundary, not proof of authorization.
- Scope every tenant-owned read, write, export, webhook lookup, background action, and billing operation to the authenticated/resolved site.
- Never accept a route, query, form, webhook, or metadata site identifier without verifying it against the authenticated owner or trusted provider mapping.
- Add tenant-isolation regression tests whenever a new tenant-owned entity or endpoint is introduced.
- Preserve access to safe recovery paths such as sign-in, billing management, export, and account support when adding subscription or feature gating.

## External Providers
- Treat Meta/WhatsApp, Stripe, Postmark, OpenAI, Railway, and other provider calls as unreliable boundaries.
- Use bounded timeouts, cancellation, safe retry/backoff only where operations are idempotent, and actionable user-facing recovery states.
- Verify provider signatures and identifiers using official SDKs/documentation where practical.
- Keep provider API versions, webhook event coverage, permissions, and credential-rotation requirements documented and visible in `TASKS.md`.
- Do not perform live external mutations, send messages/emails, change billing, or rotate credentials unless the user explicitly authorizes that action.

## Real-World Product Usage
- Design and implement features with a complete real-user journey, not just technical endpoint coverage.
- New user-facing capabilities must be discoverable from an expected entry point in the UI (for example: navigation, CTA, or contextual link), not only via hidden routes.
- Prefer conventional UX patterns over custom/implicit behavior unless there is a strong product reason.
- Before closing a feature, validate: how a first-time user finds it, starts it, completes it, and recovers from failure.
- Keep onboarding non-technical. Opaque provider identifiers, raw tokens, or admin intervention are not acceptable as the primary customer journey.
- Keep marketing, onboarding, billing, legal, and in-product wording consistent with the behavior that is actually shipped.
- Use accessible labels, keyboard-operable controls, useful empty/loading/error states, and responsive layouts for user-facing work.

## Public Contracts and Data Changes
- Treat public endpoints, webhook formats, embedded widget URLs/runtime files, cookies, and configuration keys as compatibility contracts.
- Avoid breaking existing website embeds. Version or cache-bust widget runtime changes deliberately and document rollback/compatibility behavior.
- Database schema changes must include a migration, safe handling of existing data, indexes/constraints where appropriate, and rollback or forward-fix consideration.
- Prefer additive, deploy-safe migrations. Do not assume application and database versions switch atomically.
- Update runbooks and environment-variable documentation when configuration or deployment behavior changes.

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
- [ ] Negative, authorization, tenant-isolation, and failure cases are covered where relevant.
- [ ] CI confirms the build, tests, and migrations, or the absence of that confirmation is explicitly reported.
- [ ] Code follows SOLID and keeps layer boundaries clean.
- [ ] No duplicated logic introduced without clear reason.
- [ ] Documentation updated when behavior/config changes.
- [ ] `TASKS.md` accurately reflects completed work and newly discovered follow-ups.
- [ ] User journey is realistic and discoverable from the UI.
- [ ] Auth flows follow standard expectations for the intended users.
- [ ] Marketing and legal claims remain accurate.
- [ ] Full-diff code review completed with no unresolved findings.

## Required Code Review After Every Change
- After editing, inspect the entire resulting diff—not only the lines most recently touched.
- Run `git diff --check` and any relevant safe non-.NET validation.
- Review the diff for:
  - correctness, edge cases, null/empty inputs, concurrency, cancellation, and partial failure
  - authentication, authorization, tenant isolation, CSRF, signature validation, idempotency, secret/PII leakage, and abuse controls
  - data ownership, migrations, constraints, compatibility, deploy ordering, and rollback/forward-fix safety
  - provider failure behavior, timeouts, retries, duplicate events, and stale/out-of-order events
  - customer journey, accessibility, responsive behavior, honest wording, and recovery states
  - tests, documentation, configuration, monitoring, and `TASKS.md` alignment
- Fix every actionable finding, then repeat the review on the new full diff. Continue until the review finds no unresolved correctness, security, or launch-safety issue.
- If a concern cannot be resolved within scope, report it clearly with severity, impact, and the smallest safe follow-up. Do not silently waive it.
- In the final handoff, summarize the outcome, files changed, verification performed, CI status, and any remaining risks. Do not claim a clean review if known findings remain.

## Working Agreement for Agents
- If a requested change cannot reasonably include tests, explain why and propose the smallest acceptable follow-up.
- Prefer incremental, reviewable changes over broad rewrites.
- Keep `TASKS.md` current when work completes or the repository review reveals a material missing requirement. Do not mark an item complete until the implementation and required verification exist.
- Do not create commits, push branches, open pull requests, deploy, or mutate live provider state unless the user explicitly asks for that action.
- Before an explicitly requested commit or push, confirm the intended scope, inspect the final diff and status, complete the required review, and avoid including unrelated user changes.
- Never force-push, rewrite shared history, or use destructive Git commands unless the user explicitly requests it and the exact scope has been verified.
- Preserve backward compatibility by default. When a breaking change is required, explain the migration path before implementation.
