using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebServer.Services
{
    public class WebSocketHandler
    {
        private readonly RealtimeHub _hub;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public WebSocketHandler(RealtimeHub hub)
        {
            _hub = hub;
        }

        public async Task HandleAsync(WebSocket ws, string userId, CancellationToken ct)
        {
            var socketId = _hub.AddSocket(userId, ws);

            // optional: gửi hello
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

                    //await HandleClientMessageAsync(socketId, ws, msg, ct);
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
                catch { /* ignore */ }

                await _hub.RemoveSocketAsync(socketId);
            }
        }

        private async Task HandleClientMessageAsync(string socketId, string userId, WebSocket ws, string json, CancellationToken ct)
        {
            // message mẫu client:
            // { "type":"subscribe", "conversationId":3 }
            // { "type":"unsubscribe", "conversationId":3 }
            // { "type":"ping" }

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
                    {
                        // client gửi: { type:"call.send", toUserId:"2", payload:{...} }

                        if (!doc.RootElement.TryGetProperty("toUserId", out var toEl))
                        {
                            await SendAsync(ws, new { type = "call.error", message = "toUserId is required" }, ct);
                            break;
                        }

                        var toUserId = toEl.GetString();
                        if (string.IsNullOrWhiteSpace(toUserId))
                        {
                            await SendAsync(ws, new { type = "call.error", message = "toUserId is invalid" }, ct);
                            break;
                        }

                        object? payload = null;
                        if (doc.RootElement.TryGetProperty("payload", out var p))
                        {
                            payload = JsonSerializer.Deserialize<object>(p.GetRawText(), JsonOpts);
                        }

                        await _hub.SendCallToUserAsync(toUserId, userId, payload ?? new { });

                        // optional ack để debug
                        await SendAsync(ws, new { type = "call.sent", toUserId }, ct);

                        break;
                    }
            }
        }

        private static async Task<string> ReadFullMessageAsync(WebSocket ws, byte[] buffer, WebSocketReceiveResult first, CancellationToken ct)
        {
            // nhận nhiều frame nếu message lớn
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