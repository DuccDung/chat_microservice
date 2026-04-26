namespace ApplicationServer.Dtos.Conversations
{
    public class CreateConversationRequest
    {
        public int AccountId { get; set; }
        public int FriendId { get; set; }
    }

    public class ConversationDto
    {
        public int ConversationId { get; set; }
        public bool IsGroup { get; set; }
        public string? Title { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class ThreadDto
    {
        public int ConversationId { get; set; }
        public string Name { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public string Snippet { get; set; } = "";
        public DateTime? LastMessageAt { get; set; }
    }
    public class MessageDto
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }

        public string? Content { get; set; }
        public string? MessageType { get; set; }
        public DateTime? CreatedAt { get; set; }

        public bool? IsRead { get; set; }
        public bool? IsRemove { get; set; }
        public int? ParentMessageId { get; set; }

        public SenderDto Sender { get; set; } = new();
    }

    public class SenderDto
    {
        public int AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? Email { get; set; }
        public string? PhotoPath { get; set; }  // avatar
    }
    public class SendMessageRequest
    {
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public int? ParentMessageId { get; set; } // optional (reply)
    }
    public class SendImageMessageRequest
    {
        public int SenderId { get; set; }
        public string? ImageUrl { get; set; }
        public int? ParentMessageId { get; set; }
    }
    public class SendAudioMessageRequest
    {
        public int SenderId { get; set; }
        public string AudioUrl { get; set; } = "";
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
