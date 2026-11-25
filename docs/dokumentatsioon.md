# Praktikaportaal – projekti dokumentatsioon

Autor: Illia B. (TwoSao) · Repositoorium: <https://github.com/TwoSao/Praktikaportaal>

## 1. Projekti eesmärk

Praktikaportaal ühendab **praktikakohta otsivad õppurid** ja **praktikakohti pakkuvad ettevõtted**.
Õppur koostab profiili (oskused, CV), otsib praktikakohti ja esitab avaldusi. Ettevõte avaldab praktikakohti,
vaatab avaldusi läbi ja muudab nende staatust. Administraator näeb portaali statistikat.
Taustateenus (Worker) saadab iga päev õppuritele soovitused sobivatest uutest praktikakohtadest.

### Kasutajarollid

| Roll | Mida saab teha |
|---|---|
| **Student** (õppur) | registreeruda, hallata profiili ja oskusi, laadida üles CV, otsida praktikakohti, esitada ja tagasi võtta avaldusi, näha soovitusi ja teavitusi |
| **Company** (ettevõte) | registreeruda, hallata oma praktikakohti, vaadata oma kohtade avaldusi, muuta avalduse staatust, laadida alla CV, saada reaalajas teavitusi |
| **Admin** | vaadata avalduste statistikat, modereerida (peita/kustutada) praktikakohti |
| **Service** | Workeri tehniline konto: loeb õppurite nimekirja ja soovitusi, loob teavitusi |

## 2. Arhitektuur

Lahendus koosneb seitsmest projektist. Sõltuvused kulgevad ainult sissepoole.

```
 Brauser ──► Praktikaportaal.Web (MVC) ──HTTP──►┐
 Worker  ─────────────────────────────HTTP─────►├─► Praktikaportaal.API ─► Infrastructure ─► Core
 Brauser ◄──WebSocket (proxy Web kaudu)◄────────┘                │
                                                                 └─► SQL Server (LocalDB)
```

| Projekt | Vastutus |
|---|---|
| `Praktikaportaal.Core` | olemid, DTO-d, domeeniteenused (äriloogika), repositooriumide ja teenuste liidesed, domeenivead; ei sõltu raamistikest |
| `Praktikaportaal.Infrastructure` | EF Core `DbContext`, konfiguratsioonid, migratsioonid, repositooriumid, Unit of Work, JWT, paroolide räsi, failisalvestus, algandmed |
| `Praktikaportaal.API` | REST API kontrollerid, ProblemDetails veakäsitlus, Serilog, Swagger, JWT autentimine, WebSocket lõpp-punkt |
| `Praktikaportaal.Web` | ASP.NET Core MVC veebileht (eestikeelne), suhtleb API-ga üle HTTP, WebSocketi vahendaja (proxy) |
| `Praktikaportaal.Worker` | Worker Service: iga päev kell 07:00 soovituste kokkuvõte; kasutab ainult HTTP-d |
| `Praktikaportaal.Tests` | ühik- ja integratsioonitestid (xUnit, Moq, `WebApplicationFactory`) |
| `Praktikaportaal.Web.Tests` | MVC lehe testid (eraldi projekt, sest API-l ja Webil on kummalgi oma `Program`) |

### Põhilised disainiotsused

- **Kihiline arhitektuur ja Repository + Unit of Work** – kontrollerid on õhukesed, äriloogika on `Core` teenustes
  (`ApplicationService`, `InternshipService`, `RecommendationService` jt), mida saab Moq-iga testida.
- **Rollipõhine ligipääs** – JWT sisaldab rolli; lisaks kontrollitakse API-s omanikku (ettevõte näeb ainult enda
  avaldusi, õppur ainult enda andmeid).
- **Ühtne veavorm** – kõik vead tagastatakse `application/problem+json` (RFC 7807) kujul: 400 valideerimine /
  äriloogika, 401, 403, 404, 409; veateated ei sisalda stack trace'i.
- **Logimine** – Serilog (konsool + `logs/api-.log`, päevane rull, 14 päeva).
- **Pehme kustutamine** – `IsDeleted` lipp ja globaalsed päringufiltrid.
- **Turvalisus** – paroolid räsitud; CV-d salvestatakse väljaspool `wwwroot`-i juhuslike nimedega, kontrollitakse
  suurust, laiendit ja faili allkirja (PDF/DOCX, kuni 5 MB); Web hoiab JWT-d HttpOnly küpsises, kasutab
  anti-forgery tokeneid ja lubab ainult kohalikke `returnUrl` väärtusi; Workeri parool tuleb user-secrets'ist või
  keskkonnamuutujast.

### Avalduse staatuse reeglid

```
Submitted ──► Viewed | Rejected
Viewed    ──► Interview | Rejected
Interview ──► Accepted | Rejected
Submitted / Viewed / Interview ──► Withdrawn (ainult õppur)
Accepted, Rejected, Withdrawn on lõppstaatused
```

Lubamatu üleminek annab vea 400. Üks õppur saab ühele praktikakohale esitada ainult ühe avalduse (409).

### Soovitused

Õppuri `S` ja praktikakoha nõutavate oskuste `R` korral: `kattuvus % = |S ∩ R| / |R| × 100` (ümardatud 0,1-ni).
Alla `minOverlap` jäävad kohad jäetakse välja, ülejäänud sorteeritakse kattuvuse ja seejärel uuemuse järgi.
Vastuses on ka olemasolevad ja puuduvad oskused.

### Reaalajas teavitused (WebSocket)

`ws://localhost:5117/ws/applications?access_token=<JWT>` – ainult rollile Company. Kui õppur esitab avalduse
ettevõtte praktikakohale, saab ainult see ettevõte teate `ApplicationReceived`. Ühendus suletakse JWT aegumisel.
MVC leht avab brauseri jaoks oma WebSocketi (Origin kontroll) ja vahendab teateid API-st, nii et token ei satu
kunagi brauseri skriptidesse. Katkestuse korral ühendub leht ise uuesti.

### Worker

`BackgroundService` + `PeriodicTimer`, käivitub iga päev kell `Worker:RunAt` (vaikimisi 07:00). Logib API-sse
teenusekontoga sisse (401 korral uuesti), küsib õppurite nimekirja ja igaühe soovitused alates viimasest käivitusest
ning loob teavituse `POST /api/notifications`. Viimase käivituse aeg on failis `worker-state.json`, nii et
kordusjooks ei saada duplikaate. Workeril ei ole viidet `Core` ega `Infrastructure` projektile.

## 3. Andmebaas

SQL Server (LocalDB arenduses), EF Core Code First, migratsioonid projektis `Praktikaportaal.Infrastructure`.
Kõik olemid (v.a. ühendustabelid) pärivad `BaseEntity`: `Id` (Guid), `CreatedAt`, `UpdatedAt`, `IsDeleted`.

```
Users 1──0..1 StudentProfiles 1──* Applications *──1 Internships *──1 CompanyProfiles 1──1 Users
  │                 │  │                                  │
  │                 │  └─* CVs                            └─* InternshipSkills *──1 Skills
  │                 └─* StudentSkills *──1 Skills
  └─* Notifications                      SavedInternships (StudentProfiles *──* Internships)
```

| Tabel | Olulised veerud | Märkused |
|---|---|---|
| **Users** | Email (unikaalne), PasswordHash, FirstName, LastName, Role, IsActive | rollid Student, Company, Admin, Service |
| **StudentProfiles** | UserId, SchoolName, StudyProgram, Course, Description, PhoneNumber, Location | 1:1 Users |
| **CompanyProfiles** | UserId, CompanyName, RegistrationCode, Description, Website, Address, ContactEmail, ContactPhone | 1:1 Users |
| **Internships** | CompanyProfileId, Title, Description, Requirements, Field, Location, StartDate, EndDate, ApplicationDeadline, IsRemote, Status | indeksid: Field, Location, CreatedAt; Status: Draft/Published/Closed/Archived |
| **Applications** | StudentProfileId, InternshipId, CoverLetter, Status, AppliedAt | unikaalne indeks (StudentProfileId, InternshipId) |
| **Skills** | Name (unikaalne), Description | |
| **StudentSkills** | StudentProfileId, SkillId, Level | liitvõti; Level: Beginner/Intermediate/Advanced/Expert |
| **InternshipSkills** | InternshipId, SkillId, RequiredLevel | liitvõti |
| **CVs** | StudentProfileId, FileName, StorageKey, ContentType, UploadedAt | fail ise kaustas `App_Data/cvs` |
| **Notifications** | UserId, Title, Message, IsRead | |
| **SavedInternships** | StudentProfileId, InternshipId, SavedAt | liitvõti |

## 4. API lõpp-punktid

Aluse-URL arenduses: `http://localhost:5117`. Swagger: `/swagger`. Vastused on JSON (enum'id tekstina).
Nimekirjad tagastavad `{ items, totalCount, page, pageSize }` ja toetavad `page` ning `pageSize` (1–100).
Valmis päringud: `Praktikaportaal.API/Praktikaportaal.API.http` ja `docs/Praktikaportaal.postman_collection.json`.

| Meetod | Tee | Ligipääs |
|---|---|---|
| POST | `/api/auth/register-student`, `/api/auth/register-company`, `/api/auth/login` | avalik |
| GET | `/api/auth/me` | sisse logitud |
| GET | `/api/skills` | avalik |
| POST | `/api/skills` | Company, Admin |
| GET | `/api/internships` (`search, field, location, isRemote, createdAfter, sort, page, pageSize`) | avalik |
| GET | `/api/internships/filters`, `/api/internships/{id}` | avalik |
| GET | `/api/internships/my` | Company |
| POST | `/api/internships` | Company |
| PUT, DELETE | `/api/internships/{id}` | Company (omanik), Admin |
| PATCH | `/api/internships/{id}/status` | Company (omanik), Admin |
| POST | `/api/internships/{id}/applications` | Student |
| GET | `/api/internships/{id}/applications` | Company (omanik), Admin |
| GET | `/api/applications/my` | Student |
| GET | `/api/applications/{id}` | omanik-õppur, omanik-ettevõte, Admin |
| PATCH | `/api/applications/{id}/status` | Company (omanik) |
| POST | `/api/applications/{id}/withdraw` | Student (omanik) |
| GET | `/api/applications/{id}/cv` | Company (omanik), Admin |
| GET, PUT | `/api/students/me` | Student |
| POST, GET, DELETE | `/api/students/me/cv`; GET `/api/students/me/cv/file` | Student |
| GET | `/api/students` | Service, Admin |
| GET | `/api/students/{id}/recommendations` (`since, minOverlap`) | Student (enda), Service, Admin |
| POST | `/api/notifications` | Service, Admin |
| GET | `/api/notifications/my` (`unreadOnly`) | sisse logitud |
| PATCH | `/api/notifications/{id}/read`; POST `/api/notifications/read-all` | sisse logitud |
| GET | `/api/admin/statistics` (`from, to, period, top`) | Admin |
| WS | `/ws/applications?access_token=<JWT>` | Company |

Veavastuse näide:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Business rule violation",
  "status": 400,
  "detail": "Cannot change application status from Submitted to Accepted."
}
```

## 5. Paigaldus ja käivitamine

Nõuded: .NET 10 SDK, SQL Server LocalDB (või muu SQL Server).

```bash
git clone https://github.com/TwoSao/Praktikaportaal.git
cd Praktikaportaal
dotnet restore
dotnet build
dotnet run --project Praktikaportaal.API      # http://localhost:5117, Swagger /swagger
dotnet run --project Praktikaportaal.Web      # http://localhost:5026
DOTNET_ENVIRONMENT=Development dotnet run --project Praktikaportaal.Worker -- --Worker:RunOnStartup=true
```

Arenduskeskkonnas (`Database:MigrateAndSeedOnStartup=true`) rakendab API käivitumisel migratsioonid ja lisab
näidisandmed. Puhta andmebaasi saamiseks: `dotnet ef database drop -p Praktikaportaal.Infrastructure -s Praktikaportaal.API`.
Kui Windows Smart App Control blokeerib `.exe` faili, käivita `dotnet <projekt>/bin/Debug/net10.0/<Projekt>.dll`.

### Näidiskontod (ainult Development)

| Roll | E-post | Parool |
|---|---|---|
| Admin | admin@praktikaportaal.ee | Admin123! |
| Service | service@praktikaportaal.ee | Service123! |
| Company | hr@nordicsoftware.ee | Company123! |
| Student | mari.tamm@student.ee | Student123! |

### Seadistused

| Seadistus | Kus | Selgitus |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | API | SQL Serveri ühendus |
| `Jwt:Secret` | API | vähemalt 32 baiti; tootmises keskkonnamuutuja `Jwt__Secret` või user-secrets |
| `Cors:AllowedOrigins` | API | lubatud päritolud |
| `FileStorage:RootPath` | API | CV-de kaust |
| `ApiSettings:BaseUrl` | Web | API aadress |
| `Worker:ServiceEmail`, `Worker:ServicePassword` | Worker | teenusekonto (user-secrets, ei commit'ita) |
| `Worker:RunAt`, `RunOnStartup`, `IntervalSeconds` | Worker | ajastus |

### Docker (API)

```bash
docker build -f Praktikaportaal.API/Dockerfile -t praktikaportaal-api .
docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." -e Jwt__Secret="..." praktikaportaal-api
```

## 6. Testimine

`dotnet test` käivitab ligikaudu 350 automaattesti:

- **Ühikutestid (Moq)** – staatuse reeglid, soovituste arvutus, statistika perioodid, CV reeglid, teenuste loogika
  koos libarepositooriumidega;
- **API integratsioonitestid** – `WebApplicationFactory` + InMemory andmebaas: autentimine, rollid, omaniku kontroll,
  lehekülgede jaotus, ProblemDetails, WebSocket;
- **Worker testid** – ajastus, korduskäivitus, 401 korral uuesti sisselogimine;
- **Web testid** – MVC lehed mõnitatud `IApiClient`-iga, WebSocket proxy päris Kestreli vastu.

Lisaks kontrolliti süsteemi käsitsi töötava API, veebilehe ja SQL Serveriga ning reaalajas uuendust päris Chrome'is
(sh automaatne taasühendus pärast API taaskäivitust). Päringukogumikud: `.http` fail ja Postman.

## 7. Nõuete kontroll

| Nõue | Staatus | Kus |
|---|---|---|
| ASP.NET Core Web API, EF Core, SQL Server | ✔ | `Praktikaportaal.API`, `Infrastructure` |
| Repository + teenuste kiht | ✔ | `Core/Services`, `Infrastructure/Persistence/Repositories` |
| JWT, rollid Student/Company/Admin/Service | ✔ | `AuthController`, `JwtTokenService` |
| ProblemDetails + Serilog | ✔ | `GlobalExceptionHandler`, `Program.cs` |
| Swagger | ✔ | `/swagger` |
| Praktikakohtade otsing, filtrid, sorteerimine, lehekülgede jaotus | ✔ | `GET /api/internships` |
| Avaldused ja staatuse reeglid | ✔ | `ApplicationService`, `ApplicationStatusRules` |
| Õppuri profiil ja oskused | ✔ | `StudentsController` |
| CV üleslaadimine | ✔ | `CvService`, `LocalFileStorage` |
| Soovitused oskuste kattuvuse järgi | ✔ | `RecommendationService` |
| Admini statistika | ✔ | `AdminController`, `/Admin/Statistics` |
| Teavitused | ✔ | `NotificationsController` |
| WebSocket `/ws/applications` | ✔ | `ApplicationsSocketEndpoint` |
| Worker Service (iga päev 07:00, ainult HTTP) | ✔ | `Praktikaportaal.Worker` |
| Veebiklient | ✔ (ASP.NET MVC, kokkuleppel React/MAUI asemel) | `Praktikaportaal.Web` |
| Vähemalt 8 ühikutesti, neist 2 Moq-iga | ✔ | `Praktikaportaal.Tests` |
| .http / Postman kogumik | ✔ | `Praktikaportaal.API.http`, `docs/*.postman_collection.json` |
| README inglise keeles | ✔ | `README.md` |
| Dokumentatsioon eesti keeles | ✔ | see dokument |
| GitHubi planeerimine (repo, tahvel, sildid, issue'd, PR-id) | ✔ | GitHubi projekt ja issue'd |
| MAUI mobiilirakendus | ✘ jäeti tegemata (projekti ulatuse otsus) | issue'd #27 ja #28 suletud |
