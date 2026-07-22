using cpms_Domain;
using MailKit.Security;

namespace cpms_Application.Services;

public static class SmtpConnectionOptions
{
    public static SecureSocketOptions Resolve(EmailSettings settings)
    {
        if (settings.SmtpPort == 587)
            return SecureSocketOptions.StartTls;

        return settings.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }

    public static string NormalizeUsername(string value) => value.Trim();

    public static string NormalizePassword(string value) => value.Trim().Replace(" ", string.Empty);

    public static string ResolveFromAddress(EmailSettings settings, string username) =>
        string.IsNullOrWhiteSpace(settings.FromAddress) ? username : settings.FromAddress.Trim();
}
