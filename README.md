# LeadRelay

## Production version check

`GET /.well-known/leadrelay-version` returns the safe application version and Railway-provided commit SHA for the currently running deployment. The endpoint is deliberately not linked from the public site, sends a `noindex` robots header, and disables response caching.

Enable **Wait for CI** on the production Railway service's GitHub deployment trigger. With that setting enabled, compare the endpoint's `commitSha` with the commit pushed to `main`: a match demonstrates that GitHub Actions passed, Railway deployed that commit, and the new process is serving requests. Without **Wait for CI**, the endpoint proves deployment only—not CI success.

Drop-in “Chat via WhatsApp” widget + server-side lead capture. Includes a simple POC chat flow and a debug page to simulate WhatsApp messages locally.

## Run
- Install/build pinned frontend assets after checkout or dependency changes: `npm ci && npm run build`
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
- `WhatsApp:VerifyToken`
- `WhatsApp:AppSecret`
- `WhatsApp:CredentialEncryptionKey` (base64-encoded 32-byte key)
- `WhatsApp:GraphApiBaseUrl` (normally `https://graph.facebook.com`)
- `WhatsApp:GraphApiVersion` (currently `v23.0`)
- `WhatsApp:RequireSignatureValidation=true`

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
dotnet user-secrets set "WhatsApp:AppSecret" "<meta_app_secret>" --project src/LeadRelay.Web
dotnet user-secrets set "WhatsApp:CredentialEncryptionKey" "<openssl_rand_base64_32_output>" --project src/LeadRelay.Web
dotnet user-secrets set "OpenAI:ApiKey" "<openai_api_key>" --project src/LeadRelay.Web
```

### Production
Provide values via environment variables (double underscore maps to `:`):
```bash
ConnectionStrings__LeadRelay=...
AdminAuth__Token=...
OwnerPortal__SigningSecret=...
# Optional only during a signing-key rotation:
OwnerPortal__PreviousSigningSecret=...
OwnerPortal__EmailVerificationTtlHours=24
OwnerPortal__EmailVerificationResendCooldownSeconds=60
PublicBaseUrl=https://your-domain.com
WhatsApp__VerifyToken=...
WhatsApp__AppSecret=...
WhatsApp__CredentialEncryptionKey=...
WhatsApp__GraphApiBaseUrl=https://graph.facebook.com
WhatsApp__GraphApiVersion=v23.0
WhatsApp__RequireSignatureValidation=true
WhatsApp__IdempotencyProcessingLeaseMinutes=30
WhatsApp__ProcessedReceiptRetentionDays=30
WhatsApp__AccessToken=...
WhatsApp__Senders__<PHONE_NUMBER_ID>__AccessToken=...
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownProxies__0=<YOUR_REVERSE_PROXY_IP>
OpenAI__ApiKey=...
```

To rotate the owner-session signing key without signing everyone out at once, move the existing key to `OwnerPortal__PreviousSigningSecret`, set a new high-entropy `OwnerPortal__SigningSecret`, and deploy both changes together. New sessions use only the current key. Remove the previous key after the configured owner session lifetime (12 hours by default). A successful password reset increments the account's session version and immediately invalidates its existing sessions regardless of key overlap.

Normal logout expires the browser's owner cookie using the same path and security attributes with which it was issued. Owner tokens are stateless for the MVP, so if a copied session token may have been compromised, complete a password reset to revoke every existing owner session immediately.

Railway deployments automatically recognize Railway's exact `X-Forwarded-Proto: https` signal so secure cookies, antiforgery, HSTS, and generated HTTPS URLs work behind its TLS-terminating proxy. This Railway-specific handling only upgrades the request scheme; it does not trust forwarded host or client-IP values.

Leave the general forwarded-header configuration disabled for direct hosting. Behind any other reverse proxy, enable it and add at least one proxy IP you operate or explicitly trust. The app fails startup if general forwarding is enabled without a trusted proxy, and headers from every other source are ignored.

## CI/CD (GitHub tests + Railway deploy)
This repo includes `.github/workflows/ci-cd.yml` to:
- rebuild locally served Tailwind, Alpine, and Lucide assets and fail if committed output has drifted
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
- `GET /owner/onboarding` — guided WhatsApp + widget onboarding and progress checklist
- `GET /owner/leads/{id}` — lead detail view
- `POST /owner/leads/{id}/reply` — send an email or an in-window WhatsApp reply to a lead
- `GET /debug/whatsapp` — local UI to simulate chat flow
- `POST /debug/whatsapp/send` — simulate incoming message (form-encoded)
- `GET /debug/whatsapp/leads` — recent leads for debug UI
- `GET /v1/webhooks/whatsapp` — Meta webhook verification
- `POST /v1/webhooks/whatsapp` — WhatsApp webhook receiver

Owner WhatsApp replies follow [Meta's rolling 24-hour customer-service window](https://whatsappbusiness.com/policy/), measured from the latest persisted inbound customer message. LeadRelay blocks free-form sends after the window closes and explains that the customer must message again or an approved template is required; approved-template sending is not yet available in LeadRelay.

## Widget embed
Use this script on a customer site (update `siteId`):
```html
<script src="https://your-domain.com/widget/bootstrap.js?siteId=site_demo"></script>
```

## Domain allow-list
Each site has `AllowedDomains` and the bootstrap endpoint checks `Referer`/`Origin`.  
Base domains allow subdomains (e.g. `example.com` allows `foo.example.com`).

## WhatsApp setup
The Graph API base URL and version are configured once and all onboarding and message endpoints are generated from them:
```json
{
  "WhatsApp": {
    "VerifyToken": "set_via_user_secrets_or_env",
    "AccessToken": "set_via_user_secrets_or_env",
    "GraphApiBaseUrl": "https://graph.facebook.com",
    "GraphApiVersion": "v23.0",
    "Senders": {
      "<PHONE_NUMBER_ID>": {
        "AccessToken": "optional_per_sender_token"
      }
    }
  }
}
```

`WhatsApp:MessagesEndpoint` and sender-specific `MessagesEndpoint` values remain supported only as legacy/operator overrides. New configuration should omit them so a Graph API version upgrade is made in one place.

Review Meta's supported Graph API versions at least quarterly and before each production release. Upgrade `WhatsApp:GraphApiVersion` only after exercising account connection, WABA subscription, inbound webhook routing, and outbound messaging in a non-production environment; then repeat those smoke tests after deployment.

For multi-tenant routing:
- owners can connect WhatsApp from `/owner/onboarding`; tokens are encrypted before database storage
- the onboarding flow validates the phone number, subscribes the app to the WABA, and stores the sender identifiers
- `WhatsAppConnections` is the source of truth for self-serve sender identifiers; legacy fields on `Sites` are synchronized for backward compatibility and operator-managed tenants
- admin/API configuration and `WhatsApp:Senders` remain available as a legacy/operator fallback
- webhook inbound routing matches `entry[].changes[].value.metadata.phone_number_id` to that site
- outbound sends use per-sender credentials when `WhatsApp:Senders:<PHONE_NUMBER_ID>` is configured
- unmatched inbound messages are logged and ignored; they are never assigned to an arbitrary tenant

Generate the credential encryption key once and retain it in the production secret store:
```bash
openssl rand -base64 32
```
Changing or losing this key makes previously stored tenant access tokens unreadable.

## Admin auth token
All `/admin` endpoints are protected by a shared token configured under `AdminAuth`.

Set `AdminAuth:Token` via user-secrets (dev) or environment (non-dev).  
For local testing:
- Access `/admin` and enter the token in the login form.
- Browser access uses the `POST /admin/login` form and its secure, HTTP-only session cookie. Admin tokens are deliberately rejected in query strings because URLs leak into history and logs.

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
After registration they are signed in and sent to `/owner/onboarding`. The setup checklist is resumable and remains available from the WhatsApp status badge in the workspace header.
Users sign in from `/owner/login` with email and password.  
Password reset is available via `/owner/password/forgot` (email link) and `/owner/password/reset`.  
Admin site edit pages include the login URL (`/owner/login`) to share as the canonical entry point.

## Channel-agnostic lead intake
Leads can now be ingested through an API independent of WhatsApp:
- `POST /admin/api/leads/intake`
- includes `siteId`, optional `channel` (defaults to `api`), optional `isTest`, contact details, fields, and optional conversation turns
- uses the same lead capture pipeline as WhatsApp/debug flows
- preserves test attribution across later updates to the same lead; the setup flow also marks conversations from the configured WhatsApp test recipient automatically
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
