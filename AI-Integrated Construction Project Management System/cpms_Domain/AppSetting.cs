using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain
{
    public class AppSetting
    {
        public ConnectionStrings ConnectionStrings { get; set; }
        public Logging Logging { get; set; }
        public string AllowedHosts { get; set; }
        public SecretToken SecretToken { get; set; }
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
