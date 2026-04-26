using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebServer.Services
{
    public class RealtimeHub
    {
        // userId -> sockets (1 user có thể mở nhiều tab)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _userSockets = new();

        // socketId -> set conversationIds đã subscribe
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _socketSubscriptions = new();

        // socketId -> userId (để cleanup)
        private readonly ConcurrentDictionary<string, string> _socketToUser = new();

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public string AddSocket(string userId, WebSocket socket)
        {
            var socketId = Guid.NewGuid().ToString("N");

            var socketsOfUser = _userSockets.GetOrAdd(userId, _ => new ConcurrentDictionary<string, WebSocket>());
            socketsOfUser[socketId] = socket;

            _socketSubscriptions.TryAdd(socketId, new ConcurrentDictionary<int, byte>());
            _socketToUser[socketId] = userId;

            return socketId;
        }

        public async Task RemoveSocketAsync(string socketId)
        {
            if (_socketToUser.TryRemove(socketId, out var userId))
            {
                if (_userSockets.TryGetValue(userId, out var sockets))
                {
                    sockets.TryRemove(socketId, out _);

                    // nếu user không còn socket nào -> xoá luôn entry
                    if (sockets.IsEmpty)
                        _userSockets.TryRemove(userId, out _);
                }
            }

            _socketSubscriptions.TryRemove(socketId, out _);

            await Task.CompletedTask;
        }

        public void Subscribe(string socketId, int conversationId)
        {
            if (!_socketSubscriptions.TryGetValue(socketId, out var set))
                return;

            set[conversationId] = 1;
        }

        public void Unsubscribe(string socketId, int conversationId)
        {
            if (!_socketSubscriptions.TryGetValue(socketId, out var set))
                return;

            set.TryRemove(conversationId, out _);
        }

        public async Task SendToUserAsync(string userId, object message)
        {
            if (!_userSockets.TryGetValue(userId, out var sockets) || sockets.IsEmpty)
                return;

            var bytes = Serialize(message);

            foreach (var kv in sockets)
            {
                var ws = kv.Value;
                await SafeSendAsync(ws, bytes);
            }
        }

        public async Task BroadcastToConversationAsync(int conversationId, object message, string? excludeUserId = null)
        {
            var bytes = Serialize(message);

            // Duyệt tất cả sockets, socket nào subscribe convId thì gửi
            foreach (var pair in _socketSubscriptions)
            {
                var socketId = pair.Key;
                var convSet = pair.Value;

                if (!convSet.ContainsKey(conversationId))
                    continue;

                // loại sender (nếu cần)
                if (!string.IsNullOrWhiteSpace(excludeUserId)
                    && _socketToUser.TryGetValue(socketId, out var uid)
                    && uid == excludeUserId)
                {
                    continue;
                }

                // lấy websocket object
                if (_socketToUser.TryGetValue(socketId, out var userId)
                    && _userSockets.TryGetValue(userId, out var sockets)
                    && sockets.TryGetValue(socketId, out var ws))
                {
                    await SafeSendAsync(ws, bytes);
                }
            }
        }

        public Task SendNotificationToUserAsync(string userId, object payload)
        {
            var envelope = new
            {
                type = "notification",
                payload
            };
            return SendToUserAsync(userId, envelope);
        }

        private static byte[] Serialize(object message)
        {
            var json = JsonSerializer.Serialize(message, JsonOpts);
            return Encoding.UTF8.GetBytes(json);
        }

        private static async Task SafeSendAsync(WebSocket ws, byte[] bytes)
        {
            try
            {
                if (ws.State != WebSocketState.Open) return;
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
            }
        }

        public Task SendCallToUserAsync(string toUserId, string fromUserId, object payload)
        {
            var envelope = new
            {
                type = "call.event",   
                fromUserId,
                toUserId,
                payload
            };

            return SendToUserAsync(toUserId, envelope);
        }
    }
}