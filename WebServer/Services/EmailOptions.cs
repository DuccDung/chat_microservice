namespace WebServer.Services
{
    public sealed class EmailOptions
    {
        public string BrandName { get; set; } = "Mess Social";
        public string FromName { get; set; } = "Mess Social";
        public string FromAddress { get; set; } = "";
    }

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 30;
    }
}
