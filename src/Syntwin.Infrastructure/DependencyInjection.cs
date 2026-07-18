using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Syntwin.Application.Admin.Interfaces;
using Syntwin.Application.Admin.Services;
using Syntwin.Application.AdminCompanies.Interfaces;
using Syntwin.Application.AdminCompanies.Services;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Auth.Interfaces;
using Syntwin.Application.Auth.Services;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Commands.Services;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.Companies.Services;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.Devices.Services;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.FactoryRuns.Services;
using Syntwin.Application.FactoryRuns.Strategies;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.LuaParsing.Services;
using Syntwin.Application.Payments.Interfaces;
using Syntwin.Application.Payments.Services;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.RobotPrograms.Services;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Application.Robots.Services;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Services;
using Syntwin.Application.SubscriptionPlans.Interfaces;
using Syntwin.Application.SubscriptionPlans.Services;
using Syntwin.Application.Telemetry.Interfaces;
using Syntwin.Application.Telemetry.Services;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Application.Users.Services;
using Syntwin.Infrastructure.Auth;
using Syntwin.Infrastructure.Email;
using Syntwin.Infrastructure.Payments.VnPay;
using Syntwin.Infrastructure.Persistence;
using Syntwin.Infrastructure.Robots;
using Syntwin.Infrastructure.Telemetry;
using Syntwin.Infrastructure.FactoryRuns;

namespace Syntwin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(options =>
        {
            var jwtSection = configuration.GetSection("Jwt");

            options.Issuer = jwtSection["Issuer"] ?? string.Empty;
            options.Audience = jwtSection["Audience"] ?? string.Empty;
            options.SigningKey = jwtSection["SigningKey"] ?? string.Empty;

            if (int.TryParse(jwtSection["AccessTokenExpirationMinutes"], out var expirationMinutes))
            {
                options.AccessTokenExpirationMinutes = expirationMinutes;
            }
        });

        services.Configure<VnPayOptions>(options =>
        {
            var vnPaySection = configuration.GetSection("VnPay");

            options.PaymentUrl = vnPaySection["PaymentUrl"] ?? options.PaymentUrl;
            options.ReturnUrl = vnPaySection["ReturnUrl"] ?? options.ReturnUrl;
            options.ClientReturnUrl = vnPaySection["ClientReturnUrl"] ?? options.ClientReturnUrl;
            options.IpnUrl = vnPaySection["IpnUrl"] ?? options.IpnUrl;
            options.TmnCode = vnPaySection["TmnCode"] ?? options.TmnCode;
            options.HashSecret = vnPaySection["HashSecret"] ?? options.HashSecret;
            options.Version = vnPaySection["Version"] ?? options.Version;
            options.Command = vnPaySection["Command"] ?? options.Command;
            options.CurrencyCode = vnPaySection["CurrencyCode"] ?? options.CurrencyCode;
            options.Locale = vnPaySection["Locale"] ?? options.Locale;
        });

        services.AddDbContext<SyntwinDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("SyntwinDb"));
        });
        services.Configure<RobotRuntimeOptions>(configuration.GetSection("RobotRuntime"));
        services.Configure<InfluxDbOptions>(configuration.GetSection("InfluxDb"));
        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is required.");
        }
        services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddSingleton<IRobotRuntimeMetrics, RobotRuntimeMetrics>();

        if (configuration.GetValue<bool>("InfluxDb:Enabled"))
        {
            services.AddSingleton<IRobotTelemetryHistoryWriter, InfluxRobotTelemetryHistoryWriter>();
            services.AddSingleton<IRobotTelemetryHistoryReader, InfluxRobotTelemetryHistoryReader>();
        }
        else
        {
            services.AddSingleton<IRobotTelemetryHistoryWriter, NoopRobotTelemetryHistoryWriter>();
            services.AddSingleton<IRobotTelemetryHistoryReader, NoopRobotTelemetryHistoryReader>();
        }
        services.AddScoped<IRobotStateCache, RedisRobotStateCache>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<IVnPayGateway, VnPayGateway>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRobotRepository, RobotRepository>();
        services.AddScoped<IRobotModelRepository, RobotModelRepository>();
        services.AddScoped<IRobotModelService, RobotModelService>();
        services.AddScoped<IRobotRuntimeSessionRepository, RobotRuntimeSessionRepository>();
        services.AddScoped<IRobotAccessService, RobotAccessService>();
        services.AddScoped<IRobotService, RobotService>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRobotCommandRepository, RobotCommandRepository>();
        services.AddScoped<IRobotCommandQueue, RedisRobotCommandQueue>();
        services.AddScoped<IRobotBusyLock, RedisRobotBusyLock>();
        services.AddScoped<IRobotCommandTimeoutScheduler, RedisRobotCommandTimeoutScheduler>();
        services.AddScoped<IRobotCommandService, RobotCommandService>();
        services.AddScoped<IFactoryRunRepository, FactoryRunRepository>();
        services.AddScoped<IFactoryRunProgramPreparationExecutor, ScopedFactoryRunProgramPreparationExecutor>();
        services.AddScoped<IFactoryRunExecutionStrategy, ParallelIndependentFactoryRunStrategy>();
        services.AddScoped<IFactoryRunExecutionStrategy, SynchronizedFactoryRunStrategy>();
        services.AddScoped<FactoryRunExecutionStrategyResolver>();
        services.AddScoped<IFactoryRunService, FactoryRunService>();
        services.AddScoped<IDeviceGatewayService, DeviceGatewayService>();
        services.AddScoped<IRobotProgramRepository, RobotProgramRepository>();
        services.AddScoped<IRobotProgramService, RobotProgramService>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IAdminCompanyService, AdminCompanyService>();
        services.AddScoped<ILuaProgramParser, LuaProgramParser>();
        services.AddScoped<ILuaProgramImportMapper, LuaProgramImportMapper>();
        services.AddScoped<ILuaProgramImportService, LuaProgramImportService>();
        services.AddScoped<IRobotSafetyPolicyService, RobotSafetyPolicyService>();
        services.AddScoped<IRobotSafetyPolicyRepository, RobotSafetyPolicyRepository>();
        services.AddScoped<IRobotSafetyDefaultPolicyFactory, RobotSafetyDefaultPolicyFactory>();
        services.AddScoped<IRobotSafetyPolicyProvider, DefaultRobotSafetyPolicyProvider>();
        services.AddScoped<IRobotSafetyValidationService, RobotSafetyValidationService>();
        services.Configure<EmailOptions>(options =>
        {
            var emailSection = configuration.GetSection("Email");

            if (bool.TryParse(emailSection["Enabled"], out var enabled))
            {
                options.Enabled = enabled;
            }

            options.FromEmail = emailSection["FromEmail"] ?? options.FromEmail;
            options.FromName = emailSection["FromName"] ?? options.FromName;
            options.Host = emailSection["Host"] ?? options.Host;
            options.Username = emailSection["Username"] ?? options.Username;
            options.Password = emailSection["Password"] ?? options.Password;

            if (int.TryParse(emailSection["Port"], out var port))
            {
                options.Port = port;
            }
        });
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
