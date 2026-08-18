# Third-party frontend assets

LeadRelay builds and serves the following browser assets locally. Exact versions and integrity hashes are locked in `package-lock.json`; generated files are verified in CI.

- Alpine.js 3.16.2 — MIT license — <https://github.com/alpinejs/alpine>
- Lucide 1.32.0 — ISC license — <https://github.com/lucide-icons/lucide>
- Tailwind CSS 3.4.19 — MIT license — <https://github.com/tailwindlabs/tailwindcss>

Google Fonts remain loaded from `fonts.googleapis.com` and `fonts.gstatic.com` under their respective font licenses. They are explicitly allowed by the production Content Security Policy and are tracked separately from the locally bundled script and stylesheet dependencies.
