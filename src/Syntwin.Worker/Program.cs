using Syntwin.Hosting.Configuration;
using Syntwin.Hosting.HealthChecks;
using Syntwin.Hosting.Realtime;
using Syntwin.Infrastructure;
using Syntwin.Worker.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
}

if (int.TryParse(builder.Configuration["PORT"], out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSyntwinRealtime(builder.Configuration);
builder.Services.AddSyntwinDependencyHealthChecks();

builder.Services.AddHostedService<RobotOfflineMonitorService>();
builder.Services.AddHostedService<RobotCommandTimeoutMonitorService>();
builder.Services.AddHostedService<FactoryRunLockMaintenanceService>();
builder.Services.AddHostedService<RobotLastSeenFlushService>();

var app = builder.Build();

StartupConfigurationValidator.ValidateWorker(app.Configuration);

app.MapSyntwinHealthEndpoints();

app.Run();
