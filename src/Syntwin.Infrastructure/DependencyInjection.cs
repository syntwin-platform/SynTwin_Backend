using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Infrastructure.Auth;
using Syntwin.Infrastructure.Persistence;
using Syntwin.Application.Auth.Interfaces;
using Syntwin.Application.Auth.Services;
using Syntwin.Application.SubscriptionPlans.Interfaces;
using Syntwin.Application.SubscriptionPlans.Services;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Infrastructure.Email;
using Syntwin.Application.Users.Services;
using Syntwin.Application.Admin.Interfaces;
using Syntwin.Application.Admin.Services;
using Syntwin.Application.Payments.Interfaces;
using Syntwin.Infrastructure.Payments.VnPay;
using Syntwin.Application.Payments.Services;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Services;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Commands.Services;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.Devices.Services;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.RobotPrograms.Services;
using StackExchange.Redis;
using Syntwin.Infrastructure.Robots;


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

        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is required.");
        }

        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));

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
        services.AddScoped<IRobotService, RobotService>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRobotCommandRepository, RobotCommandRepository>();
        services.AddScoped<IRobotCommandService, RobotCommandService>();
        services.AddScoped<IDeviceGatewayService, DeviceGatewayService>();
        services.AddScoped<IRobotProgramRepository, RobotProgramRepository>();
        services.AddScoped<IRobotProgramService, RobotProgramService>();
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
