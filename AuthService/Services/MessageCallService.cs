using AuthService.Dtos.Conversations;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public sealed class MessageCallService : IMessageCallService
{
    private const string DefaultAvatarUrl = "/assets/images/avatar-default.png";
    private const string DefaultGroupAvatarUrl = "/assets/images/group-default.png";

    private readonly SocialNetworkContext _context;

    public MessageCallService(SocialNetworkContext context)
    {
        _context = context;
    }

    public async Task<ConversationDto> CreateOrGetOneToOneAsync(CreateConversationRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (req.AccountId <= 0 || req.FriendId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Invalid ids.");
        if (req.AccountId == req.FriendId)
            throw new ServiceException(StatusCodes.Status400BadRequest, "AccountId and FriendId must be different.");

        var usersExist = await _context.Accounts
            .Where(a => a.AccountId == req.AccountId || a.AccountId == req.FriendId)
            .Select(a => a.AccountId)
            .ToListAsync(ct);

        if (usersExist.Count != 2)
            throw new ServiceException(StatusCodes.Status404NotFound, "One or both users not found.");

        var existingConversationId = await _context.ConversationMembers
            .Where(cm => cm.AccountId == req.AccountId || cm.AccountId == req.FriendId)
            .GroupBy(cm => cm.ConversationId)
            .Where(g => g.Select(x => x.AccountId).Distinct().Count() == 2)
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        if (existingConversationId != 0)
        {
            var existing = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConversationId == existingConversationId && c.IsGroup == false, ct);

            if (existing != null)
                return MapConversation(existing);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            IsGroup = false,
            Title = null,
            CreatedAt = now
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(ct);

        _context.ConversationMembers.AddRange(
            new ConversationMember
            {
                ConversationId = conversation.ConversationId,
                AccountId = req.AccountId,
                JoinedAt = now,
                CreatedAt = now,
                Title = null
            },
            new ConversationMember
            {
                ConversationId = conversation.ConversationId,
                AccountId = req.FriendId,
                JoinedAt = now,
                CreatedAt = now,
                Title = null
            }
        );

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return MapConversation(conversation);
    }

    public async Task<List<ThreadDto>> GetThreadsAsync(int accountId, CancellationToken ct = default)
    {
        if (accountId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "accountId is required.");

        var exists = await _context.Accounts.AnyAsync(a => a.AccountId == accountId, ct);
        if (!exists)
            throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

        var threads = await _context.ConversationMembers
            .Where(cm => cm.AccountId == accountId)
            .Select(cm => cm.Conversation)
            .Where(c => c != null)
            .Select(c => new
            {
                Conversation = c!,
                LastMessage = c!.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new { m.Content, m.MessageType, m.CreatedAt, m.SenderId })
                    .FirstOrDefault(),
                OtherMember = c!.ConversationMembers
                    .Where(x => x.AccountId != accountId)
                    .Select(x => x.Account)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.LastMessage == null ? x.Conversation.CreatedAt : x.LastMessage.CreatedAt)
            .ToListAsync(ct);

        return threads.Select(x =>
        {
            var conversation = x.Conversation;
            var name = conversation.IsGroup
                ? string.IsNullOrWhiteSpace(conversation.Title) ? "Nhóm chat" : conversation.Title!
                : x.OtherMember?.AccountName ?? "Người dùng";

            var avatar = conversation.IsGroup
                ? DefaultGroupAvatarUrl
                : string.IsNullOrWhiteSpace(x.OtherMember?.PhotoPath) ? DefaultAvatarUrl : x.OtherMember!.PhotoPath!;

            var snippet = "Chưa có tin nhắn";
            DateTime? lastAt = null;

            if (x.LastMessage != null)
            {
                lastAt = x.LastMessage.CreatedAt;
                snippet = !string.IsNullOrWhiteSpace(x.LastMessage.Content)
                    ? x.LastMessage.Content!
                    : "Đã gửi một tệp đính kèm.";
            }

            return new ThreadDto
            {
                ConversationId = conversation.ConversationId,
                Name = name,
                AvatarUrl = avatar,
                Snippet = snippet,
                LastMessageAt = lastAt
            };
        }).ToList();
    }

    public async Task<List<MessageDto>> GetMessagesAsync(
        int conversationId,
        int meId,
        int limit = 50,
        int? beforeMessageId = null,
        CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (meId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "me is required.");

        limit = Math.Clamp(limit, 1, 200);
        await EnsureMemberAsync(conversationId, meId, ct);

        var query = _context.Messages
            .Where(m => m.ConversationId == conversationId && (m.IsRemove == null || m.IsRemove == false))
            .Include(m => m.Sender)
            .AsNoTracking()
            .OrderByDescending(m => m.MessageId);

        if (beforeMessageId.HasValue)
        {
            query = query
                .Where(m => m.MessageId < beforeMessageId.Value)
                .OrderByDescending(m => m.MessageId);
        }

        var messages = await query.Take(limit).ToListAsync(ct);
        messages.Reverse();

        return messages.Select(MapMessage).ToList();
    }

    public async Task MarkReadAsync(int conversationId, int meId, CancellationToken ct = default)
    {
        if (conversationId <= 0 || meId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId and me are required.");

        await EnsureMemberAsync(conversationId, meId, ct);
    }

    public Task<MessageDto> SendTextMessageAsync(int conversationId, SendMessageRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (string.IsNullOrWhiteSpace(req.Content))
            throw new ServiceException(StatusCodes.Status400BadRequest, "Content is required.");

        return CreateMessageAsync(conversationId, req.SenderId, req.Content.Trim(), "text", req.ParentMessageId, ct);
    }

    public Task<MessageDto> SendImageMessageAsync(int conversationId, SendImageMessageRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (string.IsNullOrWhiteSpace(req.ImageUrl))
            throw new ServiceException(StatusCodes.Status400BadRequest, "ImageUrl is required.");

        return CreateMessageAsync(conversationId, req.SenderId, req.ImageUrl.Trim(), "image", req.ParentMessageId, ct);
    }

    public Task<MessageDto> SendAudioMessageAsync(int conversationId, SendAudioMessageRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (string.IsNullOrWhiteSpace(req.AudioUrl))
            throw new ServiceException(StatusCodes.Status400BadRequest, "AudioUrl is required.");

        return CreateMessageAsync(conversationId, req.SenderId, req.AudioUrl.Trim(), "audio", req.ParentMessageId, ct);
    }

    public async Task<ConversationPeerResponseDto> GetPeerInfoAsync(int conversationId, int meId, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (meId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "meId is required.");

        var conv = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, ct);

        if (conv == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Conversation not found.");
        if (conv.IsGroup)
            throw new ServiceException(StatusCodes.Status400BadRequest, "This endpoint only supports 1-1 conversations.");

        await EnsureMemberAsync(conversationId, meId, ct);

        var users = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == conversationId)
            .Select(cm => new PeerDto
            {
                AccountId = cm.Account.AccountId,
                AccountName = cm.Account.AccountName,
                Email = cm.Account.Email,
                PhotoPath = cm.Account.PhotoPath
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var me = users.FirstOrDefault(x => x.AccountId == meId);
        var peer = users.FirstOrDefault(x => x.AccountId != meId);

        if (me == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Me not found.");
        if (peer == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Peer not found.");

        return new ConversationPeerResponseDto
        {
            Me = me,
            Peer = peer
        };
    }

    private async Task<MessageDto> CreateMessageAsync(
        int conversationId,
        int senderId,
        string content,
        string messageType,
        int? parentMessageId,
        CancellationToken ct)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "ConversationId is required.");
        if (senderId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "SenderId is required.");

        var convExists = await _context.Conversations.AnyAsync(c => c.ConversationId == conversationId, ct);
        if (!convExists)
            throw new ServiceException(StatusCodes.Status404NotFound, "Conversation not found.");

        var sender = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == senderId, ct);

        if (sender == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Sender not found.");

        await EnsureMemberAsync(conversationId, senderId, ct);

        if (parentMessageId.HasValue)
        {
            var parentOk = await _context.Messages.AnyAsync(m =>
                m.MessageId == parentMessageId.Value &&
                m.ConversationId == conversationId, ct);

            if (!parentOk)
                throw new ServiceException(StatusCodes.Status400BadRequest, "ParentMessageId is invalid.");
        }

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = messageType,
            CreatedAt = DateTime.UtcNow,
            ParentMessageId = parentMessageId,
            IsRead = false,
            IsRemove = false
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync(ct);

        message.Sender = sender;
        return MapMessage(message);
    }

    private async Task EnsureMemberAsync(int conversationId, int accountId, CancellationToken ct)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(cm => cm.ConversationId == conversationId && cm.AccountId == accountId, ct);

        if (!isMember)
            throw new ServiceException(StatusCodes.Status403Forbidden, "User is not a member of this conversation.");
    }

    private static ConversationDto MapConversation(Conversation conversation)
    {
        return new ConversationDto
        {
            ConversationId = conversation.ConversationId,
            IsGroup = conversation.IsGroup,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt
        };
    }

    private static MessageDto MapMessage(Message message)
    {
        return new MessageDto
        {
            MessageId = message.MessageId,
            ConversationId = message.ConversationId,
            Content = message.Content,
            MessageType = message.MessageType,
            CreatedAt = message.CreatedAt,
            IsRead = message.IsRead,
            IsRemove = message.IsRemove,
            ParentMessageId = message.ParentMessageId,
            Sender = new SenderDto
            {
                AccountId = message.Sender.AccountId,
                AccountName = message.Sender.AccountName,
                Email = message.Sender.Email,
                PhotoPath = message.Sender.PhotoPath
            }
        };
    }
}

