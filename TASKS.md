# LeadRelay MVP Tasks

## Now
- [x] Set up persistent storage for Sites, Leads, and Conversations (schema + migrations)
- [x] Replace in-memory repositories with DB-backed implementations
- [x] Add minimal admin interface (UI or API) to create/update site config
- [x] Protect admin endpoints with auth/token
- [ ] Add login area for site owners to view and respond to leads
- [ ] Wire secrets via environment (no secrets in repo) and document required env vars

## Next
- [ ] Harden WhatsApp webhook verification and signature validation
- [ ] Add outbound send retries/backoff with structured error logging
- [ ] Implement real lead delivery (email service or CRM webhook)
- [ ] Add lead payload formatting (site id, timestamp, summary)
- [ ] Rate limit by site and WhatsApp ID
- [ ] Disable/protect debug endpoints outside development
- [ ] Version + cache-bust widget runtime assets

## Later
- [ ] Add structured logging for key events (inbound/outbound, lead submission)
- [ ] Health checks for dependencies (DB, WhatsApp) with degraded status
- [ ] Deployment docs (env vars, ports, setup steps, example config)
- [ ] Container run instructions for production
