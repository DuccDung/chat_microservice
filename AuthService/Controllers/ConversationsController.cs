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
            return ex.StatusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(ex.Message),
                StatusCodes.Status403Forbidden => Forbid(),
                StatusCodes.Status404NotFound => NotFound(ex.Message),
                _ => StatusCode(ex.StatusCode, ex.Message)
            };
        }
    }
}

