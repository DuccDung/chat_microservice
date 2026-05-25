using System.Text.Json.Serialization;

namespace WebServer.Dtos
{
    public class ConversationThreadDto
    {
        [JsonPropertyName("conversationId")]
        public int ConversationId { get; set; }

        [JsonPropertyName("otherAccountId")]
        public int? OtherAccountId { get; set; }

        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; set; }

        [JsonPropertyName("isOwner")]
        public bool IsOwner { get; set; }

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

    public class ConversationDto
    {
        [JsonPropertyName("conversationId")]
        public int ConversationId { get; set; }

        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("avatarUrl")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateGroupConversationRequestDto
    {
        [JsonPropertyName("ownerId")]
        public int OwnerId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("memberIds")]
        public List<int> MemberIds { get; set; } = new();
    }

    public class UpdateGroupConversationRequestDto
    {
        [JsonPropertyName("ownerId")]
        public int OwnerId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("avatarUrl")]
        public string? AvatarUrl { get; set; }
    }

    public class GroupMemberDto
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("photoPath")]
        public string? PhotoPath { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = "member";

        [JsonPropertyName("joinedAt")]
        public DateTime? JoinedAt { get; set; }
    }

    public class GroupInfoDto
    {
        [JsonPropertyName("conversationId")]
        public int ConversationId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; } = "";

        [JsonPropertyName("isOwner")]
        public bool IsOwner { get; set; }

        [JsonPropertyName("members")]
        public List<GroupMemberDto> Members { get; set; } = new();
    }
}
