using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain
{
    public class AppSetting
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public Logging Logging { get; set; } = new();
        public string AllowedHosts { get; set; } = "*";
        public SecretToken SecretToken { get; set; } = new();
        public EmailSettings Email { get; set; } = new();
        public EmailSettings EmailSettings
        {
            get => Email;
            set => Email = value ?? new EmailSettings();
        }
        public TeamsGraph TeamsGraph { get; set; } = new TeamsGraph();
        public GoogleAI GoogleAI { get; set; } = new GoogleAI();
        public Tavily Tavily { get; set; } = new Tavily();
    }
    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; } = string.Empty;
        public string LocalDockerConnection { get; set; } = string.Empty;
    }

    public class Logging
    {
        public LogLevel LogLevel { get; set; } = new();
    }

    public class LogLevel
    {
        public string Default { get; set; } = "Information";
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }

    public class SecretToken
    {
        public string Value { get; set; } = string.Empty;
        public string Issuer { get; set; } = "BuildSenseAPI";
        public string Audience { get; set; } = "BuildSenseClient";
        public int DurationInMinutes { get; set; } = 1440;
    }

    public class EmailSettings
    {
        public string GmailClientId { get; set; } = string.Empty;
        public string GmailClientSecret { get; set; } = string.Empty;
        public string GmailRefreshToken { get; set; } = string.Empty;
        public string GmailSenderEmail { get; set; } = string.Empty;
        public string GmailSenderName { get; set; } = "BuildSense";

        // SMTP Properties (kept for backward compatibility)
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 465;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
    }

    public class TeamsGraph
    {
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? OrganizerUserId { get; set; }
    }

    public class GoogleAI
    {
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "gemini-3.5-flash";
    }

    public class Tavily
    {
        public string? ApiKey { get; set; }
        public int DefaultMaxResults { get; set; } = 5;
        public string SearchDepth { get; set; } = "basic";
    }
}
