# LeadRelay

Drop-in “Chat via WhatsApp” widget + server-side lead capture. Includes a simple POC chat flow and a debug page to simulate WhatsApp messages locally.

## Run
- `dotnet run --project src/LeadRelay.Web`
- App binds to `http://localhost:5180` (configured in `src/LeadRelay.Web/appsettings.json`)
- Debug console (no WhatsApp required): `http://localhost:5180/debug/whatsapp`

## Database (MySQL)
- Connection string: `src/LeadRelay.Web/appsettings.json` (`ConnectionStrings:LeadRelay`)
- Apply migrations:
  - `dotnet ef database update --project src/LeadRelay.Infrastructure --startup-project src/LeadRelay.Web`

## Endpoints
- `GET /widget/bootstrap.js?siteId=...` — bootstrap script (domain allow-list enforced)
- `GET /admin` — admin dashboard (requires admin token)
- `GET /admin/sites/new` — admin site create view (requires admin token)
- `GET /admin/sites/{siteId}` — admin site edit view (requires admin token)
- `GET/POST/PUT /admin/api/sites...` — admin API (requires admin token)
- `POST /admin/api/leads/intake` — channel-agnostic lead intake API (requires admin token)
- `GET /owner/login` — site owner login
- `GET /owner/password/forgot` — request password reset
- `GET /owner/password/reset` — set new password with reset token
- `GET /owner` — site owner lead inbox
- `GET /owner/leads/{id}` — lead detail view
- `POST /owner/leads/{id}/reply` — send WhatsApp reply to lead
- `GET /debug/whatsapp` — local UI to simulate chat flow
- `POST /debug/whatsapp/send` — simulate incoming message (form-encoded)
- `GET /debug/whatsapp/leads` — recent leads for debug UI
- `GET /v1/webhooks/whatsapp` — Meta webhook verification
- `POST /v1/webhooks/whatsapp` — WhatsApp webhook receiver

## Widget embed
Use this script on a customer site (update `siteId`):
```html
<script src="https://your-domain.com/widget/bootstrap.js?siteId=site_demo"></script>
```

## Domain allow-list
Each site has `AllowedDomains` and the bootstrap endpoint checks `Referer`/`Origin`.  
Base domains allow subdomains (e.g. `example.com` allows `foo.example.com`).

## WhatsApp setup (placeholders)
Set these in `src/LeadRelay.Web/appsettings.json`:
```json
{
  "WhatsApp": {
    "VerifyToken": "change_me_in_real_env",
    "AccessToken": "paste_your_cloud_api_access_token",
    "MessagesEndpoint": "https://graph.facebook.com/v20.0/<PHONE_NUMBER_ID>/messages"
  }
}
```

## Admin auth token
All `/admin` endpoints are protected by a shared token configured under `AdminAuth`.

Set `AdminAuth:Token` via environment for non-local environments.  
For local testing:
- Access `/admin` and enter the token in the login form.
- You can still deep-link with `http://localhost:5180/admin?adminToken=your_token`.

For API requests, send:
- Header `X-Admin-Token: your_token`
- or `Authorization: Bearer your_token`

## Owner portal
Site owners can log into `/owner` and:
- view leads scoped to their site
- open lead details
- send WhatsApp replies to leads

Owner inbox supports:
- search: `/owner?q=alice`
- paging: `/owner?page=2&pageSize=20`

Owner sessions are signed tokens and require `OwnerPortal:SigningSecret` to be set.  
Owners sign in from `/owner/login` with email and password.  
Password reset is available via `/owner/password/forgot` (email link) and `/owner/password/reset`.  
Admin site edit pages include the owner login URL (`/owner/login`) to share as the canonical entry point.

## Channel-agnostic lead intake
Leads can now be ingested through an API independent of WhatsApp:
- `POST /admin/api/leads/intake`
- includes `siteId`, optional `channel` (defaults to `api`), contact details, fields, and optional conversation turns
- uses the same lead capture pipeline as WhatsApp/debug flows
- current outbound channels supported by dispatcher: `whatsapp`, `email`

## Conversation configuration (per site)
Edit the demo seed in `src/LeadRelay.Infrastructure/Persistence/SeedData.cs`:
- `BusinessSummary` — short description of the business (for future AI prompting).
- `Fields` — ordered list of data points to collect (name, email, project description, etc.).

## Local testing (no WhatsApp required)
Use the debug page to send messages and see replies/collected fields:
`http://localhost:5180/debug/whatsapp`

### Debug UI tips
- Use the “Recent leads” dropdown to load an existing lead (auto-fills waId + contact name).
- Pick “New lead (start fresh)” to generate a new waId and contact name.
- To pause/resume a conversation (simulated human takeover):
  - `POST /debug/whatsapp/pause` with form fields `waId` and `paused=true|false`
