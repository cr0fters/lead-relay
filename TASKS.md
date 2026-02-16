# LeadRelay Tasks

## Current focus
- Prioritize self-serve product features to reach a true MVP users can onboard themselves.
- Keep critical hardening work visible and scheduled (do not defer indefinitely).

## Priority 0: next build cycle (self-serve MVP)
- [x] Implement self-serve account registration flow (no manual admin provisioning required).
- [ ] Build guided onboarding wizard after signup:
  - connect WhatsApp account
  - capture/store `phone_number_id` and required identifiers
  - validate webhook configuration and connectivity
  - send and verify a test WhatsApp message
- [ ] Add onboarding progress UI/checklist in app:
  - account created
  - WhatsApp connected
  - webhook verified
  - first lead captured
- [ ] Add account bootstrap completion flow for new signups (set initial password via secure link).
- [ ] Add important notification emails (starting with new lead notifications).

## Priority 1: monetization and gating
- [ ] Add basic billing state model (`trialing`, `active`, `past_due`, `canceled`).
- [ ] Add trial logic (`TrialEndsAtUtc`) and enforce server-side access rules.
- [ ] Add simple billing/status UI in authenticated app shell.
- [ ] Decide and implement trial start trigger (recommended: after onboarding completion or first lead captured).

## Priority 1: production hardening required for dogfood/early customers
- [x] Implement real transactional email sender (Postmark).
- [ ] Add basic rate limiting for webhook and lead intake endpoints (by site + sender/contact).
- [ ] Add minimal production monitoring/alerts for:
  - webhook receive failures
  - outbound message failures
  - password reset email failures
- [ ] Run one end-to-end production-like dry run and document rollback steps.
- [ ] Add health checks for DB and upstream dependencies with degraded status.
- [ ] Review logs/errors for token/secret leakage and redact where needed.

## Priority 2: post-MVP stabilization
- [ ] Improve WhatsApp attribution model beyond "first site wins" for multi-site readiness.
- [ ] Version and cache-bust widget runtime assets on every release.
- [ ] Add backup/restore runbook and execute one restore drill.

## Completed foundation
- [x] Persistent storage + migrations in place.
- [x] DB-backed repositories wired in.
- [x] Admin site config API/UI implemented.
- [x] Admin token protection middleware implemented.
- [x] Login area and lead workspace implemented.
- [x] Password reset flow with Postmark template support implemented.
- [x] Secrets required in non-development and documented.
- [x] CI pipeline runs tests and validates migrations.
- [x] Lean lead/customer/project modeling refactor completed.
- [x] Integration and regression tests added for key changed areas.

## MVP exit criteria
- [ ] A new user can complete this journey without manual intervention:
  1. sign up and create account
  2. complete WhatsApp onboarding in-product
  3. install widget on allowed domain
  4. capture first lead with correct site attribution
  5. receive notification and sign in to the workspace
  6. reply successfully over chosen channel
  7. see meaningful errors/alerts when failures occur
- [ ] Billing/trial gating behaves as expected for trial and paid states.
- [ ] One backup and restore drill completed successfully.
