using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using WebServer.Interfaces;

namespace WebServer.Services
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _email;
        private readonly SmtpOptions _smtp;

        public SmtpEmailSender(IOptions<EmailOptions> email, IOptions<SmtpOptions> smtp)
        {
            _email = email.Value;
            _smtp = smtp.Value;
        }

        public async Task SendRegisterOtpAsync(string toEmail, string otp, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Email is required.", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(otp)) throw new ArgumentException("Otp is required.", nameof(otp));

            using var message = new MailMessage
            {
                From = new MailAddress(_email.FromAddress, _email.FromName),
                Subject = $"Ma xac thuc dang ky {_email.BrandName}",
                Body = BuildOtpEmailBody(otp),
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl = _smtp.UseStartTls,
                Credentials = new NetworkCredential(_smtp.User, _smtp.Pass),
                Timeout = Math.Max(1, _smtp.TimeoutSeconds) * 1000
            };

            await client.SendMailAsync(message, ct);
        }

        private string BuildOtpEmailBody(string otp)
        {
            return $$"""
<!doctype html>
<html>
  <body style="margin:0;background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;color:#1c1e21;">
    <div style="max-width:520px;margin:0 auto;padding:28px 16px;">
      <div style="font-size:28px;font-weight:800;color:#2b6fe8;margin-bottom:14px;">{{WebUtility.HtmlEncode(_email.BrandName)}}</div>
      <div style="background:#fff;border-radius:10px;padding:24px;box-shadow:0 2px 12px rgba(0,0,0,.08);">
        <h1 style="font-size:20px;margin:0 0 10px;">Xac thuc email dang ky</h1>
        <p style="font-size:15px;line-height:1.5;margin:0 0 18px;color:#606770;">Nhap ma ben duoi tren man hinh dang ky de hoan tat tao tai khoan.</p>
        <div style="letter-spacing:8px;font-size:30px;font-weight:800;text-align:center;background:#f5f7fb;border:1px solid #dddfe2;border-radius:8px;padding:14px 10px;color:#1c1e21;">{{WebUtility.HtmlEncode(otp)}}</div>
        <p style="font-size:13px;line-height:1.5;margin:18px 0 0;color:#606770;">Ma co hieu luc trong 5 phut. Neu ban khong yeu cau dang ky, hay bo qua email nay.</p>
      </div>
    </div>
  </body>
</html>
""";
        }
    }
}
