# LeadRelay manual actions

This is the short, owner-maintained checklist for work that cannot be completed safely from the repository alone. `TASKS.md` remains the detailed source of truth for the MVP roadmap; this file is the convenient view of decisions, accounts, credentials, legal review, and live-service setup that require human action.

Update this file when an autonomous MVP run discovers or completes a human action. Never add secrets or credential values.

## Do next

- [ ] Rotate the production database, admin-auth, owner-session signing, Postmark, and Meta/WhatsApp credentials exposed during the 2026-08-19 Railway diagnostic. Plan the WhatsApp credential-encryption-key rotation with data re-encryption or customer reconnects so stored sender credentials remain recoverable.
- [ ] Choose and provision a durable, access-restricted store for ASP.NET Core Data Protection keys (for example, a Railway volume or database-backed key ring) plus an at-rest protection mechanism; production currently warns that container-local keys disappear on redeploy.
- [x] In Railway, enable **Wait for CI** on the production service's GitHub deployment trigger and accept the updated GitHub permissions.
- [x] Check the latest GitHub Actions runs for `main` and confirm the build, tests, and migration validation pass.
- [ ] Replace the placeholder legal business name, address, company details, and contact email used by the terms and privacy pages.
- [ ] Create and activate the LeadRelay Stripe account and enable MFA.
- [x] Complete Meta business verification and the WhatsApp Embedded Signup/App Review setup.

## Product and commercial decisions

- [ ] Choose the default Meta Embedded Signup path: dedicated Cloud API number only, or WhatsApp Business App coexistence for customers who want to keep using an existing app number. This determines the authorization flow, eligibility checks, recovery UX, and support scope.
- [ ] Decide whether the production `/admin` surface will use dedicated operator authentication or be restricted to a trusted operator network; the shared-token login is not the intended launch control.
- [ ] Choose the fixed monthly GBP launch price.
- [ ] Define included lead, message, and AI usage plus the fair-use or hard-limit behavior.
- [ ] Decide cancellation timing, refund policy, and whether displayed prices include tax.
- [ ] Decide who pays Meta WhatsApp messaging charges and how each customer's WABA payment relationship is established.
- [ ] Decide what happens to inbound WhatsApp messages after subscription entitlement is lost.
- [ ] Decide whether analytics/cookies require consent for the intended launch setup and jurisdiction.

## Stripe and tax setup

- [ ] Confirm VAT/tax obligations with an accountant and decide whether to use Stripe Tax or explicit tax rates.
- [ ] Create separate Stripe sandbox and live products/prices for the agreed plan and trial.
- [ ] Configure Stripe branding, statement descriptor, support contact, invoice/receipt emails, and Customer Portal options.
- [ ] After the billing implementation exists, add its sandbox and live secrets to the correct Railway environments without committing them.
- [ ] Register the production Stripe webhook and complete the sandbox lifecycle rehearsal.

## Meta and WhatsApp setup

- [x] Register/configure the Meta app and LeadRelay's required tech-provider or solution integration.
- [x] Complete Meta business verification, App Review, and Advanced Access for only the required permissions.
- [ ] Confirm the production WABA webhook, payment/credit relationship, and reconnect process end to end.
- [ ] Confirm Railway has no stale `WhatsApp__MessagesEndpoint` or sender-specific endpoint override, set/verify `WhatsApp__GraphApiVersion=v23.0`, run production inbound/outbound smoke tests, and assign an owner for the quarterly version review.

## Legal, privacy, and trust

- [ ] Have the privacy policy, terms, trial/subscription wording, refund/cancellation wording, and data-processing terms reviewed for the launch jurisdiction.
- [ ] Publish the actual subprocessor list, including Railway/database hosting, Meta, Postmark, OpenAI, Stripe, monitoring, and analytics providers in use.
- [ ] Approve retention periods for leads, conversations, webhook receipts, accounts, and backups.
- [ ] Define the data-subject-request and account-deletion process, including identity verification and retained-data obligations.
- [ ] Review OpenAI/provider data retention settings and ensure the privacy notice matches them.
- [ ] Document customer responsibilities for WhatsApp consent, lawful lead processing, and template/window rules.

## Production operations

- [ ] Confirm production domain, DNS, TLS, and support/privacy/legal mailboxes.
- [ ] Verify the Postmark sending domain and test verification, password-reset, lead, and billing emails end to end.
- [ ] Audit Railway production variables, assign an owner for each secret, and document rotation.
- [ ] Enable automated database backups, approve retention, and complete a timed restore drill into an isolated environment.
- [ ] Choose and configure monitoring/alerting, plus a named incident owner and support channel.
- [ ] Complete a production-like release rehearsal and document rollback/forward-fix procedures.

## Launch approval

- [ ] Review the final pricing, landing-page claims, legal pages, and onboarding wording against the shipped product.
- [ ] Complete mobile, accessibility, browser, tenant-isolation, billing, webhook, backup/restore, and end-to-end launch checks.
- [ ] Approve the launch checklist and the post-launch daily operational review.
