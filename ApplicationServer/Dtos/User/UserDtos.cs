namespace ApplicationServer.Dtos.User
{
    public class userDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
        public string PhotoBackground { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
        public byte? Gender { get; set; }
        public string? Bio { get; set; }
    }
}
