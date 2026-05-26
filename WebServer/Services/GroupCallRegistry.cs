using System.Collections.Concurrent;
using WebServer.Dtos;

namespace WebServer.Services
{
    public sealed class GroupCallRegistry
    {
        private readonly ConcurrentDictionary<string, GroupCallRoom> _rooms = new();
        private readonly ConcurrentDictionary<int, string> _activeRoomByConversation = new();

        public GroupCallRoomSnapshot StartRoom(
            int conversationId,
            string? callType,
            int startedByUserId,
            GroupInfoDto group,
            out bool created)
        {
            created = false;

            if (_activeRoomByConversation.TryGetValue(conversationId, out var existingRoomId)
                && _rooms.TryGetValue(existingRoomId, out var existing)
                && existing.Status == "active")
            {
                return Snapshot(existing);
            }

            var roomId = Guid.NewGuid().ToString("N");
            var normalizedCallType = string.Equals(callType, "audio", StringComparison.OrdinalIgnoreCase)
                ? "audio"
                : "video";

            var room = new GroupCallRoom
            {
                RoomId = roomId,
                ConversationId = conversationId,
                CallType = normalizedCallType,
                StartedByUserId = startedByUserId,
                StartedAtUtc = DateTime.UtcNow,
                GroupName = group.Title,
                GroupPhoto = group.AvatarUrl,
                Status = "active"
            };

            foreach (var member in group.Members.Where(m => m.AccountId > 0))
            {
                var isCaller = member.AccountId == startedByUserId;
                room.Participants[member.AccountId] = new GroupCallParticipant
                {
                    AccountId = member.AccountId,
                    AccountName = member.AccountName,
                    PhotoPath = member.PhotoPath,
                    Status = isCaller ? "joined" : "invited",
                    MicEnabled = true,
                    CameraEnabled = normalizedCallType != "audio",
                    JoinedAtUtc = isCaller ? DateTime.UtcNow : null
                };
            }

            if (!room.Participants.ContainsKey(startedByUserId))
            {
                room.Participants[startedByUserId] = new GroupCallParticipant
                {
                    AccountId = startedByUserId,
                    AccountName = "Người dùng",
                    Status = "joined",
                    MicEnabled = true,
                    CameraEnabled = normalizedCallType != "audio",
                    JoinedAtUtc = DateTime.UtcNow
                };
            }

            _rooms[roomId] = room;
            _activeRoomByConversation[conversationId] = roomId;
            created = true;

            return Snapshot(room);
        }

        public GroupCallRoomSnapshot? Join(string? roomId, int userId)
        {
            if (!TryGetActiveRoom(roomId, out var room)) return null;

            lock (room.Sync)
            {
                if (!room.Participants.TryGetValue(userId, out var participant))
                    return null;

                participant.Status = "joined";
                participant.MicEnabled = true;
                participant.CameraEnabled = room.CallType != "audio";
                participant.JoinedAtUtc ??= DateTime.UtcNow;
                participant.LeftAtUtc = null;

                return Snapshot(room);
            }
        }

        public GroupCallRoomSnapshot? Decline(string? roomId, int userId)
        {
            if (!TryGetActiveRoom(roomId, out var room)) return null;

            lock (room.Sync)
            {
                if (!room.Participants.TryGetValue(userId, out var participant))
                    return null;

                participant.Status = "declined";
                participant.LeftAtUtc = DateTime.UtcNow;

                return Snapshot(room);
            }
        }

        public GroupCallRoomSnapshot? Leave(string? roomId, int userId, out bool ended)
        {
            ended = false;
            if (!TryGetRoom(roomId, out var room)) return null;

            lock (room.Sync)
            {
                if (!room.Participants.TryGetValue(userId, out var participant))
                    return null;

                participant.Status = "left";
                participant.LeftAtUtc = DateTime.UtcNow;

                if (!room.Participants.Values.Any(p => p.Status == "joined"))
                {
                    room.Status = "ended";
                    room.EndedAtUtc = DateTime.UtcNow;
                    RemoveActiveRoomIndex(room);
                    ended = true;
                }

                return Snapshot(room);
            }
        }

        public GroupCallRoomSnapshot? End(string? roomId, int endedByUserId)
        {
            if (!TryGetRoom(roomId, out var room)) return null;

            lock (room.Sync)
            {
                room.Status = "ended";
                room.EndedAtUtc = DateTime.UtcNow;

                foreach (var participant in room.Participants.Values)
                {
                    if (participant.Status == "joined" || participant.Status == "invited")
                    {
                        participant.Status = participant.AccountId == endedByUserId ? "left" : participant.Status;
                        participant.LeftAtUtc ??= DateTime.UtcNow;
                    }
                }

                RemoveActiveRoomIndex(room);

                return Snapshot(room);
            }
        }

        public GroupCallRoomSnapshot? UpdateState(
            string? roomId,
            int userId,
            bool? micEnabled,
            bool? cameraEnabled)
        {
            if (!TryGetActiveRoom(roomId, out var room)) return null;

            lock (room.Sync)
            {
                if (!room.Participants.TryGetValue(userId, out var participant))
                    return null;

                if (micEnabled.HasValue) participant.MicEnabled = micEnabled.Value;
                if (cameraEnabled.HasValue) participant.CameraEnabled = cameraEnabled.Value;

                return Snapshot(room);
            }
        }

        public bool IsJoinedParticipant(string? roomId, int userId)
        {
            if (!TryGetActiveRoom(roomId, out var room)) return false;

            lock (room.Sync)
            {
                return room.Participants.TryGetValue(userId, out var participant)
                    && participant.Status == "joined";
            }
        }

        public bool IsParticipant(string? roomId, int userId)
        {
            if (!TryGetRoom(roomId, out var room)) return false;

            lock (room.Sync)
            {
                return room.Participants.ContainsKey(userId);
            }
        }

        public GroupCallRoomSnapshot? GetSnapshot(string? roomId)
        {
            return TryGetRoom(roomId, out var room) ? Snapshot(room) : null;
        }

        public IReadOnlyList<int> GetAllParticipantIds(string? roomId)
        {
            if (!TryGetRoom(roomId, out var room)) return Array.Empty<int>();

            lock (room.Sync)
            {
                return room.Participants.Keys.ToList();
            }
        }

        public IReadOnlyList<int> GetJoinedParticipantIds(string? roomId)
        {
            if (!TryGetRoom(roomId, out var room)) return Array.Empty<int>();

            lock (room.Sync)
            {
                return room.Participants.Values
                    .Where(p => p.Status == "joined")
                    .Select(p => p.AccountId)
                    .ToList();
            }
        }

        private bool TryGetActiveRoom(string? roomId, out GroupCallRoom room)
        {
            if (TryGetRoom(roomId, out room) && room.Status == "active")
                return true;

            room = default!;
            return false;
        }

        private bool TryGetRoom(string? roomId, out GroupCallRoom room)
        {
            if (!string.IsNullOrWhiteSpace(roomId) && _rooms.TryGetValue(roomId, out room!))
                return true;

            room = default!;
            return false;
        }

        private void RemoveActiveRoomIndex(GroupCallRoom room)
        {
            if (_activeRoomByConversation.TryGetValue(room.ConversationId, out var activeRoomId)
                && string.Equals(activeRoomId, room.RoomId, StringComparison.Ordinal))
            {
                _activeRoomByConversation.TryRemove(room.ConversationId, out _);
            }
        }

        private static GroupCallRoomSnapshot Snapshot(GroupCallRoom room)
        {
            lock (room.Sync)
            {
                return new GroupCallRoomSnapshot
                {
                    RoomId = room.RoomId,
                    ConversationId = room.ConversationId,
                    CallType = room.CallType,
                    Status = room.Status,
                    StartedByUserId = room.StartedByUserId,
                    StartedAtUtc = room.StartedAtUtc,
                    EndedAtUtc = room.EndedAtUtc,
                    GroupName = room.GroupName,
                    GroupPhoto = room.GroupPhoto,
                    Participants = room.Participants.Values
                        .OrderByDescending(p => p.Status == "joined")
                        .ThenBy(p => p.AccountName)
                        .Select(p => new GroupCallParticipantSnapshot
                        {
                            AccountId = p.AccountId,
                            AccountName = p.AccountName,
                            PhotoPath = p.PhotoPath,
                            Status = p.Status,
                            MicEnabled = p.MicEnabled,
                            CameraEnabled = p.CameraEnabled,
                            JoinedAtUtc = p.JoinedAtUtc,
                            LeftAtUtc = p.LeftAtUtc
                        })
                        .ToList()
                };
            }
        }

        private sealed class GroupCallRoom
        {
            public object Sync { get; } = new();
            public string RoomId { get; init; } = "";
            public int ConversationId { get; init; }
            public string CallType { get; init; } = "video";
            public int StartedByUserId { get; init; }
            public DateTime StartedAtUtc { get; init; }
            public DateTime? EndedAtUtc { get; set; }
            public string GroupName { get; init; } = "";
            public string? GroupPhoto { get; init; }
            public string Status { get; set; } = "active";
            public Dictionary<int, GroupCallParticipant> Participants { get; } = new();
        }

        private sealed class GroupCallParticipant
        {
            public int AccountId { get; init; }
            public string AccountName { get; init; } = "";
            public string? PhotoPath { get; init; }
            public string Status { get; set; } = "invited";
            public bool MicEnabled { get; set; } = true;
            public bool CameraEnabled { get; set; } = true;
            public DateTime? JoinedAtUtc { get; set; }
            public DateTime? LeftAtUtc { get; set; }
        }
    }

    public sealed class GroupCallRoomSnapshot
    {
        public string RoomId { get; set; } = "";
        public int ConversationId { get; set; }
        public string CallType { get; set; } = "video";
        public string Status { get; set; } = "active";
        public int StartedByUserId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public string GroupName { get; set; } = "";
        public string? GroupPhoto { get; set; }
        public List<GroupCallParticipantSnapshot> Participants { get; set; } = new();
    }

    public sealed class GroupCallParticipantSnapshot
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string? PhotoPath { get; set; }
        public string Status { get; set; } = "invited";
        public bool MicEnabled { get; set; } = true;
        public bool CameraEnabled { get; set; } = true;
        public DateTime? JoinedAtUtc { get; set; }
        public DateTime? LeftAtUtc { get; set; }
    }
}
