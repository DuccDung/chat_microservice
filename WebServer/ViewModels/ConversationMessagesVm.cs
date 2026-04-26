using WebServer.Dtos;

namespace WebServer.ViewModels
{
    namespace WebServer.ViewModels
    {
        public class ConversationMessagesVm
        {
            public int ConversationId { get; set; }
            public int MeAccountId { get; set; }
            public List<ConversationMessageDto> Messages { get; set; } = new();
        }
        public class IncomingCallVm
        {
            // người gọi (caller)
            public int FromUserId { get; set; }
            public string FromUserName { get; set; } = "";
            public string? FromUserPhoto { get; set; }

            // người nhận (me - callee)
            public int ToUserId { get; set; }
            public string ToUserName { get; set; } = "";
            public string? ToUserPhoto { get; set; }

            // call meta
            public int ConversationId { get; set; }
            public string CallType { get; set; } = "video"; // "video" | "audio"
        }
        public class CallPopupVm
        {
            public int ConversationId { get; set; }
            public string CallType { get; set; } = "video";

            public int MeId { get; set; }
            public string? MeName { get; set; }
            public string? MePhoto { get; set; }

            public int PeerId { get; set; }
            public string? PeerName { get; set; }
            public string? PeerPhoto { get; set; }
        }
    }
}
