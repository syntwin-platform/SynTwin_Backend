using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace Syntwin.Integration.Tests.Infrastructure;

public sealed class FactoryRunApiFixture : IAsyncLifetime
{
    public static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CompanyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid RobotOneId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid RobotTwoId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    private readonly MsSqlContainer _sql = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04")
        .WithPassword("Syntwin_tests_Only!2026")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;
    public FactoryRunFailurePlan FailurePlan { get; } = new();
    public InMemoryRobotBusyLock BusyLock { get; } = new();
    public RecordingCommandQueue CommandQueue { get; private set; } = null!;
    public RecordingCommandTimeoutScheduler TimeoutScheduler { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        CommandQueue = new RecordingCommandQueue(FailurePlan);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("IntegrationTest");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SyntwinDb"] = GetApplicationConnectionString(),
                    ["Redis:ConnectionString"] = "localhost:1,abortConnect=false,connectTimeout=100",
                    ["InfluxDb:Enabled"] = "false",
                    ["Email:Enabled"] = "false",
                    ["SeedSuperAdmin:Enabled"] = "false",
                    ["Jwt:Issuer"] = "integration-tests",
                    ["Jwt:Audience"] = "integration-tests",
                    ["Jwt:SigningKey"] = "integration-tests-signing-key-must-be-at-least-32-bytes"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDistributedLock>();
                services.RemoveAll<IRobotBusyLock>();
                services.RemoveAll<IRobotCommandQueue>();
                services.RemoveAll<IRobotCommandTimeoutScheduler>();
                services.RemoveAll<IFactoryRunProgramPreparationExecutor>();

                services.AddSingleton(FailurePlan);
                services.AddSingleton(BusyLock);
                services.AddSingleton<IRobotBusyLock>(BusyLock);
                services.AddSingleton(CommandQueue);
                services.AddSingleton<IRobotCommandQueue>(CommandQueue);
                services.AddSingleton(TimeoutScheduler);
                services.AddSingleton<IRobotCommandTimeoutScheduler>(TimeoutScheduler);
                services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
                services.AddScoped<IFactoryRunProgramPreparationExecutor, DatabaseProgramPreparationExecutor>();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            });
        });

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await ResetAsync();
    }

    public async Task ResetAsync()
    {
        FailurePlan.Reset();
        BusyLock.Reset();
        CommandQueue.Reset();
        TimeoutScheduler.Reset();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyntwinDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        var user = new User
        {
            Id = UserId,
            Email = "factory.integration@syntwin.test",
            PasswordHash = "not-used",
            Status = UserStatus.Active
        };
        var company = new Company
        {
            Id = CompanyId,
            Name = "Factory Integration",
            Slug = "factory-integration",
            Status = CompanyStatus.Active,
            CreatedByUserId = UserId
        };

        dbContext.Users.Add(user);
        dbContext.Companies.Add(company);
        dbContext.CompanyMembers.Add(new CompanyMember
        {
            CompanyId = CompanyId,
            UserId = UserId,
            Role = CompanyMemberRole.Owner,
            IsActive = true
        });
        dbContext.Robots.AddRange(
            CreateRobot(RobotOneId, "Line 1"),
            CreateRobot(RobotTwoId, "Line 2"));
        await dbContext.SaveChangesAsync();
    }

    public async Task<T> WithDbContextAsync<T>(Func<SyntwinDbContext, Task<T>> action)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyntwinDbContext>();
        return await action(dbContext);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _sql.DisposeAsync();
    }

    private static Robot CreateRobot(Guid id, string suffix) => new()
    {
        Id = id,
        UserId = UserId,
        CompanyId = CompanyId,
        RobotName = $"Fairino FR5 - {suffix}",
        Model = "Fairino FR5",
        Status = RobotStatus.Registered
    };

    private string GetApplicationConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "SyntwinIntegration"
        };
        return builder.ConnectionString;
    }
}
