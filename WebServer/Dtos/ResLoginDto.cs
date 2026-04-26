using System.Text.Json;
using System.Text.Json.Serialization;
namespace WebServer.Dtos
{
    public class ReqLoginDto
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
        [JsonPropertyName("rememberMe")]
        public bool RememberMe { get; set; } = false;
    }


    public class ResLoginDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = null!;

        [JsonPropertyName("password")]
        public string Password { get; set; } = null!;

        [JsonPropertyName("email")]
        public string Email { get; set; } = null!;

        [JsonPropertyName("photoPath")]
        public string? PhotoPath { get; set; }
    }

    public class ReqRegisterDto
    {
        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }
    public class ResRegisterDto
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}