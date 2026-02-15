# Quoted Pricing Runbook

## Generate a tenant-scoped quote link
1. Resolve tenant (`X-Tenant-Slug` header; dev API host fallback for dev only).
2. Call `POST /quotes` with listing, dates, guests, quoted base amount, fee mode, and expiry.
3. Copy returned `token` into guest portal URL (for example `...?quoteToken=<token>`).

## Change tenant-level convenience fee / global discount
1. Resolve tenant context.
2. Read current values with `GET /tenant/settings/pricing`.
3. Update with `PUT /tenant/settings/pricing`.
4. Changes are cached for up to 5 minutes in API memory cache.

## Guest payment flow (Razorpay)
1. Guest portal validates quote via `GET /quotes/validate` and renders server breakdown.
2. Guest portal creates order using `POST /api/Razorpay/order` (send quote token or booking draft; do not send trusted amount).
3. API computes amount server-side and creates Razorpay order in paise.
4. On `POST /api/Razorpay/verify`, API persists booking/payment pricing breakdown fields.
