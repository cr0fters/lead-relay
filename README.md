# LeadRelay

Drop-in "Chat via WhatsApp" widget + server-side lead capture.

## Run
- `dotnet run --project src/LeadRelay.Web`
- Open: `http://localhost:5180/widget/demo`

## Endpoints
- `POST /v1/widget/token`
- `GET /health`
- `GET /widget/wa-lead-widget.min.js`
- `GET /widget/demo`
- `POST /v1/webhooks/whatsapp`
