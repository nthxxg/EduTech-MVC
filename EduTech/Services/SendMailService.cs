using System;
using System.Threading.Tasks;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EduTech.Services {


    public class MailSettings {
        public string Mail { get; set; }
        public string DisplayName { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

    }

    // Email sending service
    public class SendMailService : IEmailSender {
        private readonly MailSettings mailSettings;

        private readonly ILogger<SendMailService> logger;

        // inject MailSettings and Logger

        public SendMailService(IOptions<MailSettings> _mailSettings, ILogger<SendMailService> _logger)
        {
            mailSettings = _mailSettings.Value;
            logger = _logger;
            logger.LogInformation("Create SendMailService");
        }

        public async Task SendEmailAsync (string email, string subject, string htmlMessage) {
            var message = new MimeMessage ();
            message.Sender = new MailboxAddress (mailSettings.DisplayName, mailSettings.Mail);
            message.From.Add (new MailboxAddress (mailSettings.DisplayName, mailSettings.Mail));
            message.To.Add (MailboxAddress.Parse (email));
            message.Subject = subject;

            var builder = new BodyBuilder ();
            builder.HtmlBody = htmlMessage;
            message.Body = builder.ToMessageBody ();

            // use MailKit to send email
            using var smtp = new MailKit.Net.Smtp.SmtpClient ();

            try {
                smtp.Connect (mailSettings.Host, mailSettings.Port, SecureSocketOptions.StartTls);
                smtp.Authenticate (mailSettings.Mail, mailSettings.Password);
                await smtp.SendAsync (message);
            } catch (Exception ex) {

                // If the mail sending fails, the email content will be saved to the mailssave folder
                System.IO.Directory.CreateDirectory ("mailssave");
                var emailsavefile = string.Format (@"mailssave/{0}.eml", Guid.NewGuid ());
                await message.WriteToAsync (emailsavefile);

                logger.LogInformation ("Error to send email, saved to - " + emailsavefile);
                logger.LogError (ex.Message);
            }

            smtp.Disconnect (true);

            logger.LogInformation ("send mail to: " + email);

        }
    }
}
