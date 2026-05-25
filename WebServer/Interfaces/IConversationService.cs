using WebServer.Dtos;

namespace WebServer.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationThreadDto>> GetThreadsAsync(int accountId);
        Task<ConversationDto> CreateOrGetOneToOneAsync(int accountId, int friendId);
        Task<ConversationDto> CreateGroupAsync(int ownerId, string title, IEnumerable<int> memberIds);
        Task<GroupInfoDto> GetGroupInfoAsync(int conversationId, int meAccountId);
        Task<ConversationDto> JoinGroupAsync(int conversationId, int accountId);
        Task<GroupInfoDto> UpdateGroupAsync(int conversationId, int ownerId, string? title, string? avatarUrl);
        Task<LeaveGroupResultDto> LeaveGroupAsync(int conversationId, int accountId, int? successorId);
        Task RemoveGroupMemberAsync(int conversationId, int ownerId, int memberId);
        Task<List<ConversationMessageDto>> GetMessagesAsync(int conversationId, int meAccountId, int limit = 50);

        Task<ConversationMessageDto> SendTextMessageAsync(int conversationId, int senderId, string content, int? parentMessageId = null);
        Task<ConversationMessageDto> SendImageMessageAsync(int conversationId, int senderId, IFormFile file, int? parentMessageId);
        Task<ConversationMessageDto> SendAudioMessageAsync(int conversationId, int senderId, IFormFile file, int? parentMessageId);
        Task<ConversationPeerResponseDto?> GetPeerAsync(int conversationId, int meAccountId);
    }
}
