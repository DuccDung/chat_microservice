using WebServer.Dtos;

namespace WebServer.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationThreadDto>> GetThreadsAsync(int accountId);
        Task<List<ConversationMessageDto>> GetMessagesAsync(int conversationId, int meAccountId, int limit = 50);

        Task<ConversationMessageDto> SendTextMessageAsync(int conversationId, int senderId, string content, int? parentMessageId = null);
        Task<ConversationMessageDto> SendImageMessageAsync(int conversationId, int senderId, IFormFile file, int? parentMessageId);
        Task<ConversationMessageDto> SendAudioMessageAsync(int conversationId, int senderId, IFormFile file, int? parentMessageId);
        Task<ConversationPeerResponseDto?> GetPeerAsync(int conversationId, int meAccountId);
    }
}
