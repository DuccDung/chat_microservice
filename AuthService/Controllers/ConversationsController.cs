using AuthService.Dtos.Conversations;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [Route("api/conversations")]
    [ApiController]
    public class ConversationsController : ControllerBase
    {
        private readonly IMessageCallService _messageCallService;

        public ConversationsController(IMessageCallService messageCallService)
        {
            _messageCallService = messageCallService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrGetOneToOne([FromBody] CreateConversationRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.CreateOrGetOneToOneAsync(req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("groups")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupConversationRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.CreateGroupAsync(req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("threads")]
        public async Task<IActionResult> GetThreads([FromQuery] int accountId, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.GetThreadsAsync(accountId, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("{conversationId:int}/group")]
        public async Task<IActionResult> GetGroupInfo(int conversationId, [FromQuery] int me, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.GetGroupInfoAsync(conversationId, me, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/group/join")]
        public async Task<IActionResult> JoinGroup(int conversationId, [FromBody] JoinGroupRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.JoinGroupAsync(conversationId, req.AccountId, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPut("{conversationId:int}/group")]
        public async Task<IActionResult> UpdateGroup(int conversationId, [FromBody] UpdateGroupRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.UpdateGroupAsync(conversationId, req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/group/leave")]
        public async Task<IActionResult> LeaveGroup(int conversationId, [FromBody] LeaveGroupRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.LeaveGroupAsync(conversationId, req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpDelete("{conversationId:int}/group/members/{memberId:int}")]
        public async Task<IActionResult> RemoveGroupMember(
            int conversationId,
            int memberId,
            [FromQuery] int ownerId,
            CancellationToken ct)
        {
            try
            {
                await _messageCallService.RemoveGroupMemberAsync(
                    conversationId,
                    new RemoveGroupMemberRequest { OwnerId = ownerId, MemberId = memberId },
                    ct);

                return Ok(new { ok = true });
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("{conversationId:int}/messages")]
        public async Task<IActionResult> GetMessages(
            int conversationId,
            [FromQuery] int me,
            [FromQuery] int limit = 50,
            [FromQuery] int? beforeMessageId = null,
            CancellationToken ct = default)
        {
            try
            {
                return Ok(await _messageCallService.GetMessagesAsync(conversationId, me, limit, beforeMessageId, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/mark-read")]
        public async Task<IActionResult> MarkRead(int conversationId, [FromQuery] int me, CancellationToken ct)
        {
            try
            {
                await _messageCallService.MarkReadAsync(conversationId, me, ct);
                return Ok(new { ok = true });
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/messages")]
        public async Task<IActionResult> SendTextMessage(
            int conversationId,
            [FromBody] SendMessageRequest req,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.SendTextMessageAsync(conversationId, req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/messages/image")]
        public async Task<IActionResult> SendImageMessage(
            int conversationId,
            [FromBody] SendImageMessageRequest req,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.SendImageMessageAsync(conversationId, req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{conversationId:int}/messages/audio")]
        public async Task<IActionResult> SendAudioMessage(
            int conversationId,
            [FromBody] SendAudioMessageRequest req,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.SendAudioMessageAsync(conversationId, req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("{conversationId:int}/peer")]
        public async Task<IActionResult> GetPeerInfo(int conversationId, [FromQuery] int meId, CancellationToken ct)
        {
            try
            {
                return Ok(await _messageCallService.GetPeerInfoAsync(conversationId, meId, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        private IActionResult ToErrorResult(ServiceException ex)
        {
            var error = new { message = ex.Message };

            return ex.StatusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(error),
                StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, error),
                StatusCodes.Status404NotFound => NotFound(error),
                _ => StatusCode(ex.StatusCode, error)
            };
        }
    }
}
