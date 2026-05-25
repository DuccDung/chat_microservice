using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using WebServer.Dtos;
using WebServer.Interfaces;
using WebServer.Services;
using WebServer.ViewModels.WebServer.ViewModels;
namespace WebServer.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IUserService _userService;
        private readonly IConversationService _conversationService;
        private readonly RealtimeHub realtime;
        public HomeController(IUserService userService, IConversationService conversationService , RealtimeHub realtime)
        {
            _userService = userService;
            _conversationService = conversationService;
            this.realtime = realtime;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Main()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Auth");

            var user = await _userService.GetUserByIdAsync(int.Parse(userId));
            ViewBag.User = user;
            return View();
        }

        // Render cái form modal (HTML)
        [HttpGet("/chat/search_view")]
        public async Task<IActionResult> SearchView()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return PartialView("Partials/_FormFriends", new List<ConversationThreadDto>());

            try
            {
                var threads = await _conversationService.GetThreadsAsync(int.Parse(userIdStr));
                return PartialView("Partials/_FormFriends", threads);
            }
            catch
            {
                return PartialView("Partials/_FormFriends", new List<ConversationThreadDto>());
            }
        }

        [HttpGet("/chat/personal")]
        public async Task<IActionResult> PersonalView(int userId)
        {
            var friend = await _userService.GetUserByIdAsync(userId);
            return PartialView("Partials/_FormPersonal", friend);
        }

        [HttpGet("/chat/search_user")]
        public async Task<IActionResult> SearchUser([FromQuery] string email, [FromQuery] int limit = 20)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Content("<div class='form_friends__empty'>Bạn chưa đăng nhập.</div>", "text/html");

            try
            {
                var user = await _userService.SearchUsersByEmailAsync(email, limit);
                if (user.AccountId == currentUserId)
                {
                    return Content("<div class='form_friends__empty'>Đây là tài khoản của bạn!</div>", "text/html");
                }
                if (user.AccountId == 0)
                {
                    return Content("<div class='form_friends__empty'>Không tìm thấy người dùng nào.</div>", "text/html");
                }
                return PartialView("Partials/_FriendSearchResults", user);
            }
            catch
            {
                Response.StatusCode = 500;
                return Content("<div class='form_friends__empty'>Có lỗi xảy ra khi tìm kiếm.</div>", "text/html");
            }
        }

        [HttpGet("/chat/users/search")]
        public async Task<IActionResult> SearchUserJson([FromQuery] string email, [FromQuery] int limit = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Not logged in." });

            try
            {
                var user = await _userService.SearchUsersByEmailAsync(email, limit);
                if (user.AccountId == 0)
                    return NotFound(new { message = "Không tìm thấy người dùng nào." });
                if (user.AccountId == int.Parse(userId))
                    return BadRequest(new { message = "Đây là tài khoản của bạn." });

                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/chat/conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateDirectChatRequest req)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            if (req == null || req.FriendId <= 0)
                return BadRequest(new { message = "Vui lòng chọn người muốn nhắn tin." });

            try
            {
                var conversation = await _conversationService.CreateOrGetOneToOneAsync(int.Parse(userIdStr), req.FriendId);
                return Ok(conversation);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/chat/groups")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupChatRequest req)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            if (req == null)
                return BadRequest(new { message = "Body is required." });

            try
            {
                if (!int.TryParse(userIdStr, out var ownerId) || ownerId <= 0)
                    return Unauthorized(new { message = "Not logged in." });

                var memberIds = (req.MemberIds ?? new List<int>())
                    .Where(id => id > 0 && id != ownerId)
                    .Distinct()
                    .ToList();

                if (memberIds.Count == 0)
                    return BadRequest(new { message = "Vui lòng chọn ít nhất một thành viên khác bạn." });

                var group = await _conversationService.CreateGroupAsync(ownerId, req.Title, memberIds);
                return Ok(group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("/chat/groups/{conversationId:int}")]
        public async Task<IActionResult> GroupInfoView(int conversationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Content("<div class='form_friends__empty'>Bạn chưa đăng nhập.</div>", "text/html");

            try
            {
                var meId = int.Parse(userIdStr);
                var group = await _conversationService.GetGroupInfoAsync(conversationId, meId);
                ViewBag.InviteUrl = $"{Request.Scheme}://{Request.Host}/chat/groups/{conversationId}/join";
                return PartialView("Partials/_GroupInfo", group);
            }
            catch
            {
                Response.StatusCode = 500;
                return Content("<div class='form_friends__empty'>Không tải được thông tin nhóm.</div>", "text/html");
            }
        }

        [HttpGet("/chat/groups/{conversationId:int}/join")]
        public async Task<IActionResult> JoinGroupByLink(int conversationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return RedirectToAction("Login", "Auth");

            await _conversationService.JoinGroupAsync(conversationId, int.Parse(userIdStr));
            return RedirectToAction(nameof(Main));
        }

        [HttpPost("/chat/groups/{conversationId:int}/settings")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateGroupSettings(int conversationId, [FromForm] UpdateGroupSettingsRequest req)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            if (req == null)
                return BadRequest(new { message = "Body is required." });

            try
            {
                var ownerId = int.Parse(userIdStr);
                string? avatarUrl = null;

                if (req.Avatar != null && req.Avatar.Length > 0)
                    avatarUrl = await SaveGroupAvatarAsync(req.Avatar);

                var title = req.Title?.Trim();
                var updated = await _conversationService.UpdateGroupAsync(
                    conversationId,
                    ownerId,
                    title,
                    avatarUrl);

                return Ok(updated);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/chat/groups/{conversationId:int}/members/{memberId:int}/remove")]
        public async Task<IActionResult> RemoveGroupMember(int conversationId, int memberId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            try
            {
                await _conversationService.RemoveGroupMemberAsync(conversationId, int.Parse(userIdStr), memberId);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/chat/groups/{conversationId:int}/leave")]
        public async Task<IActionResult> LeaveGroup(int conversationId, [FromBody] LeaveGroupWebRequest req)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            try
            {
                var result = await _conversationService.LeaveGroupAsync(
                    conversationId,
                    int.Parse(userIdStr),
                    req?.SuccessorId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("/chat/threads")]
        public async Task<IActionResult> ThreadsView([FromServices] IConversationService conversationService)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Content("<div class='threads-empty'>Bạn chưa đăng nhập.</div>", "text/html");

            var accountId = int.Parse(userIdStr);

            try
            {
                var threads = await conversationService.GetThreadsAsync(accountId);
                return PartialView("Partials/_ChatThreads", threads);
            }
            catch
            {
                Response.StatusCode = 500;
                return Content("<div class='threads-empty'>Không tải được danh sách cuộc trò chuyện.</div>", "text/html");
            }
        }
        [HttpGet("/chat/conversation")]
        public async Task<IActionResult> ConversationView(
            int conversationId,
            [FromServices] IConversationService conversationService)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Content("<div class='threads-empty'>Bạn chưa đăng nhập.</div>", "text/html");

            var meId = int.Parse(userIdStr);

            try
            {
                var messages = await conversationService.GetMessagesAsync(conversationId, meId, limit: 50);

                var vm = new ConversationMessagesVm
                {
                    ConversationId = conversationId,
                    MeAccountId = meId,
                    Messages = messages
                        .Where(x => !x.IsRemove) // nếu muốn ẩn message đã remove
                        .OrderBy(x => x.CreatedAt)
                        .ToList()
                };

                return PartialView("Partials/_ConversationMessages", vm);
            }
            catch
            {
                Response.StatusCode = 500;
                return Content("<div class='threads-empty'>Không tải được tin nhắn cuộc trò chuyện.</div>", "text/html");
            }
        }

        // POST /chat/send_message
        [HttpPost("/chat/send_message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            if (req == null) return BadRequest(new { message = "Body is required." });
            if (req.ConversationId <= 0) return BadRequest(new { message = "ConversationId is required." });
            if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest(new { message = "Content is required." });

            // senderId lấy từ auth cookie (claims)
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            var senderId = int.Parse(userIdStr);

            try
            {
                var created = await _conversationService.SendTextMessageAsync(
                    req.ConversationId,
                    senderId,
                    req.Content.Trim(),
                    req.ParentMessageId
                );
                // Broadcast realtime cho những socket đang subscribe conversation này (trừ sender)
                await realtime.BroadcastToConversationAsync(
                    req.ConversationId,
                    new { type = "message-text", conversationId = req.ConversationId, payload = created },
                    excludeUserId: userIdStr
                );
                // trả về message dto (hoặc ok=true cũng được)
                return Ok(created);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/chat/send_image")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> SendImage(
    [FromForm] SendImageUploadRequest req)
        {
            if (req == null || req.File == null)
                return BadRequest(new { message = "File is required." });

            if (req.ConversationId <= 0)
                return BadRequest(new { message = "ConversationId is required." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized();

            var senderId = int.Parse(userIdStr);

            try
            {
                var created = await _conversationService.SendImageMessageAsync(
                    req.ConversationId,
                    senderId,
                    req.File,
                    req.ParentMessageId);

                // Realtime broadcast giống text
                await realtime.BroadcastToConversationAsync(
                    req.ConversationId,
                    new
                    {
                        type = "message-image",
                        conversationId = req.ConversationId,
                        payload = created
                    },
                    excludeUserId: userIdStr
                );

                return Ok(created);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpPost("/chat/send_audio")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendAudio([FromForm] SendAudioUploadRequest req)
        {
            if (req == null || req.File == null)
                return BadRequest(new { message = "File is required." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized();

            var senderId = int.Parse(userIdStr);

            try
            {
                var created = await _conversationService.SendAudioMessageAsync(
                    req.ConversationId,
                    senderId,
                    req.File,
                    req.ParentMessageId
                );

                await realtime.BroadcastToConversationAsync(
                    req.ConversationId,
                    new
                    {
                        type = "message-audio",
                        conversationId = req.ConversationId,
                        payload = created
                    },
                    excludeUserId: userIdStr
                );

                return Ok(created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        private static string WsEventTypeFromMessageType(string? messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType)) return "message-text";

            return messageType.Trim().ToLowerInvariant() switch
            {
                "image" => "message-image",
                "audio" => "message-audio",
                "text" => "message-text",
                _ => "message-text"
            };
        }
        [HttpGet("/chat/peer")]
        public async Task<IActionResult> GetPeer([FromQuery] int conversationId)
        {
            if (conversationId <= 0)
                return BadRequest(new { message = "conversationId is required." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            var meId = int.Parse(userIdStr);

            try
            {
                var peer = await _conversationService.GetPeerAsync(conversationId, meId);
                if (peer == null) return NotFound(new { message = "Peer not found." });

                return Ok(peer); // Me and Peer{ accountId, accountName, email, photoPath }
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("/call/incoming_popup")]
        public IActionResult IncomingCallPopup(
         [FromQuery] int conversationId,
         [FromQuery] string callType,

         [FromQuery] int fromUserId,
         [FromQuery] string fromUserName,
         [FromQuery] string? fromUserPhoto,

         [FromQuery] int toUserId,
         [FromQuery] string toUserName,
         [FromQuery] string? toUserPhoto
 )
        {
            var vm = new IncomingCallVm
            {
                ConversationId = conversationId,
                CallType = string.IsNullOrWhiteSpace(callType) ? "video" : callType,

                FromUserId = fromUserId,
                FromUserName = fromUserName,
                FromUserPhoto = fromUserPhoto,

                ToUserId = toUserId,
                ToUserName = toUserName,
                ToUserPhoto = toUserPhoto
            };

            return PartialView("Partials/_IncomingCallPopup", vm);
        }
        [HttpGet("/call/popup")]
        public async Task<IActionResult> CallPopup(
        [FromQuery] int conversationId,
        [FromQuery] string? callType
    )
        {
            if (conversationId <= 0)
                return BadRequest(new { message = "conversationId is required." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized(new { message = "Not logged in." });

            var meId = int.Parse(userIdStr);

            var peerResp = await _conversationService.GetPeerAsync(conversationId, meId);
            if (peerResp == null)
                return NotFound(new { message = "Peer not found." });

            // peerResp: ConversationPeerResponseDto { Me, Peer }
            var vm = new CallPopupVm
            {
                ConversationId = conversationId,
                CallType = string.IsNullOrWhiteSpace(callType) ? "video" : callType.Trim(),

                MeId = peerResp.Me.AccountId,
                MeName = peerResp.Me.AccountName,
                MePhoto = peerResp.Me.PhotoPath,

                PeerId = peerResp.Peer.AccountId,
                PeerName = peerResp.Peer.AccountName,
                PeerPhoto = peerResp.Peer.PhotoPath
            };

            return PartialView("Partials/_CallPopup", vm);
        }

        private static async Task<string> SaveGroupAvatarAsync(IFormFile file)
        {
            var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".gif"
            };

            var ext = Path.GetExtension(file.FileName);
            if (!allowedExts.Contains(ext))
                throw new Exception("Chỉ hỗ trợ ảnh .jpg, .jpeg, .png, .webp, .gif.");

            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "groups");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/groups/{fileName}";
        }
    }

    public sealed class CreateGroupChatRequest
    {
        public string Title { get; set; } = "";
        public List<int> MemberIds { get; set; } = new();
    }

    public sealed class CreateDirectChatRequest
    {
        public int FriendId { get; set; }
    }

    public sealed class UpdateGroupSettingsRequest
    {
        public string? Title { get; set; }
        public IFormFile? Avatar { get; set; }
    }

    public sealed class LeaveGroupWebRequest
    {
        public int? SuccessorId { get; set; }
    }

}
