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
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 465;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
    }
}
