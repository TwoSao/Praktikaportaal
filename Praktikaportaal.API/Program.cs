using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Praktikaportaal.API.ErrorHandling;
using Praktikaportaal.API.WebSockets;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Infrastructure;
using Praktikaportaal.Infrastructure.Options;
using Praktikaportaal.Infrastructure.Persistence.Seed;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger catches startup errors before configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// ── Logging: Serilog (console + rolling file), configured in the "Serilog" section ──
// preserveStaticLogger: several hosts can live in one process (integration tests); with the default the
// first host freezes the shared bootstrap logger and the next one fails with "The logger is already frozen".
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext(), preserveStaticLogger: true);

// ── Error handling: RFC 7807 ProblemDetails for every error response ──────
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Infrastructure & Database ──────────────────────────────────────────────
builder.Services.AddInfrastructureServices(builder.Configuration);

// Uploaded files live outside wwwroot; a relative path is resolved against the content root.
builder.Services.PostConfigure<FileStorageOptions>(o =>
    o.RootPath = Path.GetFullPath(o.RootPath, builder.Environment.ContentRootPath));

// ── Authentication & JWT ───────────────────────────────────────────────────
// Token validation uses the same validated JwtOptions as token generation (no fallback secrets).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        // Browsers cannot send an Authorization header on a WebSocket, so /ws/* accepts ?access_token=.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                string? token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ApplicationsSocketEndpoint.IsSocketPath(ctx.HttpContext.Request.Path))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ── Real-time: WebSocket connections + the notifier the services layer calls ─
builder.Services.AddSingleton<ConnectionGroups>();
builder.Services.AddSingleton<Praktikaportaal.Core.Interfaces.IApplicationNotifier, WebSocketApplicationNotifier>();

// ── CORS: only the configured client origins (Web MVC dev server etc.) ────
const string ClientCorsPolicy = "Clients";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Controllers: enums as strings in JSON ("Submitted" instead of 0) ──────
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Swagger / OpenAPI: XML docs, examples, JWT "Authorize" button ─────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Praktikaportaal API",
        Version = "v1",
        Description = "Internship portal: students, companies, internships, applications. " +
                      "Log in via POST /api/auth/login, then press Authorize and paste the token."
    });

    // XML comments from the API (controllers) and Core (DTOs, incl. <example> values).
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(LoginDto).Assembly.GetName().Name}.xml"));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT from /api/auth/login (paste the token only, without 'Bearer ')."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// ── Database migrations + demo seed data (enabled in Development) ─────────
if (app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.InitializeDatabaseAsync();
}

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseExceptionHandler();   // exceptions → ProblemDetails (GlobalExceptionHandler)
app.UseStatusCodePages();    // empty 401/403/404 responses → ProblemDetails
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Praktikaportaal API v1"));
    // Alternative UI on /scalar, reading the same Swagger document.
    app.MapScalarApiReference(o => o.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json"));
}

app.UseHttpsRedirection();
app.UseCors(ClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.MapControllers();
app.MapApplicationsSocket();   // /ws/applications

await app.RunAsync();

/// <summary>Makes the entry point visible to WebApplicationFactory in integration tests.</summary>
public partial class Program;
