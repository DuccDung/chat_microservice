using System.Text.Json.Serialization;

namespace WebServer.Dtos
{
    public sealed class ProfileAccountDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("photoPath")]
        public string? PhotoPath { get; set; }

        [JsonPropertyName("photoBackground")]
        public string? PhotoBackground { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public DateOnly? DateOfBirth { get; set; }

        [JsonPropertyName("gender")]
        public byte? Gender { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }
    }

    public sealed class UpdateProfileRequestDto
    {
        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = "";

        [JsonPropertyName("dateOfBirth")]
        public DateOnly? DateOfBirth { get; set; }

        [JsonPropertyName("gender")]
        public byte? Gender { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }
    }

    public sealed class UpdateProfilePhotosRequestDto
    {
        [JsonPropertyName("photoPath")]
        public string? PhotoPath { get; set; }

        [JsonPropertyName("photoBackground")]
        public string? PhotoBackground { get; set; }
    }

    public sealed class CreateProfilePostRequestDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("mediaUrl")]
        public string? MediaUrl { get; set; }

        [JsonPropertyName("mediaType")]
        public string? MediaType { get; set; }
    }

    public sealed class UpdateProfilePostRequestDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("mediaUrl")]
        public string? MediaUrl { get; set; }

        [JsonPropertyName("mediaType")]
        public string? MediaType { get; set; }
    }

    public sealed class ProfilePostDto
    {
        [JsonPropertyName("postId")]
        public int PostId { get; set; }

        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("authorName")]
        public string AuthorName { get; set; } = "";

        [JsonPropertyName("authorPhotoPath")]
        public string? AuthorPhotoPath { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("postType")]
        public string? PostType { get; set; }

        [JsonPropertyName("createAt")]
        public DateTime? CreateAt { get; set; }

        [JsonPropertyName("updateAt")]
        public DateTime? UpdateAt { get; set; }

        [JsonPropertyName("media")]
        public List<ProfilePostMediaDto> Media { get; set; } = new();
    }

    public sealed class ProfilePostMediaDto
    {
        [JsonPropertyName("mediaId")]
        public int MediaId { get; set; }

        [JsonPropertyName("mediaUrl")]
        public string MediaUrl { get; set; } = "";

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = "";
    }
}
