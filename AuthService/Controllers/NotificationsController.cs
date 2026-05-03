using AuthService.Dtos.Notifications;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNotificationRequest req, CancellationToken ct)
        {
            try
            {
                return Ok(await _notificationService.CreateAsync(req, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetByConsumer(
            [FromQuery] int consumerId,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            try
            {
                return Ok(await _notificationService.GetByConsumerAsync(consumerId, limit, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("{notificationId:int}/mark-read")]
        public async Task<IActionResult> MarkRead(
            int notificationId,
            [FromQuery] int consumerId,
            CancellationToken ct)
        {
            try
            {
                await _notificationService.MarkReadAsync(notificationId, consumerId, ct);
                return Ok(new { ok = true });
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
                StatusCodes.Status404NotFound => NotFound(ex.Message),
                _ => StatusCode(ex.StatusCode, ex.Message)
            };
        }
    }
}

