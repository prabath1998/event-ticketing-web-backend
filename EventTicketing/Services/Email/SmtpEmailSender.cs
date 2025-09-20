using System.Net;
using System.Net.Mail;
using EventTicketing.Models.Options;
using Microsoft.Extensions.Options;

namespace EventTicketing.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        public SmtpEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(_opt.FromEmail, _opt.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            msg.To.Add(to);

            using var client = new SmtpClient(_opt.SmtpHost, _opt.SmtpPort)
            {
                EnableSsl = _opt.UseSsl
            };

            if (!string.IsNullOrWhiteSpace(_opt.User))
                client.Credentials = new NetworkCredential(_opt.User, _opt.Pass);
            
            await Task.Run(() => client.Send(msg), ct);
        }
    }
}