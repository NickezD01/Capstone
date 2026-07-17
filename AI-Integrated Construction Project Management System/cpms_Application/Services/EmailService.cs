using cpms_Application.Interfaces;
using cpms_Application.Response;
using cpms_Domain;
using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(AppSetting appSettings)
        {
            _settings = appSettings.Email;
        }

        public async Task<ApiResponse> SendNotiMail(string recievedUser, string emailContent)
        {

            try
            {

                var message = new MimeMessage();
                if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
                    return new ApiResponse().SetBadRequest(message: "SMTP credentials are not configured.");
                message.From.Add(new MailboxAddress("BuildSense",
                    string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.Username : _settings.FromAddress));
                message.To.Add(new MailboxAddress("", recievedUser));
                message.Subject = $"Notification";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = emailContent;
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.UseSsl);
                    await client.AuthenticateAsync(_settings.Username, _settings.Password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                return new ApiResponse().SetOk("Mail Sent!");
            }
            catch (Exception)
            {
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.ServiceUnavailable, false, "Unable to send email.");
            }
        }

        public async Task<ApiResponse> SendValidationEmail(string recievedUser, string emailContent)
        {
            try
            {
                // Replace placeholders with actual values


                var message = new MimeMessage();
                if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
                    return new ApiResponse().SetBadRequest(message: "SMTP credentials are not configured.");
                message.From.Add(new MailboxAddress("BuildSense",
                    string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.Username : _settings.FromAddress));
                message.To.Add(new MailboxAddress("", recievedUser));
                message.Subject = $"Verification Email";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = emailContent; // Use the modified emailContent with the placeholders replaced
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.UseSsl);
                    await client.AuthenticateAsync(_settings.Username, _settings.Password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                return new ApiResponse().SetOk("Mail Sent!");
            }
            catch (Exception)
            {
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.ServiceUnavailable, false, "Unable to send email.");
            }
        }
    }
}
