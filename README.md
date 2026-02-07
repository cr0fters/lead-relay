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
