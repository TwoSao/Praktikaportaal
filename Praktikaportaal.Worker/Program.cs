using Microsoft.Extensions.Options;
using Praktikaportaal.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(o => o.Validate() is null, "Invalid Worker configuration (see the log for details).")
    .ValidateOnStart();

// Report *what* is wrong with the configuration before the host fails on startup.
var configured = builder.Configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>() ?? new WorkerOptions();
if (configured.Validate() is { } problem)
{
    Console.Error.WriteLine($"Worker configuration error: {problem}");
    return 1;
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ILastRunStore, JsonFileLastRunStore>();
builder.Services.AddScoped<DigestJob>();

// The only way the Worker reaches the system is this HTTP client (no database, no shared services).
builder.Services.AddHttpClient<IServiceApiClient, ServiceApiClient>((sp, client) =>
{
    client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<WorkerOptions>>().Value.ApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
return 0;
