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
- [ ] `WhatsApp__MessagesEndpoint=https://graph.facebook.com/v20.0/{phone_number_id}/messages` is set.
- [ ] Webhook callback URL points to `https://<your-domain>/v1/webhooks/whatsapp`.
- [ ] Webhook verification succeeds in Meta.
- [ ] Alerting exists for webhook and outbound send failures.

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
- [ ] Store sender access token in your secret manager.
- [ ] Set env var: `WhatsApp__Senders__<PHONE_NUMBER_ID>__AccessToken`.
- [ ] Set env var: `WhatsApp__Senders__<PHONE_NUMBER_ID>__MessagesEndpoint=https://graph.facebook.com/v20.0/<PHONE_NUMBER_ID>/messages`.

3. LeadRelay admin config
- [ ] Open `/admin/sites/{siteId}`.
- [ ] Set `WhatsApp number` to tenant display number digits.
- [ ] Set `WhatsApp phone number ID` to tenant `PHONE_NUMBER_ID`.
- [ ] Save site.

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
  - Check sender-specific endpoint is valid.
- Webhook verify fails:
  - Check `WhatsApp__VerifyToken` matches Meta webhook config.
