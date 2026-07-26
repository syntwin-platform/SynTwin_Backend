using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Syntwin.Infrastructure.Persistence;

public static class SyntwinPersistenceExtensions
{
    public static IServiceCollection AddSyntwinPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SyntwinDbContext>(options =>
        {
            var connectionString =
                configuration.GetConnectionString("SyntwinDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:SyntwinDb is required.");
            }

            options.EnableSensitiveDataLogging(false);

            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);

                    sqlOptions.CommandTimeout(60);
                });
        });

        return services;
    }
}
