namespace WebServer.Interfaces
{
    public interface IEmailSender
    {
        Task SendRegisterOtpAsync(string toEmail, string otp, CancellationToken ct = default);
    }
}
