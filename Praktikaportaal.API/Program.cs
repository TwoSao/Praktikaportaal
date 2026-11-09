using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Praktikaportaal.API.ErrorHandling;
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
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

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

// ── Controllers & OpenAPI ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors(ClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
