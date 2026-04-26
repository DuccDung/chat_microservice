namespace ApplicationServer.Dtos.Auth
{
    public class ResLogin
    {
        public int AccountId { get; set; }

        public string AccountName { get; set; } = null!;

        public string Password { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string? PhotoPath { get; set; }
    }
}
