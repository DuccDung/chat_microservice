using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebServer.Interfaces;

namespace WebServer.Services
{
    public class WebSocketHandler
    {
        private readonly RealtimeHub _hub;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GroupCallRegistry _groupCalls;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public WebSocketHandler(
            RealtimeHub hub,
            IServiceScopeFactory scopeFactory,
            GroupCallRegistry groupCalls)
        {
            _hub = hub;
            _scopeFactory = scopeFactory;
            _groupCalls = groupCalls;
        }

        public async Task HandleAsync(WebSocket ws, string userId, CancellationToken ct)
        {
            var socketId = _hub.AddSocket(userId, ws);

            await SendAsync(ws, new { type = "hello", userId }, ct);

            var buffer = new byte[8 * 1024];

            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(buffer, ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var msg = await ReadFullMessageAsync(ws, buffer, result, ct);
                    if (string.IsNullOrWhiteSpace(msg)) continue;

                    await HandleClientMessageAsync(socketId, userId, ws, msg, ct);
                }
            }
            catch
            {
                // client drop
            }
            finally
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch { }

                await _hub.RemoveSocketAsync(socketId);
            }
        }

        private async Task HandleClientMessageAsync(string socketId, string userId, WebSocket ws, string json, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl))
                return;

            var type = typeEl.GetString()?.ToLowerInvariant();

            switch (type)
            {
                case "ping":
                    await SendAsync(ws, new { type = "pong" }, ct);
                    break;

                case "subscribe":
                    if (doc.RootElement.TryGetProperty("conversationId", out var convEl)
                        && convEl.TryGetInt32(out var convId) && convId > 0)
                    {
                        _hub.Subscribe(socketId, convId);
                        await SendAsync(ws, new { type = "subscribed", conversationId = convId }, ct);
                    }
                    break;

                case "unsubscribe":
                    if (doc.RootElement.TryGetProperty("conversationId", out var convEl2)
                        && convEl2.TryGetInt32(out var convId2) && convId2 > 0)
                    {
                        _hub.Unsubscribe(socketId, convId2);
                        await SendAsync(ws, new { type = "unsubscribed", conversationId = convId2 }, ct);
                    }
                    break;

                case "call.send":
                    await HandleOneToOneCallAsync(userId, ws, doc, ct);
                    break;

                case "group.call.start":
                    await HandleGroupCallStartAsync(userId, ws, doc, ct);
                    break;

                case "group.call.accept":
                    await HandleGroupCallAcceptAsync(userId, ws, doc, ct);
                    break;

                case "group.call.decline":
                    await HandleGroupCallDeclineAsync(userId, ws, doc, ct);
                    break;

                case "group.call.leave":
                    await HandleGroupCallLeaveAsync(userId, ws, doc, ct);
                    break;

                case "group.call.end":
                    await HandleGroupCallEndAsync(userId, ws, doc, ct);
                    break;

                case "group.call.state":
                    await HandleGroupCallStateAsync(userId, ws, doc, ct);
                    break;

                case "group.call.signal":
                    await HandleGroupCallSignalAsync(userId, ws, doc, ct);
                    break;
            }
        }

        private async Task HandleOneToOneCallAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!doc.RootElement.TryGetProperty("toUserId", out var toEl))
            {
                await SendAsync(ws, new { type = "call.error", message = "toUserId is required" }, ct);
                return;
            }

            var toUserId = toEl.GetString();
            if (string.IsNullOrWhiteSpace(toUserId))
            {
                await SendAsync(ws, new { type = "call.error", message = "toUserId is invalid" }, ct);
                return;
            }

            object? payload = null;
            if (doc.RootElement.TryGetProperty("payload", out var p))
                payload = JsonSerializer.Deserialize<object>(p.GetRawText(), JsonOpts);

            await _hub.SendCallToUserAsync(toUserId, userId, payload ?? new { });
            await SendAsync(ws, new { type = "call.sent", toUserId }, ct);
        }

        private async Task HandleGroupCallStartAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId))
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Invalid current user." }, ct);
                return;
            }

            if (!TryReadPayloadInt(doc, "conversationId", out var conversationId) || conversationId <= 0)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "conversationId is required." }, ct);
                return;
            }

            var callType = ReadPayloadString(doc, "callType") ?? "video";

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
                var group = await conversationService.GetGroupInfoAsync(conversationId, meId);

                if (!group.Members.Any(m => m.AccountId == meId))
                {
                    await SendAsync(ws, new { type = "group.call.error", message = "Bạn không thuộc nhóm này." }, ct);
                    return;
                }

                var snapshot = _groupCalls.StartRoom(conversationId, callType, meId, group, out var created);
                if (!created)
                    snapshot = _groupCalls.Join(snapshot.RoomId, meId) ?? snapshot;

                await _hub.SendGroupCallToUserAsync(
                    userId,
                    userId,
                    new { kind = "started", room = snapshot, created });

                if (created)
                {
                    var invitees = snapshot.Participants
                        .Where(p => p.AccountId != meId)
                        .Select(p => p.AccountId);

                    await SendGroupCallEventToUsersAsync(
                        invitees,
                        userId,
                        new { kind = "invite", room = snapshot });
                }
                else
                {
                    await BroadcastGroupSnapshotAsync(
                        snapshot.RoomId,
                        userId,
                        new { kind = "participants", room = snapshot, joinedUserId = meId });
                }
            }
            catch (Exception ex)
            {
                await SendAsync(ws, new { type = "group.call.error", message = ex.Message }, ct);
            }
        }

        private async Task HandleGroupCallAcceptAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            var snapshot = _groupCalls.Join(roomId, meId);
            if (snapshot == null)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Cuộc gọi nhóm không còn hoạt động." }, ct);
                return;
            }

            await SendGroupCallEventToUsersAsync(
                _groupCalls.GetAllParticipantIds(roomId),
                userId,
                new { kind = "participants", room = snapshot, joinedUserId = meId });
        }

        private async Task HandleGroupCallDeclineAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            var snapshot = _groupCalls.Decline(roomId, meId);
            if (snapshot == null)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Cuộc gọi nhóm không còn hoạt động." }, ct);
                return;
            }

            await SendGroupCallEventToUsersAsync(
                _groupCalls.GetAllParticipantIds(roomId),
                userId,
                new { kind = "participants", room = snapshot, declinedUserId = meId });
        }

        private async Task HandleGroupCallLeaveAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            var recipients = _groupCalls.GetAllParticipantIds(roomId);
            var snapshot = _groupCalls.Leave(roomId, meId, out var ended);
            if (snapshot == null)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Cuộc gọi nhóm không còn hoạt động." }, ct);
                return;
            }

            await SendGroupCallEventToUsersAsync(
                recipients,
                userId,
                new { kind = ended ? "ended" : "participant_left", room = snapshot, leftUserId = meId });
        }

        private async Task HandleGroupCallEndAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            var recipients = _groupCalls.GetAllParticipantIds(roomId);
            var snapshot = _groupCalls.End(roomId, meId);
            if (snapshot == null)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Cuộc gọi nhóm không còn hoạt động." }, ct);
                return;
            }

            await SendGroupCallEventToUsersAsync(
                recipients,
                userId,
                new { kind = "ended", room = snapshot, endedByUserId = meId });
        }

        private async Task HandleGroupCallStateAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var meId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            var micEnabled = ReadPayloadBool(doc, "micEnabled");
            var cameraEnabled = ReadPayloadBool(doc, "cameraEnabled");

            var snapshot = _groupCalls.UpdateState(roomId, meId, micEnabled, cameraEnabled);
            if (snapshot == null)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Cuộc gọi nhóm không còn hoạt động." }, ct);
                return;
            }

            await SendGroupCallEventToUsersAsync(
                _groupCalls.GetJoinedParticipantIds(roomId),
                userId,
                new { kind = "participant_state", room = snapshot, participantId = meId });
        }

        private async Task HandleGroupCallSignalAsync(string userId, WebSocket ws, JsonDocument doc, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var fromUserId)) return;

            var roomId = ReadPayloadString(doc, "roomId");
            if (!TryReadPayloadInt(doc, "toUserId", out var toUserId) || toUserId <= 0)
            {
                await SendAsync(ws, new { type = "group.call.error", message = "toUserId is required." }, ct);
                return;
            }

            if (!_groupCalls.IsJoinedParticipant(roomId, fromUserId) || !_groupCalls.IsParticipant(roomId, toUserId))
            {
                await SendAsync(ws, new { type = "group.call.error", message = "Người gửi hoặc người nhận không thuộc cuộc gọi nhóm." }, ct);
                return;
            }

            object payload = new { };
            if (doc.RootElement.TryGetProperty("payload", out var p))
                payload = JsonSerializer.Deserialize<object>(p.GetRawText(), JsonOpts) ?? new { };

            await _hub.SendGroupCallToUserAsync(
                toUserId.ToString(),
                userId,
                payload);
        }

        private async Task BroadcastGroupSnapshotAsync(string roomId, string fromUserId, object payload)
        {
            await SendGroupCallEventToUsersAsync(
                _groupCalls.GetAllParticipantIds(roomId),
                fromUserId,
                payload);
        }

        private async Task SendGroupCallEventToUsersAsync(IEnumerable<int> userIds, string fromUserId, object payload)
        {
            foreach (var targetUserId in userIds.Where(id => id > 0).Distinct())
            {
                await _hub.SendGroupCallToUserAsync(targetUserId.ToString(), fromUserId, payload);
            }
        }

        private static string? ReadPayloadString(JsonDocument doc, string propertyName)
        {
            if (!doc.RootElement.TryGetProperty("payload", out var payload))
                return null;

            if (!payload.TryGetProperty(propertyName, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        private static bool? ReadPayloadBool(JsonDocument doc, string propertyName)
        {
            if (!doc.RootElement.TryGetProperty("payload", out var payload))
                return null;

            if (!payload.TryGetProperty(propertyName, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static bool TryReadPayloadInt(JsonDocument doc, string propertyName, out int value)
        {
            value = 0;

            if (!doc.RootElement.TryGetProperty("payload", out var payload))
                return false;

            if (!payload.TryGetProperty(propertyName, out var element))
                return false;

            if (element.ValueKind == JsonValueKind.Number)
                return element.TryGetInt32(out value);

            if (element.ValueKind == JsonValueKind.String)
                return int.TryParse(element.GetString(), out value);

            return false;
        }

        private static async Task<string> ReadFullMessageAsync(WebSocket ws, byte[] buffer, WebSocketReceiveResult first, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.Append(Encoding.UTF8.GetString(buffer, 0, first.Count));

            while (!first.EndOfMessage)
            {
                var r = await ws.ReceiveAsync(buffer, ct);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                first = r;
            }

            return sb.ToString();
        }

        private static Task SendAsync(WebSocket ws, object obj, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, JsonOpts));
            return ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
    }
}
