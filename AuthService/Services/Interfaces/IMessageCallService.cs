using AuthService.Dtos.Conversations;

namespace AuthService.Services.Interfaces;

public interface IMessageCallService
{
    Task<ConversationDto> CreateOrGetOneToOneAsync(CreateConversationRequest req, CancellationToken ct = default);
    Task<List<ThreadDto>> GetThreadsAsync(int accountId, CancellationToken ct = default);
    Task<List<MessageDto>> GetMessagesAsync(int conversationId, int meId, int limit = 50, int? beforeMessageId = null, CancellationToken ct = default);
    Task MarkReadAsync(int conversationId, int meId, CancellationToken ct = default);
    Task<MessageDto> SendTextMessageAsync(int conversationId, SendMessageRequest req, CancellationToken ct = default);
    Task<MessageDto> SendImageMessageAsync(int conversationId, SendImageMessageRequest req, CancellationToken ct = default);
    Task<MessageDto> SendAudioMessageAsync(int conversationId, SendAudioMessageRequest req, CancellationToken ct = default);
    Task<ConversationPeerResponseDto> GetPeerInfoAsync(int conversationId, int meId, CancellationToken ct = default);
}

