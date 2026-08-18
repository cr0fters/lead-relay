# LeadRelay MVP and launch plan

Last reviewed: 2026-08-18

## Product definition

LeadRelay is a WhatsApp lead-capture and qualification product with a lightweight lead CRM. The website widget starts a WhatsApp conversation; AI gathers the business's chosen qualification details; the owner receives a structured lead, conversation history, notifications, and a workspace for follow-up.

MVP intentionally means:

- one business/workspace per owner account
- one connected WhatsApp business sender per workspace
- a website click-to-WhatsApp widget
- AI qualification using configurable questions
- a lightweight lead CRM, not a general-purpose sales CRM
- one paid subscription plan with a time-limited trial
- self-service signup, onboarding, billing, cancellation, and recovery

Team seats, multiple senders, advanced pipelines, automations, integrations, and usage-based pricing are post-MVP unless early customer evidence changes the priority.

## Recommended customer journey

1. Create an account and verify the email address.
2. Enter the business profile and qualification questions.
3. Connect WhatsApp through Meta Embedded Signup.
4. Complete inbound and outbound tests in LeadRelay.
5. Start a 14-day trial through Stripe-hosted Checkout.
6. Add and verify the website domain, install the widget, and test the click-to-WhatsApp journey.
7. Capture the first real lead and receive an email notification.
8. Review, qualify, reply to, and close the lead in the CRM workspace.
9. Manage payment details, invoices, and cancellation through Stripe's hosted Customer Portal.

The trial should start at Stripe Checkout after WhatsApp testing, not at initial account creation. This avoids consuming the trial while the customer completes Meta setup. Checkout should collect a payment method and Stripe should charge it when the trial ends.

## Priority 0: true self-service onboarding

- [x] Self-service account registration with password creation.
- [x] Sign in, sign out, forgotten-password, and password-reset flows.
- [x] Add email verification before an account can activate billing or publish a widget.
  - issue a single-use, expiring verification token
  - resend safely with rate limiting and non-enumerating responses
  - record `EmailVerifiedAtUtc`
- [x] Record explicit acceptance of the terms and privacy policy at signup.
  - store accepted document versions and timestamp
  - link both documents beside the checkbox
- [ ] Replace manual WABA ID, phone-number ID, and access-token entry with Meta Embedded Signup.
  - register LeadRelay as the appropriate Meta tech provider/solution integration
  - complete Meta business verification and App Review/Advanced Access requirements
  - request only the required WhatsApp/business permissions
  - exchange the returned authorization code server-side
  - discover and persist the WABA and phone-number identifiers automatically
  - subscribe the WABA to the LeadRelay webhook
  - encrypt retained credentials and support reconnect/re-authorize
  - preserve the manual credential form as an admin-only recovery tool, not the normal owner journey
- [ ] Decide and document who pays Meta's WhatsApp messaging charges and how the customer's WABA payment method/credit relationship is established during onboarding.
- [ ] Make the onboarding checklist describe customer outcomes rather than Meta internals:
  - account verified
  - WhatsApp connected and tested
  - website widget installed and tested
  - subscription/trial active
  - first real lead captured
- [ ] Add a safe widget installation test.
  - verify the configured domain can load the bootstrap script
  - show a live preview of the existing click-to-WhatsApp widget
  - guide the owner through a real test message
  - distinguish test conversations/leads from real leads and analytics
  - provide copyable platform-specific instructions for common site builders
- [ ] Let owners manage all onboarding configuration without the admin area.
  - business name and summary
  - greeting/intro message
  - qualification fields and ordering
  - allowed domains and widget snippet
  - WhatsApp connection, health, reconnect, and disconnect
- [ ] Add recovery states for every onboarding failure.
  - Meta authorization canceled or expired
  - number already connected elsewhere
  - webhook not received
  - outbound test outside an allowed messaging window
  - invalid/expired credential
  - widget blocked by domain allow-list
- [ ] Verify and upgrade the configured Meta Graph API version/endpoints before launch; remove duplicated hard-coded version strings and document the provider-version upgrade cadence.
- [ ] Send lifecycle emails for email verification, onboarding abandoned, trial started, first lead, and trial ending.

## Priority 0: Stripe Billing

### Product and account decisions

- [ ] Create a standard Stripe account for LeadRelay and activate it before accepting live payments.
- [ ] Use Stripe Billing, hosted Checkout, and the hosted Customer Portal. Do not build card forms or store card details.
- [ ] Start with one fixed monthly GBP plan and one 14-day trial.
- [ ] Define the plan's included lead/message/AI usage and fair-use or hard-limit behavior; enforce any advertised limit server-side.
- [ ] Decide the launch price, refund policy, cancellation behavior, and whether prices include tax.
- [ ] Confirm VAT/tax obligations with an accountant and configure Stripe Tax or explicit tax rates as appropriate.
- [ ] Configure Stripe branding, statement descriptor, support contact, invoice/receipt emails, and the Customer Portal.
- [ ] Create separate sandbox and live products/prices; store their IDs in environment configuration.

### Billing implementation

- [ ] Add a billing record linked one-to-one with `Site`.
  - local lifecycle: `onboarding`, `trialing`, `active`, `past_due`, `canceled`, `unpaid`
  - Stripe customer ID
  - Stripe subscription ID
  - Stripe product ID and price ID
  - trial end, current-period end, cancel-at-period-end, and last-synced timestamps
  - Stripe remains authoritative; the local record is the access-control snapshot
- [ ] Add an idempotent Stripe webhook receipt table keyed by Stripe event ID.
- [ ] Add an authenticated endpoint that creates a Stripe Checkout Session for the configured server-side price.
  - never trust a price or product ID supplied by the browser
  - attach `SiteId` as Stripe metadata/client reference
  - prevent duplicate active subscriptions
  - use fixed success and cancellation URLs under `PublicBaseUrl`
- [ ] Add a signed Stripe webhook endpoint using the raw request body.
  - verify the `Stripe-Signature` header with the webhook secret
  - handle duplicate and out-of-order events safely
  - process at least `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.paid`, and `invoice.payment_failed`
  - alert on repeated processing failures
- [ ] Add an authenticated endpoint that creates a short-lived Stripe Customer Portal session.
- [ ] Add a Billing page showing plan, status, trial/renewal date, payment problem banners, and a Manage billing button.
- [ ] Implement one server-side entitlement policy used consistently by controllers and background/webhook flows.
  - `trialing` and `active`: full service
  - `past_due`: configurable grace period with persistent warning and portal access
  - `canceled`/`unpaid` after grace: read-only CRM, export, billing, and account access remain available; widget, AI replies, and outbound sends are disabled
  - onboarding and billing routes remain available before subscription
  - never delete customer data automatically merely because payment failed
- [ ] Decide how inbound WhatsApp messages behave after entitlement loss and communicate it clearly to owners and leads.
- [ ] Add billing emails/banners for trial ending, payment failed, grace ending, cancellation scheduled, and cancellation completed.
- [ ] Add automated tests for checkout authorization, webhook signatures/idempotency/order, status transitions, grace periods, and every gated capability.
- [ ] Test the complete lifecycle in Stripe sandbox, including successful renewal, failed payment, card update, cancellation, reactivation, and duplicate webhooks.

### Stripe production configuration

- [ ] Add secrets/configuration without committing values:
  - `Stripe__SecretKey`
  - `Stripe__PublishableKey` only if a client-side Stripe component is later required
  - `Stripe__WebhookSecret`
  - `Stripe__ProductId`
  - `Stripe__MonthlyPriceId`
  - `Stripe__TrialDays=14`
- [ ] Register the production webhook URL, for example `https://leadrelay.dev/v1/webhooks/stripe`.
- [ ] Restrict live API keys, enable MFA on the Stripe account, and document key rotation.

## Priority 0: credible lightweight CRM

- [x] Tenant-scoped lead inbox, search, pagination, and lead detail.
- [x] Structured customer/project data and conversation history.
- [x] AI-generated project summary and configurable qualification fields.
- [x] Owner replies over available email/WhatsApp channels.
- [x] Per-lead automation pause/takeover control.
- [ ] Replace the current mostly inferred status display with owner-controlled CRM stages.
  - [x] use the MVP stages `new`, `qualified`, `contacted`, `won`, and `lost`
  - [x] add stage changes to the activity timeline
  - [x] filter/search by stage and date
  - [ ] confirm the authoritative CI build, tests, and migration validation for this milestone
- [ ] Add owner notes and a next-action/reminder field to each lead.
  - [x] persist tenant-scoped private notes, next action, and optional UTC due time
  - [x] let owners view, edit, clear, and validate follow-up details from lead detail
  - [ ] confirm the authoritative CI build, tests, and migration validation for this milestone
- [ ] Add unread/new-lead indication so the inbox is operationally useful.
- [ ] Add CSV export for leads and their core qualification fields.
- [ ] Add safe single-lead deletion/anonymization and an account-level export/deletion workflow.
- [ ] Clarify the source of each lead and whether a conversation is a test.
- [ ] Prevent unsupported outbound WhatsApp free-form sends outside Meta's customer-service window; guide owners to approved templates where required.
- [ ] Add a minimal empty-state experience that links a new owner back to onboarding and widget installation.

## Priority 0: landing page and product communication

- [ ] Reposition the hero as “WhatsApp lead qualification + lightweight CRM,” not merely a WhatsApp button.
- [ ] Explain the complete value chain visually:
  - website visitor taps the familiar WhatsApp button
  - AI qualifies the enquiry using custom questions
  - a structured lead appears in the CRM inbox
  - the owner is notified, takes over, follows up, and tracks the outcome
- [ ] Add real product screenshots or faithful UI mockups of the widget, qualification conversation, lead inbox, and lead detail timeline.
- [ ] Use the existing WhatsApp artwork more deliberately.
  - add a recognizable WhatsApp icon in the hero workflow and installation step
  - retain LeadRelay branding for the CRM/product areas
  - follow WhatsApp/Meta brand guidelines and do not imply endorsement
- [ ] Replace generic “AI team” language with concrete capabilities users can verify.
- [ ] Add a concise feature grid grouped as Capture, Qualify, Organize, and Convert.
- [ ] Add a real pricing section tied to the single Stripe plan, trial terms, inclusions, cancellation, and any usage limits.
- [ ] Add a “What you need” section explaining the website snippet, Meta/WhatsApp business prerequisites, and expected setup effort honestly.
- [ ] Add stronger repeated calls to action: Start trial, See how it works, and Sign in.
- [ ] Replace or remove claims that the product cannot currently substantiate:
  - “2 min typical setup” until measured
  - “0 code install” while a script snippet is required
  - “Ask your CRM anything” because no CRM query interface exists
  - dashboard export/removal until those controls exist
  - team access/audit claims while the product supports one owner and no audit log
- [ ] Update the page title, meta description, Open Graph image, canonical URL, sitemap, and robots configuration.
- [ ] Add privacy-conscious conversion analytics only after deciding the consent/cookie approach.
- [ ] Run mobile, accessibility, performance, and cross-browser checks on the final page.

## Priority 0: legal, privacy, and customer trust

- [ ] Replace all placeholder business identity/contact details in policy documents.
- [ ] Have the privacy policy, terms, trial/subscription terms, refund/cancellation wording, and data-processing terms reviewed for the launch jurisdiction.
- [ ] Publish a subprocessor list covering at least Railway, database hosting, Meta/WhatsApp, Postmark, OpenAI, Stripe, monitoring, and analytics providers actually in use.
- [ ] Define and implement retention periods for leads, conversations, webhook receipts, account records, and backups.
- [ ] Create an operational data-subject-request process with identity verification, export, deletion/anonymization, and completion tracking.
- [ ] Ensure account closure coordinates Stripe cancellation, WhatsApp disconnect, credential deletion, and retained-data obligations.
- [ ] Document the owner/business's responsibility for WhatsApp consent, lawful lead processing, and message-template rules.
- [ ] Review AI provider data-handling/retention settings and ensure the privacy notice matches the actual configuration.
- [ ] Decide whether cookies/analytics require a consent banner; do not add a generic banner without an actual consent model.

## Priority 0: production reliability and security

- [x] Production database migrations and CI migration validation.
- [x] Database readiness and process liveness endpoints.
- [x] Signed WhatsApp webhooks, tenant attribution, replay/idempotency protection, and rate limiting.
- [x] Encrypted per-tenant WhatsApp credentials.
- [x] Postmark transactional email integration.
- [ ] Add structured error monitoring and alerting for:
  - application crashes and deployment failures
  - WhatsApp webhook rejection/processing failures
  - outbound WhatsApp failures and credential expiry
  - AI provider failures/timeouts
  - Postmark failures and password-reset delivery
  - Stripe webhook failures and payment failures
- [ ] Add bounded timeouts, cancellation, and deliberate retry/backoff policies for Meta, OpenAI, Postmark, and Stripe.
- [ ] Add upstream dependency diagnostics without exposing secrets or raw provider payloads.
- [ ] Treat the embedded widget script as a public compatibility contract: version releases, preserve old embeds where practical, and use deliberate cache busting/rollback.
- [ ] Add production security headers, including HSTS, CSP, frame protections, content-type protections, and a deliberate referrer policy.
- [ ] Remove production reliance on unversioned CDN assets (`tailwindcss.com`, Alpine `3.x.x`, and Lucide `latest`); bundle or pin reviewed assets with integrity controls.
- [ ] Force secure cookies in production and review session invalidation, signing-key rotation, and logout behavior.
- [ ] Remove admin-token query-string authentication before launch; use a dedicated admin identity or restrict the admin surface to trusted operators/network access.
- [ ] Add dependency/security scanning and secret scanning to CI.
- [ ] Confirm log redaction for access tokens, reset tokens, message content, and customer personal data.
- [ ] Add abuse protection for registration, password reset, expensive AI conversations, and outbound messaging, not only raw request counts.
- [ ] Threat-model tenant isolation across every read, write, export, webhook, and billing path.

## Priority 0: operating the service

- [ ] Gate and verify production deployments.
  - [x] expose the deployed Railway commit SHA at the unlinked, non-indexed `/.well-known/leadrelay-version` endpoint
  - [ ] enable Railway **Wait for CI** so a deployed SHA implies the GitHub Actions checks passed
- [ ] Configure a real production domain, DNS, TLS, sender email domain, and support/privacy/legal mailboxes.
- [ ] Verify the Postmark sending domain and test verification, password reset, lead notification, and billing emails end to end.
- [ ] Configure Railway/environment secrets and document ownership/rotation for each.
- [ ] Enable automated database backups with a documented retention policy.
- [ ] Complete and time one restore drill into an isolated environment.
- [ ] Run a production-like release rehearsal covering migration, signup, Meta onboarding, Stripe checkout, widget install, first lead, reply, payment failure, rollback, and restore.
- [ ] Document deployment rollback and forward-fix procedures, including non-reversible migrations.
- [ ] Add external uptime checks for liveness and the customer-facing journey; keep deployment readiness checks separate.
- [ ] Define a support channel, incident owner, severity levels, and customer communication template.
- [ ] Create a launch checklist and a post-launch daily review for errors, failed webhooks, failed emails, subscriptions, and support requests.

## Priority 1: early-customer improvements

- [ ] Add a small dashboard with lead volume, qualification completion, source, and outcome conversion.
- [ ] Add notification preferences and optional daily digest.
- [ ] Add approved WhatsApp template selection for owner-initiated/out-of-window follow-up.
- [ ] Add widget appearance controls that remain within WhatsApp brand requirements.
- [ ] Add bulk lead export and archive actions.
- [ ] Add account email/password change with re-authentication.
- [ ] Add integration tests that exercise real HTTP middleware/routes against MySQL and simulated provider webhooks.
- [ ] Collect onboarding funnel events and reasons for abandonment.

## Priority 2: explicitly post-MVP

- [ ] Multiple owner seats, invitations, roles, and an audit log.
- [ ] Multiple WhatsApp numbers/workspaces under one customer.
- [ ] Advanced CRM pipelines, tasks, calendar booking, and workflow automation.
- [ ] HubSpot/other CRM synchronization.
- [ ] Usage-based or multi-tier billing and Stripe entitlements.
- [ ] Public API keys, customer webhooks, and third-party integrations.
- [ ] Additional messaging channels.
- [ ] AI-powered search or “ask your CRM” functionality.

## MVP exit criteria

- [ ] A person with no LeadRelay operator assistance can verify an account, configure their business, connect WhatsApp through Meta, test both directions, pay through Stripe, install/test the widget, and capture a correctly attributed first lead.
- [ ] The owner can understand the lead, update its CRM stage, add a note/next action, take over the conversation, and reply successfully within supported channel rules.
- [ ] Trial, active, grace, past-due, canceled, and reactivated subscriptions produce the documented server-side behavior.
- [ ] Owners can manage billing, export their data, request/complete account deletion, recover their password, reconnect WhatsApp, and understand every blocking state without operator intervention.
- [ ] Marketing and legal pages describe only functionality and controls that actually exist.
- [ ] Monitoring alerts reach a named operator and the backup/restore, rollback, Meta, Stripe, Postmark, and incident runbooks have been exercised once.
- [ ] The release passes tenant-isolation, webhook-signature/idempotency, billing, accessibility, mobile, browser, and production-like end-to-end checks.
