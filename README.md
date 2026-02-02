# LeadRelay

Drop-in “Chat via WhatsApp” widget + server-side lead capture. Includes a simple POC chat flow and a debug page to simulate WhatsApp messages locally.

## Run
- `dotnet run --project src/LeadRelay.Web`
- App binds to `http://localhost:5180` (configured in `src/LeadRelay.Web/appsettings.json`)
- Debug console (no WhatsApp required): `http://localhost:5180/debug/whatsapp`

## Endpoints
- `GET /widget/bootstrap.js?siteId=...` — bootstrap script (domain allow-list enforced)
- `GET /debug/whatsapp` — local UI to simulate chat flow
- `POST /debug/whatsapp/send` — simulate incoming message (form-encoded)
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

## Conversation configuration (per site)
Edit the demo site in `src/LeadRelay.Infrastructure/Persistence/InMemorySiteRepository.cs`:
- `BusinessSummary` — short description of the business (for future AI prompting).
- `Fields` — ordered list of data points to collect (name, email, project description, etc.).

## Local testing (no WhatsApp required)
Use the debug page to send messages and see replies/collected fields:
`http://localhost:5180/debug/whatsapp`
