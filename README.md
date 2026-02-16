# LeadRelay

Drop-in “Chat via WhatsApp” widget + server-side lead capture. Includes a simple POC chat flow and a debug page to simulate WhatsApp messages locally.

## Run
- `dotnet run --project src/LeadRelay.Web`
- App binds to `http://localhost:5180` (configured in `src/LeadRelay.Web/appsettings.json`)
- Debug console (no WhatsApp required): `http://localhost:5180/debug/whatsapp`

## Configuration and Secrets
Sensitive values are intentionally blank in `src/LeadRelay.Web/appsettings.json`.

Required outside Development:
- `ConnectionStrings:LeadRelay`
- `AdminAuth:Token`
- `OwnerPortal:SigningSecret`
- `PublicBaseUrl` (recommended behind load balancers, for example `https://leadrelay.dev`)

The app fails fast on startup in non-Development if these are missing.

### Local development
Use .NET user-secrets for local values:
```bash
dotnet user-secrets set "ConnectionStrings:LeadRelay" "Server=localhost;Port=3307;User ID=root;Password=root;Database=LeadRelay" --project src/LeadRelay.Web
dotnet user-secrets set "AdminAuth:Token" "dev_admin_token_change_me" --project src/LeadRelay.Web
dotnet user-secrets set "OwnerPortal:SigningSecret" "dev_owner_signing_secret_change_me" --project src/LeadRelay.Web
```

Optional local settings (only if using these integrations):
```bash
dotnet user-secrets set "WhatsApp:VerifyToken" "dev_verify_token" --project src/LeadRelay.Web
dotnet user-secrets set "WhatsApp:AccessToken" "<meta_cloud_api_access_token>" --project src/LeadRelay.Web
dotnet user-secrets set "WhatsApp:MessagesEndpoint" "https://graph.facebook.com/v20.0/<PHONE_NUMBER_ID>/messages" --project src/LeadRelay.Web
dotnet user-secrets set "OpenAI:ApiKey" "<openai_api_key>" --project src/LeadRelay.Web
```

### Production
Provide values via environment variables (double underscore maps to `:`):
```bash
ConnectionStrings__LeadRelay=...
AdminAuth__Token=...
OwnerPortal__SigningSecret=...
PublicBaseUrl=https://your-domain.com
WhatsApp__VerifyToken=...
WhatsApp__AccessToken=...
WhatsApp__MessagesEndpoint=...
WhatsApp__Senders__<PHONE_NUMBER_ID>__AccessToken=...
WhatsApp__Senders__<PHONE_NUMBER_ID>__MessagesEndpoint=https://graph.facebook.com/v20.0/<PHONE_NUMBER_ID>/messages
OpenAI__ApiKey=...
```

## CI/CD (GitHub tests + Railway deploy)
This repo includes `.github/workflows/ci-cd.yml` to:
- run tests on every PR and push
- validate EF Core migrations against an ephemeral MySQL service
- generate an idempotent SQL migration script artifact (`artifacts/migrations.sql`)

Deployment is managed by Railway GitHub autodeploy (not GitHub CLI deploy).

### One-time Railway setup
1. Connect the Railway service to this GitHub repo.
2. Enable Railway setting: `Wait for CI`.
3. Configure Railway to monitor your production branch (typically `main`).
4. Set app runtime variables in Railway service:
- `ConnectionStrings__LeadRelay`
- `AdminAuth__Token`
- `OwnerPortal__SigningSecret`
- optional integrations (`WhatsApp__...`, `OpenAI__ApiKey`)

Apply the idempotent migration SQL as part of your Railway release process.  
This repo is configured to run `sh /app/apply-migrations.sh` as Railway `preDeployCommand`, which applies `/app/migrations.sql` before app startup.

`build/LeadRelay.Web.Dockerfile` now:
- installs MySQL client in the runtime image
- generates `/app/migrations.sql` during image build via `dotnet ef migrations script --idempotent`
- includes `build/apply-migrations.sh` in the runtime image

`build/apply-migrations.sh` supports either:
- `MYSQL_URL`
- or `MYSQLHOST` + `MYSQLPORT` + `MYSQLUSER` + `MYSQLPASSWORD` + `MYSQLDATABASE`

GitHub Actions also generates an idempotent migration script artifact for CI validation/review.

For local/dev migration application:
```bash
dotnet ef database update --project src/LeadRelay.Infrastructure --startup-project src/LeadRelay.Web
```

## Database (MySQL)
- Connection string: `src/LeadRelay.Web/appsettings.json` (`ConnectionStrings:LeadRelay`)
- Apply migrations:
  - `dotnet ef database update --project src/LeadRelay.Infrastructure --startup-project src/LeadRelay.Web`
- Generate idempotent SQL script:
  - `dotnet ef migrations script --idempotent --project src/LeadRelay.Infrastructure --startup-project src/LeadRelay.Infrastructure -o artifacts/migrations.sql`
- Railway pre-deploy migration command:
  - `sh /app/apply-migrations.sh`

## Endpoints
- `GET /widget/bootstrap.js?siteId=...` — bootstrap script (domain allow-list enforced)
- `GET /admin` — admin dashboard (requires admin token)
- `GET /admin/sites/new` — admin site create view (requires admin token)
- `GET /admin/sites/{siteId}` — admin site edit view (requires admin token)
- `GET/POST/PUT /admin/api/sites...` — admin API (requires admin token)
- `POST /admin/api/leads/intake` — channel-agnostic lead intake API (requires admin token)
- `GET /owner/register` — self-serve account registration
- `GET /owner/login` — login
- `GET /owner/password/forgot` — request password reset
- `GET /owner/password/reset` — set new password with reset token
- `GET /owner` — lead inbox
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
    "VerifyToken": "set_via_user_secrets_or_env",
    "AccessToken": "set_via_user_secrets_or_env",
    "MessagesEndpoint": "https://graph.facebook.com/v20.0/{phone_number_id}/messages",
    "Senders": {
      "<PHONE_NUMBER_ID>": {
        "AccessToken": "optional_per_sender_token",
        "MessagesEndpoint": "https://graph.facebook.com/v20.0/<PHONE_NUMBER_ID>/messages"
      }
    }
  }
}
```

For multi-tenant routing:
- set each site's `WhatsAppPhoneNumberId` in admin/API config
- webhook inbound routing matches `entry[].changes[].value.metadata.phone_number_id` to that site
- outbound sends use per-sender credentials when `WhatsApp:Senders:<PHONE_NUMBER_ID>` is configured

## Admin auth token
All `/admin` endpoints are protected by a shared token configured under `AdminAuth`.

Set `AdminAuth:Token` via user-secrets (dev) or environment (non-dev).  
For local testing:
- Access `/admin` and enter the token in the login form.
- You can still deep-link with `http://localhost:5180/admin?adminToken=your_token`.

For API requests, send:
- Header `X-Admin-Token: your_token`
- or `Authorization: Bearer your_token`

## Workspace
Users can log into `/owner` and:
- view leads scoped to their site
- open lead details
- send WhatsApp replies to leads

Inbox supports:
- search: `/owner?q=alice`
- paging: `/owner?page=2&pageSize=20`

Login sessions are signed tokens and require `OwnerPortal:SigningSecret` to be set.  
New users can self-register at `/owner/register`.  
Users sign in from `/owner/login` with email and password.  
Password reset is available via `/owner/password/forgot` (email link) and `/owner/password/reset`.  
Admin site edit pages include the login URL (`/owner/login`) to share as the canonical entry point.

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
