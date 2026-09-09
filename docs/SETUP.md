# Local Setup

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (installed with Visual Studio's "ASP.NET and web development" workload, or standalone via SQL Server Express)
- An Azure Service Bus namespace with the topic/queues below created (a free-tier namespace is enough) — required only if you want to exercise the async email/rewards flows; the rest of the app runs without it
- A Stripe test-mode account (for checkout and coupon-to-Stripe sync)
- Visual Studio 2022 (open `Mango.slnx`) or the `dotnet` CLI

## 1. Provision configuration secrets

Every `appsettings.json` ships with an empty or placeholder value for the secrets below. **Note:** each project's `appsettings.Development.json` is committed to this repo (not gitignored) — do not paste real secrets into it. Use `dotnet user-secrets init && dotnet user-secrets set "Key:Path" "value" --project <ProjectDir>` instead, which stores secrets outside the repo on your machine.

### JWT signing secret

The same literal string must be set in **every** service plus the gateway, because each validates JWTs locally instead of calling AuthAPI:

| Project | Config path |
|---|---|
| `Mango.Services.AuthAPI` | `ApiSettings:JwtOptions:Secret` (+ `Issuer`, `Audience`) |
| `Mango.Services.ProductAPI`, `CouponAPI`, `ShoppingCartAPI`, `OrderAPI` | `ApiSettings:Secret` (+ `Issuer`, `Audience`) |
| `Mango.GatewaySolution` | `ApiSettings:Secret` (+ `Issuer`, `Audience`) |

`Issuer` (`mango-auth-api`) and `Audience` (`mango-client`) already match across projects out of the box — only `Secret` needs to be a real value you choose (any sufficiently long random string), copy-pasted identically everywhere.

### Azure Service Bus

Set `ServiceBusConnectionString` in:
- `Mango.Services.AuthAPI` (publishes to `registeruser`)
- `Mango.Services.ShoppingCartAPI` (publishes to `sbqemailshoppingcart`)
- `Mango.Services.OrderAPI` (publishes to the topic named by `TopicAndQueueNames:OrderCreatedTopic`, default `OrderCreated`)
- `Mango.Services.EmailAPI` (consumes `sbqemailshoppingcart` and `registeruser`)
- `Mango.Services.RewardAPI` (would consume `OrderCreatedRewardsUpdate`, but see the caveat below)

`Mango.MessageBus/MessageBus.cs` currently hardcodes its own connection string to `""` rather than reading it from configuration — you will need to either wire that constructor up to `IConfiguration`/DI, or hardcode your dev connection string there, before publishing will work end-to-end. This is a real gap in the current code, not a documentation omission.

In your Service Bus namespace, create:
- Queue `registeruser`
- Queue `sbqemailshoppingcart`
- Topic `OrderCreated` with subscription `OrderCreatedRewardsUpdate`

**Note:** even with all of the above wired up, RewardAPI does not currently start its consumer at all (see [ARCHITECTURE.md](ARCHITECTURE.md#known-gaps-worth-knowing-before-you-touch-this-code)) — reward points will not be credited until `Program.cs` is fixed to register and start `IAzureServiceBusConsumer`.

### Stripe

Set `Stripe:SecretKey` (a test-mode secret key, `sk_test_...`) in:
- `Mango.Services.OrderAPI`
- `Mango.Services.CouponAPI`

## 2. Databases

No manual `dotnet ef database update` is required — every service calls `Database.Migrate()` on startup (`ApplyMigration()` in each `Program.cs`), creating its LocalDB database on first run:

| Service | Database |
|---|---|
| AuthAPI | `Mango_Auth` |
| ProductAPI | `Mango_Product` |
| CouponAPI | `Mango_Coupon` |
| ShoppingCartAPI | `Mango_ShoppingCart` |
| OrderAPI | `Mango_Order` |
| RewardAPI | `Mango_Reward` |
| EmailAPI | `Mango_Email` |

If you point `DefaultConnection` at a real SQL Server instance instead of LocalDB, just make sure the account running the app can create databases.

## 3. Run the services

There is no `docker-compose`/Aspire orchestration in this repo — start each project individually, e.g. one terminal per service:

```
dotnet run --project Mango.Services.AuthAPI
dotnet run --project Mango.Services.ProductAPI
dotnet run --project Mango.Services.CouponAPI
dotnet run --project Mango.Services.ShoppingCartAPI
dotnet run --project Mango.Services.OrderAPI
dotnet run --project Mango.Services.RewardAPI
dotnet run --project Mango.Services.EmailAPI
dotnet run --project Mango.GatewaySolution   # optional — see ARCHITECTURE.md, unused by Mango.Web today
dotnet run --project Mango.Web
```

Or, in Visual Studio: right-click the solution → **Set Startup Projects... → Multiple startup projects**, and set at least AuthAPI, ProductAPI, CouponAPI, ShoppingCartAPI, OrderAPI, and Mango.Web to **Start**.

`Mango.Web` expects the backend services on the exact HTTPS ports baked into its `ServiceUrls` config (`Mango.Web/appsettings.json`) — these match each service's `launchSettings.json` `https` profile out of the box, so as long as you run with the default `https` launch profile for each project, no port changes are needed:

| Service | HTTPS port |
|---|---|
| Mango.GatewaySolution | 7777 |
| Mango.Web | 7207 |
| AuthAPI | 7137 |
| ProductAPI | 7244 |
| CouponAPI | 7246 |
| ShoppingCartAPI | 7181 |
| OrderAPI | 7036 |
| RewardAPI | 7224 |
| EmailAPI | 7282 |

## 4. First-time smoke test

1. Browse to `https://localhost:7207`.
2. Register a user at `/Auth/Register` — pick role `ADMIN` to be able to manage products/coupons, or leave blank for `CUSTOMER`. This calls AuthAPI, which publishes to `registeruser` (requires Service Bus to be configured, otherwise this call will throw — see the `MessageBus` connection-string gap above).
3. Log in — `Mango.Web` stores the JWT in a cookie and signs you in.
4. As `ADMIN`, create a product (`/Product/ProductCreate`) and a coupon (`/Coupon/CouponCreate` — this also calls Stripe, so `Stripe:SecretKey` must be valid or this will fail).
5. As any user, add the product to cart, apply the coupon, and check out — checkout redirects to Stripe Checkout (test mode), then back to `/Cart/Confirmation`, which calls `OrderAPI.ValidateStripeSession`.

If you haven't configured Service Bus/Stripe, you can still smoke-test catalog browsing, cart math, and coupon application — registration, checkout, and email/reward side effects require those integrations.

## Troubleshooting

- **401/403 from a backend API called by Mango.Web or another backend service**: the JWT secret differs between the two services, or the calling code isn't forwarding the bearer token (see `BackendApiAuthenticationHttpClientHandler` in `ShoppingCartAPI`/`OrderAPI`).
- **`Register` throws / hangs**: `Mango.MessageBus`'s connection string is empty — see the Service Bus section above.
- **LocalDB errors on first run**: confirm `sqllocaldb info MSSQLLocalDB` shows a running instance, or point `DefaultConnection` at another SQL Server instance.
- **Stripe calls fail with 401**: `Stripe:SecretKey` is empty/invalid in `OrderAPI` and/or `CouponAPI`.
