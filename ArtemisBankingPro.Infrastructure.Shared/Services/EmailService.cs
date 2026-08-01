using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Settings;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ArtemisBankingPro.Infrastructure.Shared.Services
{
    public class EmailService : IEmailService
    {
        public MailSettings _mailSettings { get; }

        public EmailService(IOptions<MailSettings> mailsettings)
        {
            _mailSettings = mailsettings.Value;
        }

        public async Task<Result> SendAsync(EmailRequestDto emailRequestDto)
        {
            try
            {
                emailRequestDto.ToRange.Add(emailRequestDto.To ?? "");

                var email = new MimeMessage()
                {
                    Sender = MailboxAddress.Parse(_mailSettings.EmailFrom),
                    Subject = emailRequestDto.Subject
                };

                foreach (var toItem in emailRequestDto.ToRange ?? [])
                {
                    email.To.Add(MailboxAddress.Parse(toItem));
                }

                var builder = new BodyBuilder()
                {
                    HtmlBody = emailRequestDto.BodyHtml
                };

                email.Body = builder.ToMessageBody();

                using MailKit.Net.Smtp.SmtpClient smtpClient = new();
                await smtpClient.ConnectAsync(_mailSettings.SmtpHost, _mailSettings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                await smtpClient.AuthenticateAsync(_mailSettings.SmtpUser, _mailSettings.SmtpPass);
                await smtpClient.SendAsync(email);
                await smtpClient.DisconnectAsync(true);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(error: ex.Message);
            }
        }
    }
}
