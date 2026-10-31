using System.Net;
using System.Net.Mail;
using Documents.Core.Interfaces;
using Documents.Core.Options;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Services
{
    /// <summary>
    /// SMTP sender. Smtp:PickupDirectory set ho (development) to mail bheji nahi jati —
    /// .eml file us folder mein likhi jati hai. Mail ka content kabhi log nahi hota.
    /// </summary>
    public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
    {
        public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            var smtp = options.Value;
            using var message = new MailMessage(smtp.From, to, subject, body);
            using var client = new SmtpClient();

            if (!string.IsNullOrWhiteSpace(smtp.PickupDirectory))
            {
                Directory.CreateDirectory(smtp.PickupDirectory);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = smtp.PickupDirectory;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(smtp.Host))
                    throw new InvalidOperationException("Smtp:Host (or Smtp:PickupDirectory) must be configured to send email.");
                client.Host = smtp.Host;
                client.Port = smtp.Port;
                client.EnableSsl = smtp.EnableSsl;
                if (!string.IsNullOrEmpty(smtp.User))
                    client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
            }

            await client.SendMailAsync(message, ct);
        }
    }
}
