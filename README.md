# Praktikaportaal

An internship portal that connects **students** looking for internships with **companies** offering them.
Students build a profile with skills, upload a CV and apply; companies publish internships, review applicants and
move them through a hiring pipeline; admins see portal-wide statistics. A background worker sends daily
recommendation digests, and companies see new applications live over a WebSocket.

## Features

| Area | What it does |
|---|---|
| Auth | JWT, roles `Student`, `Company`, `Admin`, `Service`; self-registration for students and companies |
| Internships | CRUD, draft/published/closed/archived status, soft delete, search, filters (field, location, remote, date), sorting, paging |
| Applications | Apply with a cover letter, one application per student per internship, status workflow, withdraw |
| Student profile | Personal data, skills with levels, CV upload (PDF/DOCX, max 5 MB), download |
| Recommendations | Internships ranked by skill overlap with the student's skills |
| Statistics (admin) | Applications by status, by field, over time (day/week/month), top internships, portal totals |
| Notifications | In-app notifications for new applications and status changes, unread counter, mark as read |
| Live updates | WebSocket `/ws/applications` pushes `ApplicationReceived` to the owning company |
| Worker | Daily 07:00 digest of new matching internships for every student, talks to the API over HTTP only |
| Web site | ASP.NET Core MVC, Estonian UI, cookie sign-in, live company dashboard |
| Errors / logs | RFC 7807 `ProblemDetails` for every error, Serilog to console and rolling files |
| Docs | Swagger UI, `.http` request collection, Estonian documentation in [docs/](docs) |

### Application status rules

```
Submitted -> Viewed | Rejected
Viewed    -> Interview | Rejected
Interview -> Accepted | Rejected
Accepted, Rejected, Withdrawn are final.   A student can withdraw from Submitted, Viewed and Interview.
```

### Recommendations

For a student `S` and an internship `I` with required skills `R`:
`overlap % = |S ∩ R| / |R| × 100` (rounded to 0.1). Internships below `minOverlap` are dropped, the rest are
sorted by overlap and then by newest. The response also lists matched and missing skills.

## Tech stack

C# / .NET 10 · ASP.NET Core Web API · ASP.NET Core MVC (Razor) · Entity Framework Core 10 · SQL Server (LocalDB)
· JWT bearer · Serilog · Swashbuckle (Swagger) · xUnit + Moq · Docker (API)

## Solution layout

```
Praktikaportaal.Core            entities, DTOs, domain services, repository/service interfaces (no framework deps)
Praktikaportaal.Infrastructure  EF Core, repositories, unit of work, JWT, password hashing, file storage, seeding
Praktikaportaal.API             REST API, ProblemDetails, Serilog, Swagger, WebSocket endpoint
Praktikaportaal.Web             MVC web site (talks to the API over HTTP, WebSocket proxy for live updates)
Praktikaportaal.Worker          Worker Service, daily recommendation digest (HTTP only, no Core/Infrastructure reference)
Praktikaportaal.Tests           unit + API integration tests (xUnit, Moq, WebApplicationFactory)
Praktikaportaal.Web.Tests       MVC site tests (separate project: API and Web both have a top-level Program class)
```

Dependencies point inwards: `API/Web/Worker -> Infrastructure -> Core`; the Web site and the Worker only know the API's
HTTP contract.

## Getting started

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) and SQL Server LocalDB (installed with Visual Studio)
or any SQL Server (change `ConnectionStrings:DefaultConnection`).

```bash
git clone https://github.com/TwoSao/Praktikaportaal.git
cd Praktikaportaal
dotnet restore
dotnet build
```

### 1. API (http://localhost:5117)

```bash
dotnet run --project Praktikaportaal.API
```

With `Database:MigrateAndSeedOnStartup=true` (set in `appsettings.Development.json`) the API applies migrations and seeds demo data on start; in other environments run `dotnet ef database update -p Praktikaportaal.Infrastructure -s Praktikaportaal.API`. Swagger: <http://localhost:5117/swagger>.

To start from a clean database: `dotnet ef database drop -p Praktikaportaal.Infrastructure -s Praktikaportaal.API` and start the API again.

### 2. Web site (http://localhost:5026)

```bash
dotnet run --project Praktikaportaal.Web
```

`ApiSettings:BaseUrl` (default `http://localhost:5117`) must point to the API.

### 3. Worker

```bash
# DOTNET_ENVIRONMENT=Development uses the demo service account; set RunOnStartup to test without waiting for 07:00
DOTNET_ENVIRONMENT=Development dotnet run --project Praktikaportaal.Worker -- --Worker:RunOnStartup=true
```

(PowerShell: `$env:DOTNET_ENVIRONMENT="Development"; dotnet run --project Praktikaportaal.Worker -- --Worker:RunOnStartup=true`)

> On Windows with Smart App Control the generated `.exe` may be blocked; run `dotnet <project>/bin/Debug/net10.0/<Project>.dll` instead.

### Demo accounts (Development seed only)

| Role | E-mail | Password |
|---|---|---|
| Admin | `admin@praktikaportaal.ee` | `Admin123!` |
| Service (Worker) | `service@praktikaportaal.ee` | `Service123!` |
| Company | `hr@nordicsoftware.ee` | `Company123!` |
| Student | `mari.tamm@student.ee` | `Student123!` |

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | API | SQL Server connection string |
| `Jwt:Secret` | API | **Required**, at least 32 bytes. Committed only for Development; in production use environment variable `Jwt__Secret` or user-secrets |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiresInHours` | API | defaults in `appsettings.json` |
| `Cors:AllowedOrigins` | API | list of allowed browser origins |
| `Seed:*Password` | API | demo passwords, set only in `appsettings.Development.json` |
| `FileStorage:RootPath` | API | CV folder (default `App_Data/cvs`, outside `wwwroot`) |
| `ApiSettings:BaseUrl` | Web | URL of the API |
| `Worker:ApiBaseUrl`, `ServiceEmail`, `ServicePassword` | Worker | service account; set with user-secrets or env vars (`Worker__ServicePassword`), never commit |
| `Worker:RunAt`, `RunOnStartup`, `IntervalSeconds`, `StateFilePath`, `MaxItemsInDigest` | Worker | schedule and digest options |

Example for production secrets:

```bash
dotnet user-secrets set "Worker:ServiceEmail" "service@praktikaportaal.ee" --project Praktikaportaal.Worker
dotnet user-secrets set "Worker:ServicePassword" "<password>" --project Praktikaportaal.Worker
```

## API overview

All endpoints are under `/api` and return JSON (enums as strings). Errors use `application/problem+json`:
`400` validation / business rule, `401` not signed in, `403` wrong role or not the owner, `404` not found, `409` conflict.
List endpoints return `{ items, totalCount, page, pageSize }` and accept `page` and `pageSize` (1-100).

| Method | Route | Access |
|---|---|---|
| POST | `/api/auth/register-student`, `/api/auth/register-company`, `/api/auth/login` | public |
| GET | `/api/auth/me` | signed in |
| GET | `/api/skills` | public |
| POST | `/api/skills` | Company, Admin |
| GET | `/api/internships` (`search, field, location, isRemote, createdAfter, sort`), `/filters`, `/{id}` | public |
| GET | `/api/internships/my` | Company |
| POST / PUT / DELETE | `/api/internships`, `/api/internships/{id}` | Company (owner), Admin |
| PATCH | `/api/internships/{id}/status` | Company (owner), Admin |
| POST | `/api/internships/{id}/applications` | Student |
| GET | `/api/internships/{id}/applications` | Company (owner), Admin |
| GET | `/api/applications/my` | Student |
| GET | `/api/applications/{id}` | owner student, owner company, Admin |
| PATCH | `/api/applications/{id}/status` | Company (owner) |
| POST | `/api/applications/{id}/withdraw` | Student (owner) |
| GET | `/api/applications/{id}/cv` | Company (owner), Admin |
| GET / PUT | `/api/students/me` | Student |
| POST / GET / DELETE | `/api/students/me/cv`, `/api/students/me/cv/file` | Student |
| GET | `/api/students` | Service, Admin |
| GET | `/api/students/{id}/recommendations` (`since, minOverlap`) | Student (own), Service, Admin |
| POST | `/api/notifications` | Service, Admin |
| GET | `/api/notifications/my`, PATCH `/{id}/read`, POST `/read-all` | signed in |
| GET | `/api/admin/statistics` (`from, to, period, top`) | Admin |
| WS | `/ws/applications?access_token=<JWT>` | Company |

Ready-made requests for every endpoint: [Praktikaportaal.API/Praktikaportaal.API.http](Praktikaportaal.API/Praktikaportaal.API.http)
(VS Code REST Client or Visual Studio). Swagger UI has the same list with schemas.

### WebSocket

Connect to `ws://localhost:5117/ws/applications?access_token=<company JWT>` (the query string is accepted only for `/ws/*`).
When a student applies to one of the company's internships the server sends a JSON message of type `ApplicationReceived`.
Other companies never receive it. The connection is closed when the token expires. The MVC site proxies this socket
so the token never reaches browser scripts.

## Tests

```bash
dotnet test
```

About 350 tests: domain services (Moq), status rules, recommendations, statistics buckets, CV validation, API integration
tests on an in-memory host (auth, roles, paging, ProblemDetails, WebSocket), Worker tests and MVC site tests with a mocked API client.

## Docker (API)

```bash
docker build -f Praktikaportaal.API/Dockerfile -t praktikaportaal-api .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="<sql server connection string>" \
  -e Jwt__Secret="<at least 32 bytes>" praktikaportaal-api
```

## Security notes

- Passwords are hashed; JWT secret and service-account credentials are never committed outside Development.
- Roles and ownership are enforced in the API (a company only sees its own applicants, a student only their own data).
- CVs are stored outside `wwwroot` under random names, validated by size, extension and file signature.
- The MVC site keeps the JWT in an HttpOnly cookie, uses anti-forgery tokens and only follows local return URLs.
- Validation and business-rule errors never expose stack traces.
