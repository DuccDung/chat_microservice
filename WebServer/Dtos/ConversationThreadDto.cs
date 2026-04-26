using System.Text.Json.Serialization;

namespace WebServer.Dtos
{
    public class ConversationThreadDto
    {
        [JsonPropertyName("conversationId")]
        public int ConversationId { get; set; }
        [JsonPropertyName("name")]

        public string Name { get; set; } = "";
        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; } = "";
        [JsonPropertyName("snippet")]

        public string Snippet { get; set; } = "";
        [JsonPropertyName("lastMessageAt")]
        public DateTime? LastMessageAt { get; set; }
    }
    public class ConversationMessageDto
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public string Content { get; set; } = "";
        public string MessageType { get; set; } = "text";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsRemove { get; set; }
        public int? ParentMessageId { get; set; }
        public SenderDto Sender { get; set; } = new SenderDto();
    }

    public class SenderDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhotoPath { get; set; }
    }
    public class SendMessageRequest
    {
        public int ConversationId { get; set; }
        public string? Content { get; set; }
        public int? ParentMessageId { get; set; }
    }
    public class SendImageUploadRequest
    {
        public int ConversationId { get; set; }
        public IFormFile File { get; set; } = default!;
        public int? ParentMessageId { get; set; }
    }
    public class SendAudioUploadRequest
    {
        public int ConversationId { get; set; }
        public IFormFile File { get; set; } = default!;
        public int? ParentMessageId { get; set; }
    }
    public class PeerDto
    {
        public int AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? Email { get; set; }
        public string? PhotoPath { get; set; }
    }
    public class ConversationPeerResponseDto
    {
        public PeerDto Me { get; set; } = default!;
        public PeerDto Peer { get; set; } = default!;
    }
}
