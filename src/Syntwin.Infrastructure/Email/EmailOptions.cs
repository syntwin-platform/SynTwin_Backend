namespace Syntwin.Infrastructure.Email;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string FromEmail { get; set; } = "no-reply@syntwin.local";
    public string FromName { get; set; } = "SynTwin";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}