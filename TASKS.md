# LeadRelay MVP Tasks

## Current MVP Readiness (critical view)
- Product flow: 70%
- Technical hardening: 45%
- Safe dogfood deploy readiness: 60%

Interpretation:
- Core loop works: widget -> inbound message -> lead capture -> owner inbox/reply.
- Main blockers are production hardening and reliability, not basic feature completeness.

## Must-do before dogfood go-live
- [ ] Lock down debug endpoints outside development (`/debug/*` should be disabled or admin-protected).
- [ ] Add WhatsApp webhook signature validation (`X-Hub-Signature-256`) using app secret.
- [ ] Add webhook idempotency for duplicate event delivery (store processed message IDs).
- [ ] Implement a real transactional email sender (password reset + owner lead notifications).
- [ ] Add retry/backoff for outbound WhatsApp sends with structured failure logging.
- [ ] Finalize owner account bootstrap flow (invite/initial password setup, operator runbook).
- [ ] Add basic rate limiting for webhook and lead intake endpoints (by site + sender/contact).
- [ ] Add minimal production monitoring/alerts for:
  - webhook receive failures
  - outbound message failures
  - password reset email failures
- [ ] Run one end-to-end production-like dry run and document rollback steps.

## Should-do soon after dogfood launch
- [ ] Improve WhatsApp attribution model beyond "first site wins" for multi-site readiness.
- [ ] Version and cache-bust widget runtime assets on every release.
- [ ] Add health checks for DB and upstream dependencies with degraded status.
- [ ] Add a backup/restore runbook and test restore once.
- [ ] Review logs/errors for token/secret leakage and redact where needed.

## Completed foundation
- [x] Persistent storage + migrations in place.
- [x] DB-backed repositories wired in.
- [x] Admin site config API/UI implemented.
- [x] Admin token protection middleware implemented.
- [x] Owner login area and lead workspace implemented.
- [x] Secrets required in non-development and documented.
- [x] CI pipeline runs tests and validates migrations.
- [x] Leaned lead/customer/project modeling refactor completed.

## Testing gaps to close
- [ ] Add integration tests in `tests/LeadRelay.IntegrationTests` (currently no discovered tests).
- [ ] Add regression tests for webhook signature + idempotency behavior.
- [ ] Add failure-path tests for outbound message retry logic.

## Dogfood go-live exit criteria
- [ ] You can complete this path without manual DB edits:
  1. user opens widget on your domain
  2. lead is captured with correct site attribution
  3. you receive owner notification
  4. you sign in to owner portal
  5. you reply successfully over chosen channel
  6. failures are visible via logs/alerts
- [ ] One backup and restore drill completed successfully.
