namespace AuthService.Dtos.Conversations
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
        public string? AvatarUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class ThreadDto
    {
        public int ConversationId { get; set; }
        public int? OtherAccountId { get; set; }
        public bool IsGroup { get; set; }
        public bool IsOwner { get; set; }
        public string Name { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public string Snippet { get; set; } = "";
        public DateTime? LastMessageAt { get; set; }
    }

    public class CreateGroupConversationRequest
    {
        public int OwnerId { get; set; }
        public string Title { get; set; } = "";
        public List<int> MemberIds { get; set; } = new();
    }

    public class JoinGroupRequest
    {
        public int AccountId { get; set; }
    }

    public class RemoveGroupMemberRequest
    {
        public int OwnerId { get; set; }
        public int MemberId { get; set; }
    }

    public class UpdateGroupRequest
    {
        public int OwnerId { get; set; }
        public string? Title { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class LeaveGroupRequest
    {
        public int AccountId { get; set; }
        public int? SuccessorId { get; set; }
    }

    public class LeaveGroupResultDto
    {
        public bool Dissolved { get; set; }
    }

    public class GroupMemberDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhotoPath { get; set; }
        public string Role { get; set; } = "member";
        public DateTime? JoinedAt { get; set; }
    }

    public class GroupInfoDto
    {
        public int ConversationId { get; set; }
        public string Title { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public bool IsOwner { get; set; }
        public List<GroupMemberDto> Members { get; set; } = new();
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
