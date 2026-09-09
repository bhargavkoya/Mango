# Mango Documentation

- [Architecture](ARCHITECTURE.md) — system diagram, component list, cross-cutting concerns (auth, messaging, payments), known gaps
- [Local Setup](SETUP.md) — prerequisites, secrets, database/service startup order, first-time smoke test, troubleshooting
- Workflows (feature-by-feature, with sequence diagrams):
  - [Authentication](workflows/auth.md) — register, login, JWT issuance, role-based access
  - [Product Catalog](workflows/product-catalog.md) — browse, create/edit with image upload, delete
  - [Cart & Coupons](workflows/cart-and-coupons.md) — add to cart, view/pricing, apply/remove coupon, email cart
  - [Checkout & Payments](workflows/checkout-and-payments.md) — Stripe Checkout session, payment confirmation, order status/refunds
  - [Rewards & Notifications](workflows/rewards-and-notifications.md) — async messaging via Azure Service Bus

See also: [`/CLAUDE.md`](../CLAUDE.md) for the condensed version of this material aimed at AI coding agents working in this repo.
