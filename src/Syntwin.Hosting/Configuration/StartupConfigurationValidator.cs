using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Syntwin.Hosting.Configuration;

public static class StartupConfigurationValidator
{
    public static void ValidateApi(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var errors = new List<string>();

        ValidateRuntimeDependencies(configuration, errors);
        Require(configuration["Jwt:Issuer"], "Jwt:Issuer", errors);
        Require(configuration["Jwt:Audience"], "Jwt:Audience", errors);

        var signingKey = configuration["Jwt:SigningKey"];
        Require(signingKey, "Jwt:SigningKey", errors);

        if (!string.IsNullOrWhiteSpace(signingKey) &&
            Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            errors.Add("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
        {
            errors.Add("Cors:AllowedOrigins must contain at least one origin.");
        }

        foreach (var origin in allowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("Cors:AllowedOrigins contains an invalid HTTP(S) origin.");
            }
        }

        if (configuration.GetValue<bool>("Email:Enabled"))
        {
            Require(configuration["Email:Host"], "Email:Host", errors);
            Require(configuration["Email:FromEmail"], "Email:FromEmail", errors);
        }

        if (environment.IsProduction())
        {
            if (configuration.GetValue<bool>("Swagger:Enabled"))
            {
                errors.Add("Swagger:Enabled must be false in Production.");
            }

            if (configuration.GetValue<bool>("Seed:SuperAdmin:Enabled"))
            {
                errors.Add("Seed:SuperAdmin:Enabled must be false in the API process.");
            }

            Require(configuration["VnPay:TmnCode"], "VnPay:TmnCode", errors);
            Require(configuration["VnPay:HashSecret"], "VnPay:HashSecret", errors);
            Require(configuration["VnPay:ReturnUrl"], "VnPay:ReturnUrl", errors);
            Require(configuration["VnPay:ClientReturnUrl"], "VnPay:ClientReturnUrl", errors);
            Require(configuration["VnPay:IpnUrl"], "VnPay:IpnUrl", errors);
        }

        ThrowIfInvalid("API", errors);
    }

    public static void ValidateWorker(IConfiguration configuration)
    {
        var errors = new List<string>();
        ValidateRuntimeDependencies(configuration, errors);
        ThrowIfInvalid("Worker", errors);
    }

    private static void ValidateRuntimeDependencies(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        Require(
            configuration.GetConnectionString("SyntwinDb"),
            "ConnectionStrings:SyntwinDb",
            errors);
        Require(
            configuration["Redis:ConnectionString"],
            "Redis:ConnectionString",
            errors);

        if (!configuration.GetValue<bool>("InfluxDb:Enabled"))
        {
            return;
        }

        Require(configuration["InfluxDb:Url"], "InfluxDb:Url", errors);
        Require(configuration["InfluxDb:Org"], "InfluxDb:Org", errors);
        Require(configuration["InfluxDb:Bucket"], "InfluxDb:Bucket", errors);
        Require(configuration["InfluxDb:Token"], "InfluxDb:Token", errors);
    }

    private static void Require(
        string? value,
        string key,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{key} is required.");
        }
    }

    private static void ThrowIfInvalid(
        string processName,
        IReadOnlyCollection<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{processName} configuration is invalid:{Environment.NewLine}" +
            string.Join(
                Environment.NewLine,
                errors.Select(error => $"- {error}")));
    }
}
