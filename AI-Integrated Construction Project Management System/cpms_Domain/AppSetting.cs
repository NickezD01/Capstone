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
        public EmailSettings EmailSettings { get; set; } = new();
        public TeamsGraph TeamsGraph { get; set; } = new TeamsGraph();
        public GoogleAI GoogleAI { get; set; } = new GoogleAI();
    }
    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; }
        public string LocalDockerConnection { get; set; }
    }

    public class Logging
    {
        public LogLevel LogLevel { get; set; }
    }

    public class LogLevel
    {
        public string Default { get; set; }
        public string MicrosoftAspNetCore { get; set; }
    }

    public class SecretToken
    {
        public string Value { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public int DurationInMinutes { get; set; }
    }

    public class EmailSettings
    {
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromAddress { get; set; }
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
}
