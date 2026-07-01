namespace Syntwin.Infrastructure.Telemetry;

public sealed class InfluxDbOptions
{
    public bool Enabled { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Org { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public int WriteTimeoutSeconds { get; set; } = 5;

    public int QueryTimeoutSeconds { get; set; } = 10;
}