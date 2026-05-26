using AuthService.Dtos.Conversations;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;

namespace AuthService.Services;

public sealed class MessageCallService : IMessageCallService
{
    private const string DefaultAvatarUrl = "/assets/images/avatar-default.png";
    private const string DefaultGroupAvatarUrl = "/assets/icons/group-default.svg";
    private const string GroupOwnerRole = "owner";
    private const string GroupMemberRole = "member";
    private static readonly TimeSpan ThreadsCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MessagesCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GroupInfoCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PeerInfoCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionCacheTtl = TimeSpan.FromDays(7);

    private readonly SocialNetworkContext _context;
    private readonly ICacheService _cache;

    public MessageCallService(SocialNetworkContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
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
            OwnerOnlyMessages = false,
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

        await InvalidateThreadCachesAsync(new[] { req.AccountId, req.FriendId }, ct);
        return MapConversation(conversation);
    }

    public async Task<ConversationDto> CreateGroupAsync(CreateGroupConversationRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (req.OwnerId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "OwnerId is required.");

        var requestedTitle = req.Title?.Trim();

        var memberIds = (req.MemberIds ?? new List<int>())
            .Where(id => id > 0 && id != req.OwnerId)
            .Distinct()
            .ToList();

        if (memberIds.Count == 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "At least one group member is required.");

        var allIds = memberIds.Append(req.OwnerId).Distinct().ToList();
        var existingUsers = await _context.Accounts
            .Where(a => allIds.Contains(a.AccountId))
            .Select(a => new { a.AccountId, a.AccountName })
            .ToListAsync(ct);

        if (existingUsers.Count != allIds.Count)
            throw new ServiceException(StatusCodes.Status404NotFound, "One or more users not found.");

        var title = requestedTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            var memberNames = existingUsers
                .Where(a => memberIds.Contains(a.AccountId))
                .OrderBy(a => memberIds.IndexOf(a.AccountId))
                .Select(a => string.IsNullOrWhiteSpace(a.AccountName) ? "Người dùng" : a.AccountName)
                .Take(3)
                .ToList();

            title = memberNames.Count == 0 ? "Nhóm chat" : string.Join(", ", memberNames);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            IsGroup = true,
            Title = title,
            OwnerOnlyMessages = false,
            CreatedAt = now
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync(ct);

        var members = new List<ConversationMember>
        {
            new()
            {
                ConversationId = conversation.ConversationId,
                AccountId = req.OwnerId,
                JoinedAt = now,
                CreatedAt = now,
                Title = GroupOwnerRole
            }
        };

        members.AddRange(memberIds.Select(memberId => new ConversationMember
        {
            ConversationId = conversation.ConversationId,
            AccountId = memberId,
            JoinedAt = now,
            CreatedAt = now,
            Title = GroupMemberRole
        }));

        _context.ConversationMembers.AddRange(members);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await InvalidateThreadCachesAsync(allIds, ct);
        return MapConversation(conversation);
    }

    public async Task<List<ThreadDto>> GetThreadsAsync(int accountId, CancellationToken ct = default)
    {
        if (accountId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "accountId is required.");

        var exists = await _context.Accounts.AnyAsync(a => a.AccountId == accountId, ct);
        if (!exists)
            throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

        return await _cache.GetOrCreateAsync(
            CacheKeys.ConversationThreads(accountId),
            ThreadsCacheTtl,
            async token =>
            {
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
                    .FirstOrDefault(),
                MyRole = c!.ConversationMembers
                    .Where(x => x.AccountId == accountId)
                    .Select(x => x.Title)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.LastMessage == null ? x.Conversation.CreatedAt : x.LastMessage.CreatedAt)
            .ToListAsync(token);

                return threads.Select(x =>
        {
            var conversation = x.Conversation;
            var name = conversation.IsGroup
                ? string.IsNullOrWhiteSpace(conversation.Title) ? "Nhóm chat" : conversation.Title!
                : x.OtherMember?.AccountName ?? "Người dùng";

            var avatar = conversation.IsGroup
                ? string.IsNullOrWhiteSpace(conversation.AvatarUrl) ? DefaultGroupAvatarUrl : conversation.AvatarUrl!
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
                OtherAccountId = conversation.IsGroup ? null : x.OtherMember?.AccountId,
                IsGroup = conversation.IsGroup,
                IsOwner = conversation.IsGroup && x.MyRole == GroupOwnerRole,
                OwnerOnlyMessages = conversation.IsGroup && conversation.OwnerOnlyMessages,
                Name = name,
                AvatarUrl = avatar,
                Snippet = snippet,
                LastMessageAt = lastAt
            };
        }).ToList();
            },
            ct);
    }

    public async Task<GroupInfoDto> GetGroupInfoAsync(int conversationId, int meId, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (meId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "meId is required.");

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && c.IsGroup, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Group not found.");

        await EnsureMemberAsync(conversationId, meId, ct);

        var version = await GetCacheStampValueAsync(CacheKeys.GroupInfoVersion(conversationId), ct);
        return await _cache.GetOrCreateAsync(
            CacheKeys.GroupInfo(conversationId, meId, version),
            GroupInfoCacheTtl,
            async token =>
            {
                var members = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == conversationId)
            .Include(cm => cm.Account)
            .AsNoTracking()
            .OrderBy(cm => cm.Title == GroupOwnerRole ? 0 : 1)
            .ThenBy(cm => cm.Account.AccountName)
            .Select(cm => new GroupMemberDto
            {
                AccountId = cm.AccountId,
                AccountName = cm.Account.AccountName,
                Email = cm.Account.Email,
                PhotoPath = cm.Account.PhotoPath,
                Role = cm.Title == GroupOwnerRole ? GroupOwnerRole : GroupMemberRole,
                JoinedAt = cm.JoinedAt
            })
            .ToListAsync(token);

                return new GroupInfoDto
                {
                    ConversationId = conversation.ConversationId,
                    Title = string.IsNullOrWhiteSpace(conversation.Title) ? "Nhóm chat" : conversation.Title!,
                    AvatarUrl = string.IsNullOrWhiteSpace(conversation.AvatarUrl) ? DefaultGroupAvatarUrl : conversation.AvatarUrl!,
                    IsOwner = members.Any(x => x.AccountId == meId && x.Role == GroupOwnerRole),
                    OwnerOnlyMessages = conversation.OwnerOnlyMessages,
                    Members = members
                };
            },
            ct);
    }

    public async Task<ConversationDto> JoinGroupAsync(int conversationId, int accountId, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (accountId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "accountId is required.");

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && c.IsGroup, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Group not found.");

        var userExists = await _context.Accounts.AnyAsync(a => a.AccountId == accountId, ct);
        if (!userExists)
            throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

        var alreadyMember = await _context.ConversationMembers
            .AnyAsync(cm => cm.ConversationId == conversationId && cm.AccountId == accountId, ct);

        if (!alreadyMember)
        {
            var now = DateTime.UtcNow;
            _context.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = conversationId,
                AccountId = accountId,
                JoinedAt = now,
                CreatedAt = now,
                Title = GroupMemberRole
            });

            await _context.SaveChangesAsync(ct);
            await InvalidateGroupCachesAsync(conversationId, ct);
            await InvalidateThreadCachesAsync(new[] { accountId }, ct);
        }

        return MapConversation(conversation);
    }

    public async Task<GroupInfoDto> UpdateGroupAsync(int conversationId, UpdateGroupRequest req, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (req.OwnerId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "OwnerId is required.");

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && c.IsGroup, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Group not found.");

        await EnsureGroupOwnerAsync(conversationId, req.OwnerId, ct);
        var affectedMemberIds = await GetConversationMemberIdsAsync(conversationId, ct);

        if (req.Title != null)
        {
            var title = req.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
                throw new ServiceException(StatusCodes.Status400BadRequest, "Group title is required.");

            conversation.Title = title.Length > 255 ? title[..255] : title;
        }

        if (req.AvatarUrl != null)
        {
            var avatarUrl = req.AvatarUrl.Trim();
            conversation.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl)
                ? null
                : avatarUrl.Length > 255 ? avatarUrl[..255] : avatarUrl;
        }

        if (req.OwnerOnlyMessages.HasValue)
        {
            conversation.OwnerOnlyMessages = req.OwnerOnlyMessages.Value;
        }

        await _context.SaveChangesAsync(ct);
        await InvalidateGroupCachesAsync(conversationId, ct);
        await InvalidateThreadCachesAsync(affectedMemberIds, ct);
        return await GetGroupInfoAsync(conversationId, req.OwnerId, ct);
    }

    public async Task<LeaveGroupResultDto> LeaveGroupAsync(int conversationId, LeaveGroupRequest req, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (req.AccountId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "AccountId is required.");

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && c.IsGroup, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Group not found.");

        var members = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == conversationId)
            .ToListAsync(ct);
        var affectedMemberIds = members.Select(cm => cm.AccountId).ToList();

        var leavingMember = members.FirstOrDefault(cm => cm.AccountId == req.AccountId);
        if (leavingMember == null)
            throw new ServiceException(StatusCodes.Status403Forbidden, "User is not a member of this group.");

        var remainingMembers = members
            .Where(cm => cm.AccountId != req.AccountId)
            .ToList();

        if (remainingMembers.Count < 2)
        {
            await DissolveGroupAsync(conversation, ct);
            await tx.CommitAsync(ct);
            await InvalidateGroupCachesAsync(conversationId, ct);
            await InvalidateConversationMessageCachesAsync(conversationId, ct);
            await InvalidateThreadCachesAsync(affectedMemberIds, ct);
            return new LeaveGroupResultDto { Dissolved = true };
        }

        var isOwner = leavingMember.Title == GroupOwnerRole;
        if (isOwner)
        {
            if (!req.SuccessorId.HasValue)
                throw new ServiceException(StatusCodes.Status400BadRequest, "SuccessorId is required when group owner leaves.");

            var successor = remainingMembers.FirstOrDefault(cm => cm.AccountId == req.SuccessorId.Value);
            if (successor == null)
                throw new ServiceException(StatusCodes.Status400BadRequest, "Successor must be an existing group member.");

            successor.Title = GroupOwnerRole;
        }

        _context.ConversationMembers.Remove(leavingMember);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await InvalidateGroupCachesAsync(conversationId, ct);
        await InvalidateThreadCachesAsync(affectedMemberIds, ct);
        return new LeaveGroupResultDto { Dissolved = false };
    }

    public async Task RemoveGroupMemberAsync(int conversationId, RemoveGroupMemberRequest req, CancellationToken ct = default)
    {
        if (conversationId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "conversationId is required.");
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (req.OwnerId <= 0 || req.MemberId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "OwnerId and MemberId are required.");
        if (req.OwnerId == req.MemberId)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Group owner cannot remove themselves.");

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && c.IsGroup, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Group not found.");

        await EnsureGroupOwnerAsync(conversationId, req.OwnerId, ct);
        var affectedMemberIds = await GetConversationMemberIdsAsync(conversationId, ct);

        var member = await _context.ConversationMembers
            .FirstOrDefaultAsync(cm =>
                cm.ConversationId == conversationId &&
                cm.AccountId == req.MemberId &&
                cm.Title != GroupOwnerRole, ct);

        if (member == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Member not found.");

        _context.ConversationMembers.Remove(member);
        await _context.SaveChangesAsync(ct);
        await InvalidateGroupCachesAsync(conversationId, ct);
        await InvalidateThreadCachesAsync(affectedMemberIds, ct);
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

        var version = await GetCacheStampValueAsync(CacheKeys.ConversationMessagesVersion(conversationId), ct);
        return await _cache.GetOrCreateAsync(
            CacheKeys.ConversationMessages(conversationId, limit, beforeMessageId, version),
            MessagesCacheTtl,
            async token =>
            {
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

                var messages = await query.Take(limit).ToListAsync(token);
                messages.Reverse();

                return messages.Select(MapMessage).ToList();
            },
            ct);
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

        return await _cache.GetOrCreateAsync(
            CacheKeys.ConversationPeer(conversationId, meId),
            PeerInfoCacheTtl,
            async token =>
            {
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
                    .ToListAsync(token);

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
            },
            ct);
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

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, ct);

        if (conversation == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Conversation not found.");

        var sender = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == senderId, ct);

        if (sender == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Sender not found.");

        var senderMembership = await _context.ConversationMembers
            .AsNoTracking()
            .Where(cm => cm.ConversationId == conversationId && cm.AccountId == senderId)
            .Select(cm => new { cm.Title })
            .FirstOrDefaultAsync(ct);

        if (senderMembership == null)
            throw new ServiceException(StatusCodes.Status403Forbidden, "User is not a member of this conversation.");

        if (conversation.IsGroup &&
            conversation.OwnerOnlyMessages &&
            !string.Equals(senderMembership.Title, GroupOwnerRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceException(StatusCodes.Status403Forbidden, "Chỉ trưởng nhóm mới được gửi tin nhắn trong nhóm này.");
        }

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
        await InvalidateConversationMessageCachesAsync(conversationId, ct);
        await InvalidateConversationThreadCachesAsync(conversationId, ct);
        return MapMessage(message);
    }

    private async Task EnsureMemberAsync(int conversationId, int accountId, CancellationToken ct)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(cm => cm.ConversationId == conversationId && cm.AccountId == accountId, ct);

        if (!isMember)
            throw new ServiceException(StatusCodes.Status403Forbidden, "User is not a member of this conversation.");
    }

    private async Task EnsureGroupOwnerAsync(int conversationId, int accountId, CancellationToken ct)
    {
        var isOwner = await _context.ConversationMembers
            .AsNoTracking()
            .AnyAsync(cm =>
                cm.ConversationId == conversationId &&
                cm.AccountId == accountId &&
                cm.Title == GroupOwnerRole, ct);

        if (!isOwner)
            throw new ServiceException(StatusCodes.Status403Forbidden, "Only group owner can update this group.");
    }

    private async Task DissolveGroupAsync(Conversation conversation, CancellationToken ct)
    {
        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversation.ConversationId)
            .ToListAsync(ct);

        var members = await _context.ConversationMembers
            .Where(cm => cm.ConversationId == conversation.ConversationId)
            .ToListAsync(ct);

        _context.Messages.RemoveRange(messages);
        _context.ConversationMembers.RemoveRange(members);
        _context.Conversations.Remove(conversation);

        await _context.SaveChangesAsync(ct);
    }

    private async Task<long> GetCacheStampValueAsync(string key, CancellationToken ct)
    {
        var stamp = await _cache.GetAsync<CacheStamp>(key, ct);
        return stamp?.Value ?? 0;
    }

    private Task InvalidateGroupCachesAsync(int conversationId, CancellationToken ct)
    {
        return _cache.SetAsync(CacheKeys.GroupInfoVersion(conversationId), CacheStamp.New(), VersionCacheTtl, ct);
    }

    private Task InvalidateConversationMessageCachesAsync(int conversationId, CancellationToken ct)
    {
        return _cache.SetAsync(CacheKeys.ConversationMessagesVersion(conversationId), CacheStamp.New(), VersionCacheTtl, ct);
    }

    private async Task InvalidateConversationThreadCachesAsync(int conversationId, CancellationToken ct)
    {
        var memberIds = await GetConversationMemberIdsAsync(conversationId, ct);
        await InvalidateThreadCachesAsync(memberIds, ct);
    }

    private async Task<List<int>> GetConversationMemberIdsAsync(int conversationId, CancellationToken ct)
    {
        return await _context.ConversationMembers
            .AsNoTracking()
            .Where(cm => cm.ConversationId == conversationId)
            .Select(cm => cm.AccountId)
            .ToListAsync(ct);
    }

    private async Task InvalidateThreadCachesAsync(IEnumerable<int> accountIds, CancellationToken ct)
    {
        foreach (var accountId in accountIds.Where(id => id > 0).Distinct())
        {
            await _cache.RemoveAsync(CacheKeys.ConversationThreads(accountId), ct);
        }
    }

    private static ConversationDto MapConversation(Conversation conversation)
    {
        return new ConversationDto
        {
            ConversationId = conversation.ConversationId,
            IsGroup = conversation.IsGroup,
            Title = conversation.Title,
            AvatarUrl = conversation.AvatarUrl,
            OwnerOnlyMessages = conversation.OwnerOnlyMessages,
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
