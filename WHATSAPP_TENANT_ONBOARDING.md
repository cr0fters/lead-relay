# WhatsApp Tenant Onboarding Runbook

Use this runbook to onboard each tenant WhatsApp account into LeadRelay.

## Meta bootstrap (do this first, once)
1. Accounts and org setup
- [ ] Create/sign into Meta for Developers: `https://developers.facebook.com/`.
- [ ] Create/sign into Meta Business Portfolio: `https://business.facebook.com/`.

2. App and product setup
- [ ] Create a Meta app (Business type).
- [ ] Add the WhatsApp product to the app.
- [ ] Open WhatsApp `Getting started` in the app dashboard.

3. Webhook setup
- [ ] Set webhook callback URL to `https://<your-domain>/v1/webhooks/whatsapp`.
- [ ] Set webhook verify token in Meta.
- [ ] Set app env var `WhatsApp__VerifyToken` to the same value.
- [ ] Complete webhook verify challenge successfully.

4. Initial WhatsApp Cloud API wiring
- [ ] Record `WABA ID`.
- [ ] Subscribe app to WABA (`POST /{WABA-ID}/subscribed_apps`).
- [ ] Record `Phone number ID` (`GET /{WABA-ID}/phone_numbers`).
- [ ] Register phone number if required (`POST /{PHONE_NUMBER_ID}/register` with PIN).
- [ ] Send a test message (`POST /{PHONE_NUMBER_ID}/messages`).

5. Production preparation
- [ ] Complete Meta business verification requirements.
- [ ] Move off temporary tokens to long-lived system-user/business token strategy.

## Global preflight (once per environment)
- [ ] `WhatsApp__VerifyToken` is set.
- [ ] `WhatsApp__GraphApiBaseUrl=https://graph.facebook.com` is set.
- [ ] `WhatsApp__GraphApiVersion=v23.0` is set and has passed the documented Meta sandbox smoke tests.
- [ ] `WhatsApp__EmbeddedSignupEnabled=true`, `WhatsApp__MetaAppId`, `WhatsApp__EmbeddedSignupConfigurationId`, and `WhatsApp__EmbeddedSignupVersion=v4` are set.
- [ ] The v4 Tech Provider configuration enables the WhatsApp Business App onboarding/coexistence flow.
- [ ] The production domain is allowed in Facebook Login for Business's OAuth redirect and JavaScript SDK settings.
- [ ] Webhook callback URL points to `https://<your-domain>/v1/webhooks/whatsapp`.
- [ ] Webhook verification succeeds in Meta.
- [ ] Alerting exists for webhook and outbound send failures.
- [ ] `WhatsApp__AppSecret` is set and `WhatsApp__RequireSignatureValidation=true`.
- [ ] `WhatsApp__CredentialEncryptionKey` is set to a retained base64-encoded 32-byte key.

## Self-serve tenant onboarding
Authenticated owners now use `/owner/onboarding` to:
1. launch Meta Embedded Signup v4 and choose the WhatsApp Business App coexistence flow
2. authorize an eligible existing WhatsApp Business App number without pasting identifiers or access tokens
3. let LeadRelay exchange the one-time code server-side, discover and validate the selected number, and subscribe the WABA
4. store the returned access token encrypted at rest
5. send a test message
6. send an inbound message so LeadRelay can verify signed webhook delivery
7. configure an allowed website domain and copy the widget snippet

The workspace header distinguishes setup, action-required, awaiting-webhook-verification, and verified states and links back to the resumable checklist.

## Tenant tracker
| Tenant | SiteId | WABA ID | Phone number ID | Display number (E.164) | App subscribed to WABA | Sender token stored | Site WhatsApp number set | Site WhatsApp phone number ID set | Inbound test passed | Owner reply test passed | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Example Co | `site_example` |  |  |  | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |  |

## Per-tenant onboarding checklist
1. Meta setup
- [ ] Create/confirm tenant WABA and business phone number.
- [ ] Record `WABA ID`.
- [ ] Record `Phone number ID`.
- [ ] Subscribe app to tenant WABA (`POST /{WABA-ID}/subscribed_apps`).
- [ ] Register phone number if needed (`POST /{PHONE_NUMBER_ID}/register` with PIN).

2. Secrets and config
- [ ] Complete the Meta coexistence flow in `/owner/onboarding` so the tenant token is exchanged server-side, encrypted, and stored dynamically.
- [ ] For operator-managed legacy tenants only, store sender credentials under `WhatsApp__Senders__<PHONE_NUMBER_ID>__...`.
- [ ] Do not set a sender-specific `MessagesEndpoint` unless a deliberate legacy/operator override is required; the app normally builds it from the global Graph API configuration.

3. LeadRelay config
- [ ] Confirm the owner onboarding page shows WhatsApp connected.
- [ ] Send an inbound message and confirm the onboarding page marks webhook delivery verified.
- [ ] Admin may inspect or repair the legacy site sender identifiers at `/admin/sites/{siteId}` if necessary.

4. Validation
- [ ] Restart app/deploy.
- [ ] Send inbound WhatsApp message to tenant number.
- [ ] Confirm lead lands in correct tenant owner portal.
- [ ] Send owner reply from `/owner/leads/{id}`.
- [ ] Confirm outbound message sends from correct tenant number.

5. Finalize
- [ ] Mark tenant active in tracker.
- [ ] Capture any tenant-specific notes (token expiry, messaging limits, approvals).

## Troubleshooting
- Inbound not mapped to tenant:
  - Check site `WhatsApp phone number ID`.
  - Check webhook payload includes `metadata.phone_number_id`.
- Outbound send fails:
  - Check `WhatsApp__Senders__<PHONE_NUMBER_ID>__AccessToken`.
  - Check the global Graph API version and base URL; if a legacy sender-specific endpoint override exists, check that it is current.
- Webhook verify fails:
  - Check `WhatsApp__VerifyToken` matches Meta webhook config.
