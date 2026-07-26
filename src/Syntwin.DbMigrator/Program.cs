using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Syntwin.Infrastructure;
using Syntwin.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSyntwinPersistence(builder.Configuration);

using var host = builder.Build();

var logger = host.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("Syntwin.DbMigrator");

try
{
    await using var scope = host.Services.CreateAsyncScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<SyntwinDbContext>();

    logger.LogInformation("Starting database migration.");

    await dbContext.Database.MigrateAsync();

    await host.Services.SeedSuperAdminAsync(
        builder.Configuration);

    logger.LogInformation("Database migration completed successfully.");

    return 0;
}
catch (Exception exception)
{
    logger.LogCritical(
        exception,
        "Database migration failed.");

    return 1;
}