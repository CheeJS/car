# OracleCMS Car Stock API

[![CI](https://github.com/CheeJS/car/actions/workflows/ci.yml/badge.svg)](https://github.com/CheeJS/car/actions/workflows/ci.yml)

A multi-tenant REST API for dealers to manage car stock. Built with ASP.NET Core 8, Dapper, and SQLite.

---

## Quick start

```bash
# 1. Restore + build
dotnet build

# 2. Run the API (the SQLite file is created automatically on first launch)
dotnet run --project src/OracleCMS.CarStock.API
```

The API listens on **http://localhost:5266** by default and the browser opens Swagger UI automatically.

| URL | Purpose |
|---|---|
| `http://localhost:5266` | API base |
| `http://localhost:5266/swagger` | Swagger UI (with a JWT bearer authorize button) |

Required SDK: **.NET 8.0** (`dotnet --list-sdks` should include an `8.0.*` entry).

---

## Running the tests

```bash
dotnet test
```

46 tests across two layers:

| Suite | Focus |
|---|---|
| `AuthServiceTests` | BCrypt hashing, duplicate-email handling (case-insensitive), login success/failure paths, JWT shape |
| `CarServiceTests` | Cross-dealer isolation on read/update/delete, search filtering (partial + case-insensitive), whitespace trimming |
| `CarServiceAdjustStockTests` | Atomic stock adjustment: increment, decrement, exact zero, would-go-negative, cross-dealer not-found |
| `GlobalExceptionMiddlewareTests` | 500 envelope shape, correlation ID, exception details never leak |
| `ApiIntegrationTests` | End-to-end over the real HTTP pipeline via `WebApplicationFactory<Program>` — full CRUD flow, cross-dealer 404, `/me`, `/health`, **concurrent decrement race** proving stock never goes negative under 20 parallel adjustments, validation failure paths (400/409) |

Unit tests use Microsoft.Data.Sqlite's shared in-memory database; integration tests reuse the same pattern under `WebApplicationFactory`. No files written, no cleanup needed.

### Continuous integration

Every push and pull request runs `dotnet build && dotnet test` on Ubuntu via GitHub Actions (`.github/workflows/ci.yml`). The badge at the top of this README reflects the latest run.

---

## How to exercise the API

`requests.http` at the repo root drives the full happy path plus negative cases (cross-dealer 404, validation errors, duplicate registration). Open it in Visual Studio 2022, Rider, or VS Code with the REST Client extension. Login first, paste the returned tokens into `@tokenA` and `@tokenB`, then run the rest of the file top to bottom.

Or hit Swagger UI: click **Authorize**, paste a JWT, and use the interactive forms.

### Endpoint summary

| Method | Route | Auth | Returns |
|---|---|---|---|
| POST   | `/api/auth/register`              | no  | 201, 400, 409, **429** |
| POST   | `/api/auth/login`                 | no  | 200 + token, 400, 401, **429** |
| GET    | `/api/auth/me`                    | JWT | 200 + profile, 401 |
| GET    | `/api/cars`                       | JWT | 200, 401 |
| GET    | `/api/cars?make=…&model=…`        | JWT | 200, 401 — partial, case-insensitive |
| POST   | `/api/cars`                       | JWT | 201 + `Location`, 400, 401 |
| GET    | `/api/cars/{id}`                  | JWT | 200, 401, 404 |
| DELETE | `/api/cars/{id}`                  | JWT | 204, 401, 404 |
| PATCH  | `/api/cars/{id}/stock`            | JWT | 200, 400, 401, 404 — sets stock |
| PATCH  | `/api/cars/{id}/stock/adjust`     | JWT | 200, 400, 401, 404 — atomic delta |
| GET    | `/health`                         | no  | 200, 503 |

### Example: register → login → add a car

```bash
curl -X POST http://localhost:5266/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"dealer@example.com","password":"SecurePass1!"}'

TOKEN=$(curl -s -X POST http://localhost:5266/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"dealer@example.com","password":"SecurePass1!"}' \
  | jq -r .token)

curl -X POST http://localhost:5266/api/cars \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"make":"Audi","model":"A4","year":2018,"stock":5}'
```

---

## Architecture

```
src/OracleCMS.CarStock.API/
├── Controllers/        HTTP only: routing, status codes, DTO mapping
├── Services/           Business logic (and the implementation surface the tests exercise)
├── Repositories/       Dapper raw SQL — parameterized only
├── Entities/           Domain types (Dealer, Car)
├── DTOs/               Auth and Cars request/response shapes
├── Data/               DatabaseInitializer + SqliteConnectionFactory
├── Middleware/         GlobalExceptionMiddleware
└── Program.cs          Composition root: DI, JWT, Swagger, middleware order
```

Strict layered separation: controllers never talk to repositories directly, repositories never contain business rules, services never serialize JSON.

### Multi-tenancy

- **Dealer ID is always pulled from the JWT's `NameIdentifier` claim.** It is never read from request bodies or query strings — there is no path by which a dealer can act on another dealer's data.
- Every car-table query is parameterized with `DealerId`. `UPDATE` and `DELETE` use `WHERE Id = @Id AND DealerId = @DealerId`; if zero rows are affected, the controller returns **404**, not 403, so resource existence is never leaked.
- `CarResponse` deliberately omits `DealerId` — it's implicit from the caller's own token.

### Security

| Concern | Implementation |
|---|---|
| Passwords | BCrypt with work factor 12 (`BCrypt.Net-Next`). Stored hashes verified to start with `$2…` by an automated test. Registration enforces complexity via a custom `[PasswordComplexity]` attribute: uppercase, lowercase, digit, and special character all required. |
| SQL injection | All queries use Dapper's `@Name` parameters bound via anonymous objects. No string interpolation, no concatenation. Tests cover the search path. |
| JWT signing | HMAC-SHA256 with a secret loaded from configuration; startup refuses to boot if the secret is the documented placeholder or shorter than 32 bytes. |
| Unhandled errors | `GlobalExceptionMiddleware` logs the full exception server-side with a correlation ID; the client receives only `{ "error": "An unexpected error occurred.", "correlationId": "…" }`. No stack traces, no exception types in responses. |
| Login enumeration | Wrong password and unknown email return the same 401 message. |
| Brute force | `/api/auth/register` and `/api/auth/login` are behind a fixed-window rate limiter (30 requests / minute / IP). 31st request returns 429. |
| User enumeration via timing | Login runs `BCrypt.Verify` against a placeholder hash even when the email is unknown, so the wrong-password path and the no-such-account path take the same time. Without this, ~250 ms of BCrypt work would reveal which emails are registered. |
| Hash-name confusion | The `Dealer.PasswordHash` property name makes it explicit that the stored value is a BCrypt hash, not a plaintext password. SQL column remains `Password` for schema simplicity; mapped via `SELECT Password AS PasswordHash`. |
| Dependency vulnerabilities | `dotnet list package --vulnerable --include-transitive` is clean for both the API and the test project. The known-vulnerable `System.Net.Http 4.3.0` and `System.Text.RegularExpressions 4.3.0` that ship transitively with older `Microsoft.NET.Test.Sdk` are overridden with patched 4.3.4 / 4.3.1 references. |
| Stock race conditions | `PATCH /api/cars/{id}/stock/adjust` performs `UPDATE … SET Stock = Stock + @Delta WHERE … AND Stock + @Delta >= 0` in a single SQL statement. Concurrent decrements compose correctly — proven by an integration test that fires 20 parallel `-1` adjustments against a stock of 10 and asserts exactly 10 succeed, 10 are rejected, and the final stock is 0. |

### Error response shape

All client errors:

```json
{ "error": "Validation failed", "detail": "Year must be between 1886 and next year." }
```

Unhandled server errors (500):

```json
{ "error": "An unexpected error occurred.", "correlationId": "a3f8b1c2" }
```

---

## Configuration

`appsettings.json` ships with a placeholder JWT secret. Real values live in:

| File | Purpose |
|---|---|
| `appsettings.json` | Defaults — **placeholder secret on purpose**. Boot will fail if you try to run with it. |
| `appsettings.Development.json` | Dev-only secret so `dotnet run` works on a fresh clone. **Not for production.** |
| Environment variables | Override anything: `Jwt__Secret`, `Jwt__ExpiryMinutes`, `ConnectionStrings__DefaultConnection`, … |

```json
"Jwt": {
  "Secret": "…32+ chars…",
  "Issuer": "OracleCMS.CarStock",
  "Audience": "OracleCMS.CarStock.Dealers",
  "ExpiryMinutes": 60
}
```

The SQLite file path is `Data/carstock.db` relative to the API project. The directory and schema are created on startup if they don't exist.

---

## Database schema

```sql
CREATE TABLE Dealers (
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    Email    TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    Password TEXT    NOT NULL                       -- BCrypt hash
);

CREATE TABLE Cars (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    DealerId  INTEGER NOT NULL REFERENCES Dealers(Id),
    Make      TEXT    NOT NULL,
    Model     TEXT    NOT NULL,
    Year      INTEGER NOT NULL,
    Stock     INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    UpdatedAt TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
);

CREATE INDEX IX_Cars_DealerId ON Cars(DealerId);
CREATE INDEX IX_Cars_DealerId_Make_Model ON Cars(DealerId, Make, Model);
```

---

## Project layout

```
OracleCMS.CarStock.slnx
├── src/OracleCMS.CarStock.API/        ASP.NET Core 8 web API
└── tests/OracleCMS.CarStock.Tests/    xUnit, in-memory SQLite
README.md
requests.http
.gitignore
```

---

## Notable decisions

- **404 not 403 on cross-dealer access** — preserves indistinguishability between "not yours" and "doesn't exist."
- **`UPDATE … WHERE Id=@Id AND DealerId=@DealerId`** — atomic ownership check in SQL; no read-then-write race window.
- **`COLLATE NOCASE` on the `Dealers.Email` column + unique index** — case-insensitive uniqueness enforced by the DB, not just by the service.
- **`public partial class Program;`** — top-level statement programs generate an implicit, inaccessible `Program` class; the `partial` declaration makes it visible across assembly boundaries so `WebApplicationFactory<Program>` in the integration test suite can boot a real in-process server against the same composition root the production API uses.
- **JWT placeholder guard** — `Program.cs` refuses to start if the secret is still the documented placeholder. Better to fail loud than ship a misconfigured prod.
- **Dynamic year validation** — `[Range(1886, 9999)]` plus a runtime check against `DateTime.UtcNow.Year + 1` (data annotations can't reference runtime values).
- **`GetById` is exposed in addition to the spec's four required car operations** — needed to back `CreatedAtAction`'s `Location` header on POST. Useful for clients that want to refetch a single car by ID.
- **Two stock-mutation endpoints** — `PATCH /stock` overwrites, `PATCH /stock/adjust` applies a delta. The adjust variant is the one to reach for in real client code: it's atomic, concurrent-safe, and refuses to take stock negative. The set-variant is kept for ergonomics (single PATCH to fix bookkeeping).
- **Rate-limited auth endpoints** — 30 requests/minute/IP via .NET 8's built-in `RateLimiter`. Tight enough to deter brute force; loose enough not to interfere with normal usage or integration tests.
- **`GET /api/auth/me`** — convenience endpoint for clients that need to display the logged-in dealer without round-tripping the email on each request. Doubles as a quick token-validity probe.
- **`GET /health`** — opens a SQLite connection and runs `SELECT 1`. Returns 200 + `{status: "Healthy"}` or 503 + diagnostics. Ready for load balancers or Kubernetes liveness probes.
- **Integration tests via `WebApplicationFactory<Program>`** — only the connection string is overridden (to a per-fixture in-memory SQLite). JWT secret comes from `appsettings.Development.json` so AuthService and the bearer middleware automatically agree.

---

## What I'd build next

| # | Feature | Why it matters |
|---|---|---|
| 1 | **Refresh token rotation** | JWT expiry is currently 60 minutes. Short-lived access tokens (5–15 min) paired with rotating refresh tokens stored server-side allow a compromised token to be revoked instantly without forcing the dealer to re-authenticate. The current design has no revocation path — once a token is signed, it's valid until expiry. |
| 2 | **Pagination on `GET /api/cars`** | A dealer with 5,000 cars in stock receives all of them in a single response today. Adding `page` / `pageSize` query parameters and a `X-Total-Count` header keeps response sizes predictable, makes the endpoint cacheable per page, and is a prerequisite for any frontend list view. |
| 3 | **Structured logging (Serilog)** | The built-in `ILogger` writes plain text. Serilog with JSON sinks (Seq, Datadog, CloudWatch) emits correlation IDs, dealer IDs, and request durations as queryable fields rather than substrings to grep for. This is the difference between `SELECT * WHERE correlationId = '…'` and `grep '…' /var/log/*.log`. |
| 4 | **Distributed rate limiting (Redis)** | The current `FixedWindowRateLimiter` is in-process. A load-balanced deployment with N instances gives each dealer N × 30 attempts per minute because each instance maintains its own counter. Moving the counter to Redis makes the limit global regardless of how many instances are running. |
| 5 | **OpenTelemetry traces** | When a request is slow it's currently opaque — is it BCrypt, the DB query, or middleware? Adding trace context propagation (via `System.Diagnostics.Activity` + an OTLP exporter) lets a single slow request be broken down into labelled spans without manually timing each layer. |
| 6 | **Role-based permissions** | Any authenticated dealer can mutate their own stock. A real system distinguishes a "stock manager" role (read + write) from a "viewer" role (read-only). This maps cleanly to JWT claims + ASP.NET Core policy-based authorization and requires no schema changes — just an extra claim at registration and `[Authorize(Policy = "StockManager")]` on mutating endpoints. |
| 7 | **Docker + Compose file** | A `docker compose up` that starts the API with an injected `Jwt__Secret` environment variable and mounts a volume for the SQLite file removes the "install .NET 8 SDK" prerequisite for evaluators and mirrors a minimal production deployment. |
| 8 | **Password hardening beyond complexity rules** | `RegisterRequest` already enforces complexity via `[PasswordComplexity]` (uppercase, digit, special character required). The next step is a HIBP (Have I Been Pwned) breach-check at registration: hash the password with SHA-1, send the first 5 hex characters to the k-anonymity API, and reject passwords that appear in known breach sets — without ever sending the full password to a third party. |

